namespace DatSan.Api.Utils;

public static class Geo
{
    // Haversine distance, same formula as dbo.distance_km in the SQL schema.
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double degrees) => degrees * Math.PI / 180.0;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2)
            + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lng2 - lng1) / 2), 2);
        return 2 * 6371.0 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    public static bool IsValid(double lat, double lng) => lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
}

public static class VietnamTime
{
    // Việt Nam không có giờ mùa hè, nên dùng offset cố định +07:00 (không phụ thuộc time zone của máy chủ).
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);
    public static DateTimeOffset ToLocal(DateTimeOffset value) => value.ToOffset(Offset);
}
