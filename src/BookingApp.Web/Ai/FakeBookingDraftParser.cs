namespace BookingApp.Web.Ai;

public sealed class FakeBookingDraftParser : IBookingDraftParser
{
    public Task<BookingDraftFields> ParseAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Deliberately partial: this development-only parser never invents bookable times.
        return Task.FromResult(new BookingDraftFields { Title = "Development draft",
            NotUnderstood = "Fake drafting is enabled. Add a date, times and what to book using the form below." });
    }
}
