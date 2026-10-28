namespace BookingApp.Web.Ai;

public sealed class UnavailableBookingDraftParser : IBookingDraftParser
{
    public Task<BookingDraftFields> ParseAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new BookingDraftUnavailableException();
    }
}
