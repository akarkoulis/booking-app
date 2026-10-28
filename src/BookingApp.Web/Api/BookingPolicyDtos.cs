using System.ComponentModel.DataAnnotations;
using BookingApp.Web.Domain;

namespace BookingApp.Web.Api;

public sealed class BookingPolicyRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "The maximum active room bookings per user must be at least 1.")]
    public int? MaxActiveRoomBookingsPerUser { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "The default minimum notice must be at least 1 minute.")]
    public int? DefaultMinNoticeMinutes { get; set; }
}

public sealed record BookingPolicyResponse(int? MaxActiveRoomBookingsPerUser, int? DefaultMinNoticeMinutes)
{
    public static BookingPolicyResponse From(BookingPolicy policy) => new(policy.MaxActiveRoomBookingsPerUser, policy.DefaultMinNoticeMinutes);
}
