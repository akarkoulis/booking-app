using System.Net;
using System.Text;
using System.Text.Json;
using BookingApp.Web.Ai;
using BookingApp.Web.Scheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Chat;

namespace BookingApp.Tests;

public sealed class BookingDraftParserTests
{
    private sealed class DeadlineClock : TimeProvider
    {
        private TimerCallback? callback;
        private object? state;
        public TimeSpan DueTime { get; private set; }
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        public override ITimer CreateTimer(TimerCallback action, object? value, TimeSpan dueTime, TimeSpan period)
        {
            callback = action;
            state = value;
            DueTime = dueTime;
            return new TestTimer();
        }
        public void Expire() => callback!(state);
        private sealed class TestTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class DelayedStream : Stream
    {
        public TaskCompletionSource Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            Reading.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }

    private static HttpResponseMessage Completion(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test", @object = "chat.completion", created = 1793095200, model = "test-model",
            choices = new[] { new { index = 0, finish_reason = "stop", message = new { role = "assistant", content } } }
        }), Encoding.UTF8, "application/json")
    };

    private static OpenAiBookingDraftParser Parser(HttpClient http, TimeProvider? clock = null)
    {
        var options = BookingDraftRegistration.ClientOptions();
        options.Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(http);
        var client = new ChatClient("test-model", new System.ClientModel.ApiKeyCredential("test-key"), options);
        return new(client, RuleTestData.OfficeTime, new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Office:TimeZone"] = "Europe/London" }).Build(), clock ?? TimeProvider.System);
    }

    [Fact]
    public async Task Sdk_sends_strict_schema_none_and_no_optional_generation_controls()
    {
        string? sent = null;
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            sent = await request.Content!.ReadAsStringAsync(token);
            return Completion(BookingDraftTests.Json(BookingDraftTests.Complete));
        }));
        var fields = await Parser(http).ParseAsync("Thames Friday", default);
        Assert.Equal("2026-10-30", fields.Date);
        using var document = JsonDocument.Parse(sent!);
        var body = document.RootElement;
        Assert.Equal("none", body.GetProperty("reasoning_effort").GetString());
        Assert.False(body.TryGetProperty("temperature", out _));
        Assert.False(body.TryGetProperty("max_completion_tokens", out _));
        Assert.False(body.TryGetProperty("max_tokens", out _));
        var format = body.GetProperty("response_format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        var schema = format.GetProperty("json_schema");
        Assert.True(schema.GetProperty("strict").GetBoolean());
        Assert.False(schema.GetProperty("schema").GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(10, schema.GetProperty("schema").GetProperty("required").GetArrayLength());
        Assert.Equal("Thames Friday", body.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public async Task Upstream_errors_are_unavailable_with_one_attempt(int status)
    {
        var attempts = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            { Content = new StringContent("{\"error\":{\"message\":\"private upstream detail\",\"type\":\"invalid_request_error\",\"code\":\"model_not_found\"}}", Encoding.UTF8, "application/json") });
        }));
        var error = await Assert.ThrowsAsync<BookingDraftUnavailableException>(() => Parser(http).ParseAsync("meeting", default));
        Assert.DoesNotContain("private", error.Message);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Client_cancellation_is_propagated_instead_of_unavailable()
    {
        using var source = new CancellationTokenSource();
        using var http = new HttpClient(new Handler(async (_, token) =>
        {
            source.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return Completion("{}");
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Parser(http).ParseAsync("meeting", source.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Whole_call_deadline_covers_delayed_body_and_client_cancellation_wins_race(bool disconnect)
    {
        var clock = new DeadlineClock();
        using var source = new CancellationTokenSource();
        using var stream = new DelayedStream();
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StreamContent(stream) })));
        var pending = Parser(http, clock).ParseAsync("meeting", source.Token);
        await stream.Reading.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(10), clock.DueTime);
        if (disconnect) source.Cancel();
        clock.Expire();
        if (disconnect) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        else await Assert.ThrowsAsync<BookingDraftUnavailableException>(() => pending);
    }

    [Fact]
    public async Task Network_timeout_maps_to_unavailable()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new TimeoutException()));
        await Assert.ThrowsAsync<BookingDraftUnavailableException>(() => Parser(http).ParseAsync("meeting", default));
    }

    [Fact]
    public async Task Transport_task_cancellation_without_client_cancellation_is_unavailable()
    {
        using var http = new HttpClient(new Handler((_, _) => throw new TaskCanceledException()));
        await Assert.ThrowsAsync<BookingDraftUnavailableException>(() => Parser(http).ParseAsync("meeting", CancellationToken.None));
    }

    [Fact]
    public void Prompt_uses_office_date_weekday_and_zone()
    {
        var prompt = BookingDraftPrompt.Create(new DeadlineClock(), RuleTestData.OfficeTime, "Europe/London");
        Assert.Contains("Office current date: 2026-10-27", prompt);
        Assert.Contains("Office current weekday: Tuesday", prompt);
        Assert.Contains("Office time zone: Europe/London", prompt);
    }

    [Fact]
    public async Task Fake_long_request_still_returns_valid_partial_fields()
    {
        var fields = await new FakeBookingDraftParser().ParseAsync(new string('x', 500), default);
        var validated = BookingDraftOutput.Read(BookingDraftTests.Json(fields), "stop", null);
        Assert.Null(validated.Date);
        Assert.Null(validated.StartTime);
        Assert.Null(validated.EndTime);
    }

    [Theory]
    [InlineData(true, null, null, typeof(FakeBookingDraftParser))]
    [InlineData(true, "key", "model", typeof(FakeBookingDraftParser))]
    [InlineData(false, "key", "model", typeof(OpenAiBookingDraftParser))]
    [InlineData(false, null, "model", typeof(UnavailableBookingDraftParser))]
    [InlineData(false, "key", " ", typeof(UnavailableBookingDraftParser))]
    public void Selection_requires_explicit_fake_flag_or_both_credentials(bool fake, string? key, string? model, Type expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["OpenAI:UseFakeParser"] = fake.ToString(), ["OpenAI:ApiKey"] = key, ["OpenAI:Model"] = model, ["Office:TimeZone"] = "Europe/London" }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(RuleTestData.OfficeTime);
        services.AddSingleton(TimeProvider.System);
        services.AddBookingDraftParser(config);
        using var provider = services.BuildServiceProvider();
        Assert.IsType(expected, provider.GetRequiredService<IBookingDraftParser>());
    }
}
