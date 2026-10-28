namespace BookingApp.Web.Domain;

public sealed class Resource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public ResourceType Type { get; set; }
    public string? Location { get; set; }
    public int? Seats { get; set; }
    public int Quantity { get; set; } = 1;
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }
    public int? MinDurationMinutes { get; set; }
    public int? MaxDurationMinutes { get; set; }
    public int? MaxAdvanceDays { get; set; }
    // Null uses the policy default; zero explicitly disables notice for this resource.
    public int? MinNoticeMinutes { get; set; }
    public bool IsActive { get; set; } = true;
}
