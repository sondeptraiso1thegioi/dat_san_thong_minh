namespace DatSan.Api.Models;

// Maps existing dbo.venues (cụm sân). amenities/photos are JSON arrays stored as text.
public sealed class Venue
{
    public long Id { get; set; }
    public long OwnerId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public string Amenities { get; set; } = "[]";
    public string Photos { get; set; } = "[]";
    public string? Description { get; set; }
    public string Status { get; set; } = "";
    public string? RejectReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<Court> Courts { get; set; } = [];
    public List<PriceRule> PriceRules { get; set; } = [];
    public User? Owner { get; set; }
}
