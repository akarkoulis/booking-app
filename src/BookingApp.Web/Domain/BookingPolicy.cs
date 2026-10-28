namespace BookingApp.Web.Domain;

public sealed class BookingPolicy
{
    public int Id { get; set; } = 1;
    public int? MaxActiveRoomBookingsPerUser { get; set; }
    public int? DefaultMinNoticeMinutes { get; set; }
}
