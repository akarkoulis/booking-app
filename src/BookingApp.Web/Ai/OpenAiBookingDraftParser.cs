using System.ClientModel;
using System.Text.Json;
using BookingApp.Web.Scheduling;
using OpenAI.Chat;

namespace BookingApp.Web.Ai;

public sealed class OpenAiBookingDraftParser(ChatClient client, OfficeTime officeTime, IConfiguration configuration,
    TimeProvider clock) : IBookingDraftParser
{
    public async Task<BookingDraftFields> ParseAsync(string text, CancellationToken cancellationToken)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var options = new ChatCompletionOptions
        {
#pragma warning disable OPENAI001 // SDK marks the typed reasoning option experimental; the wire value is fixed by 007.
            ReasoningEffortLevel = new ChatReasoningEffortLevel("none"),
#pragma warning restore OPENAI001
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("booking_draft", BinaryData.FromString(BookingDraftPrompt.Schema), jsonSchemaIsStrict: true)
        };
        try
        {
            var result = await client.CompleteChatAsync([
                new SystemChatMessage(BookingDraftPrompt.Create(clock, officeTime, configuration["Office:TimeZone"]!)),
                new UserChatMessage(text)
            ], options, linked.Token);
            cancellationToken.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested) throw new BookingDraftUnavailableException();
            var completion = result.Value;
            return BookingDraftOutput.Read(string.Concat(completion.Content.Select(part => part.Text)),
                completion.FinishReason == ChatFinishReason.Stop ? "stop" : completion.FinishReason.ToString(), completion.Refusal);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (Exception error) when (error is ClientResultException or HttpRequestException or JsonException or TimeoutException or OperationCanceledException)
        {
            // Upstream diagnostics can contain request content; expose only a fixed unavailable response.
            throw new BookingDraftUnavailableException();
        }
    }
}
