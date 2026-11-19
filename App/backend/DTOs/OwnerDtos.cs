namespace DatSan.Api.DTOs;

/// <summary>
/// Toàn bộ cụm sân gửi lên một lần khi tạo hoặc sửa (UC04 bước 2–6).
/// Các trường để nullable để OwnerVenueRules tự kiểm tra và báo lỗi tiếng Việt,
/// thay vì lỗi "required" tiếng Anh mặc định của ASP.NET Core.
/// </summary>
public sealed class OwnerVenueInput
{
    public string? Name { get; init; }
    public string? Address { get; init; }
    public double Lat { get; init; }
    public double Lng { get; init; }
    /// <summary>Giờ mở/đóng cửa dạng HH:mm, phút là 00 hoặc 30.</summary>
    public string? OpenTime { get; init; }
    public string? CloseTime { get; init; }
    public List<string>? Amenities { get; init; }
    public string? Description { get; init; }
    public List<OwnerCourtInput>? Courts { get; init; }
    public List<OwnerPriceInput>? Prices { get; init; }
}

/// <summary>Id null = sân con mới. Sân con không còn trong danh sách sẽ chuyển sang ngừng hoạt động (không xóa, vì có thể đã có booking).</summary>
public sealed class OwnerCourtInput
{
    public string? Id { get; init; }
    public string? Sport { get; init; }
    public string? Name { get; init; }
    public string? Environment { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class OwnerPriceInput
{
    public string? Sport { get; init; }
    /// <summary>0 = Chủ nhật … 6 = Thứ 7, giống cột weekday trong SQL.</summary>
    public int Weekday { get; init; }
    public int StartHour { get; init; }
    public int EndHour { get; init; }
    public long PricePerHour { get; init; }
}

public sealed record OwnerCourtDto(string Id, string Sport, string Name, string Environment, bool IsActive);
public sealed record OwnerPriceDto(string Sport, int Weekday, int StartHour, int EndHour, long PricePerHour);

public sealed record OwnerVenueDetailDto(
    string Id, string Name, string Address, double Lat, double Lng, string OpenTime, string CloseTime,
    IReadOnlyList<string> Amenities, string? Description, string Status, string? RejectReason, bool IsVip,
    IReadOnlyList<OwnerCourtDto> Courts, IReadOnlyList<OwnerPriceDto> Prices, DateTimeOffset CreatedAt);

public sealed record OwnerVenueListItemDto(
    string Id, string Name, string Address, string Status, string? RejectReason, bool IsVip,
    IReadOnlyList<string> Sports, int ActiveCourts, int TotalCourts, bool HasPrices,
    string OpenTime, string CloseTime, DateTimeOffset CreatedAt);

/// <summary>ReviewRequired = lần lưu này đưa cụm sân về chờ duyệt (tạo mới, gửi lại sau khi bị từ chối, hoặc đổi tên/địa chỉ/vị trí).</summary>
public sealed record OwnerVenueSaveResult(OwnerVenueDetailDto Venue, bool ReviewRequired);
