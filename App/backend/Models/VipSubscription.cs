namespace DatSan.Api.Models;

// Maps existing dbo.vip_subscriptions. A venue is VIP while an 'active' row covers "now".
public sealed class VipSubscription
{
    public long Id { get; set; }
    public long VenueId { get; set; }
    public long PlanId { get; set; }
    public DateTimeOffset? StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
