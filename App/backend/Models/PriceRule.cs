namespace DatSan.Api.Models;

// Maps existing dbo.price_rules. weekday: 0 = Chủ nhật; hours are [start_hour, end_hour).
public sealed class PriceRule
{
    public long Id { get; set; }
    public long VenueId { get; set; }
    public string Sport { get; set; } = "";
    public short Weekday { get; set; }
    public short StartHour { get; set; }
    public short EndHour { get; set; }
    public long PricePerHour { get; set; }
}
