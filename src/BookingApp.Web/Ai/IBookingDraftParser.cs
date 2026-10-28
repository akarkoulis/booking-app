namespace BookingApp.Web.Ai;

public interface IBookingDraftParser
{
    Task<BookingDraftFields> ParseAsync(string text, CancellationToken cancellationToken);
}
