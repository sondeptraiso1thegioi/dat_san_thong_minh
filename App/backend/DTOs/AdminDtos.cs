namespace DatSan.Api.DTOs;

public sealed record AdminVenueCountsDto(int Pending, int Approved, int Rejected, int Deleted);

public sealed record AdminVenueListItemDto(
    string Id, string Name, string Address, string Status, string? RejectReason,
    string OwnerName, string OwnerEmail, IReadOnlyList<string> Sports, int ActiveCourts, DateTimeOffset CreatedAt);

public sealed record AdminVenueListResponse(AdminVenueCountsDto Counts, IReadOnlyList<AdminVenueListItemDto> Venues);

public sealed record AdminOwnerDto(string Id, string Name, string Email, string? Phone, string Status);

public sealed record AdminVenueDetailDto(
    string Id, string Name, string Address, double Lat, double Lng, string OpenTime, string CloseTime,
    IReadOnlyList<string> Amenities, string? Description, string Status, string? RejectReason, DateTimeOffset CreatedAt,
    AdminOwnerDto Owner, IReadOnlyList<OwnerCourtDto> Courts, IReadOnlyList<OwnerPriceDto> Prices,
    int UpcomingBookings); // Số booking sắp tới (đang giữ chỗ hoặc đã xác nhận) sẽ bị hủy nếu ẩn cụm sân.

/// <summary>Lý do từ chối hoặc ẩn; chủ sân sẽ thấy lý do này.</summary>
public sealed class AdminVenueActionRequest
{
    public string? Reason { get; init; }
}

public sealed record AdminVenueActionResult(AdminVenueDetailDto Venue, int CancelledBookings, int RefundsRequested);
