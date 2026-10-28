using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using OpenAI.Chat;

namespace BookingApp.Web.Ai;

public static class BookingDraftRegistration
{
    public static OpenAIClientOptions ClientOptions() => new()
    {
        RetryPolicy = new ClientRetryPolicy(0),
        NetworkTimeout = TimeSpan.FromSeconds(10)
    };

    public static IServiceCollection AddBookingDraftParser(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue<bool>("OpenAI:UseFakeParser"))
            services.AddSingleton<IBookingDraftParser, FakeBookingDraftParser>();
        else if (!string.IsNullOrWhiteSpace(configuration["OpenAI:ApiKey"]) && !string.IsNullOrWhiteSpace(configuration["OpenAI:Model"]))
        {
            services.AddSingleton(new ChatClient(configuration["OpenAI:Model"]!, new ApiKeyCredential(configuration["OpenAI:ApiKey"]!), ClientOptions()));
            services.AddSingleton<IBookingDraftParser, OpenAiBookingDraftParser>();
        }
        else services.AddSingleton<IBookingDraftParser, UnavailableBookingDraftParser>();
        return services;
    }
}
