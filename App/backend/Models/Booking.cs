namespace DatSan.Api.Models;

// Maps existing dbo.bookings. status: pending|confirmed|cancelled|completed; source: manual|ai.
public sealed class Booking
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long CourtId { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string Status { get; set; } = "";
    public long Price { get; set; }
    public string Source { get; set; } = "";
    public DateTimeOffset? HoldExpiresAt { get; set; }
    public string? CancelReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Court? Court { get; set; }
}
