using BookingApp.Web.Domain;
using BookingApp.Web.Scheduling;

namespace BookingApp.Web.Ai;

public record BookingDraftFields
{
    public string? Date { get; init; }
    public string? StartTime { get; init; }
    public string? EndTime { get; init; }
    public ResourceType? ResourceType { get; init; }
    public string? ResourceName { get; init; }
    public string? Location { get; init; }
    public int? MinSeats { get; init; }
    public int? Quantity { get; init; }
    public string? Title { get; init; }
    public string? NotUnderstood { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public int EffectiveQuantity => Quantity ?? 1;
}

public sealed class BookingDraftValidationException(IReadOnlyList<RuleViolation> violations)
    : Exception(string.Join(" ", violations.Select(v => v.Message)))
{
    public IReadOnlyList<RuleViolation> Violations { get; } = violations;
}

public sealed class BookingDraftUnavailableException : Exception;
