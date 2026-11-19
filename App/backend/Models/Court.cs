namespace DatSan.Api.Models;

// Maps existing dbo.courts (sân con). sport: tennis|pickleball|badminton|table_tennis;
// environment: indoor|covered|outdoor.
public sealed class Court
{
    public long Id { get; set; }
    public long VenueId { get; set; }
    public string Sport { get; set; } = "";
    public string Name { get; set; } = "";
    public string Environment { get; set; } = "";
    public bool IsActive { get; set; }
    public Venue? Venue { get; set; }
}
