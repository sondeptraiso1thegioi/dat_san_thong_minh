namespace DatSan.Api.Models;

// Maps the columns of existing dbo.payments needed so far.
// status: pending|paid|failed|refund_pending|refunded; payout_status: none|pending|paid.
public sealed class Payment
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public long? BookingId { get; set; }
    public long Amount { get; set; }
    public string Status { get; set; } = "";
    public string PayoutStatus { get; set; } = "";
}
