using System.Globalization;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Interface;

namespace DatSan.Api.Services.Implements;

/// <summary>Admin duyệt, từ chối, ẩn cụm sân (UC05, F12).</summary>
public sealed class AdminVenueService(IAdminVenueRepository venues, TimeProvider clock) : IAdminVenueService
{
    public static readonly string[] Statuses = ["pending", "approved", "rejected", "deleted"];
    public const int MinReason = 5;
    public const int MaxReason = 500;

    public async Task<AdminVenueListResponse> ListAsync(string? status, CancellationToken cancellationToken)
    {
        var wanted = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim();
        if (!Statuses.Contains(wanted)) throw new ApiException(400, "INVALID_STATUS", "Trạng thái cụm sân không hợp lệ.");
        var counts = await venues.CountByStatusAsync(cancellationToken);
        int Count(string s) => counts.TryGetValue(s, out var n) ? n : 0;
        var list = await venues.ListAsync(wanted, cancellationToken);
        return new AdminVenueListResponse(
            new AdminVenueCountsDto(Count("pending"), Count("approved"), Count("rejected"), Count("deleted")),
            list.Select(v => new AdminVenueListItemDto(
                Id(v.Id), v.Name, v.Address, v.Status, v.RejectReason, v.Owner?.Name ?? "", v.Owner?.Email ?? "",
                v.Courts.Where(c => c.IsActive).Select(c => c.Sport).Distinct()
                    .OrderBy(s => Array.IndexOf(OwnerVenueRules.Sports, s)).ToList(),
                v.Courts.Count(c => c.IsActive), v.CreatedAt)).ToList());
    }

    public async Task<AdminVenueDetailDto> GetAsync(long venueId, CancellationToken cancellationToken)
    {
        var venue = await Load(venueId, cancellationToken);
        var upcoming = await venues.GetUpcomingBookingsAsync(venue.Id, clock.GetUtcNow(), cancellationToken);
        return ToDetail(venue, upcoming.Count);
    }

    /// <summary>Chỉ duyệt cụm sân đang chờ duyệt; cụm sân bắt đầu xuất hiện trong tìm kiếm và gợi ý.</summary>
    public async Task<AdminVenueActionResult> ApproveAsync(long venueId, CancellationToken cancellationToken)
    {
        var venue = await Load(venueId, cancellationToken);
        RequireStatus(venue, "pending", "Chỉ duyệt được cụm sân đang chờ duyệt.");
        if (!venue.Courts.Any(c => c.IsActive) || venue.PriceRules.Count == 0)
            throw new ApiException(409, "VENUE_INCOMPLETE", "Cụm sân chưa có sân con hoạt động hoặc bảng giá nên chưa duyệt được.");
        venue.Status = "approved";
        venue.RejectReason = null;
        await venues.SaveChangesAsync(cancellationToken);
        return new AdminVenueActionResult(ToDetail(venue, 0), 0, 0);
    }

    /// <summary>Từ chối kèm lý do (UC05-3a); chủ sân thấy lý do, sửa rồi gửi lại.</summary>
    public async Task<AdminVenueActionResult> RejectAsync(long venueId, string? reason, CancellationToken cancellationToken)
    {
        var text = ValidReason(reason);
        var venue = await Load(venueId, cancellationToken);
        RequireStatus(venue, "pending", "Chỉ từ chối được cụm sân đang chờ duyệt. Cụm sân đã duyệt thì dùng \"Ẩn\".");
        venue.Status = "rejected";
        venue.RejectReason = text;
        await venues.SaveChangesAsync(cancellationToken);
        return new AdminVenueActionResult(ToDetail(venue, 0), 0, 0);
    }

    /// <summary>
    /// Ẩn (xóa mềm) cụm sân (UC05-3b): chuyển deleted, hủy các booking sắp tới, đánh dấu cần hoàn tiền
    /// cho những booking đã thanh toán. Tất cả lưu trong một lần.
    /// </summary>
    public async Task<AdminVenueActionResult> HideAsync(long venueId, string? reason, CancellationToken cancellationToken)
    {
        var text = ValidReason(reason);
        var venue = await Load(venueId, cancellationToken);
        if (venue.Status == "deleted") throw new ApiException(409, "VENUE_ALREADY_HIDDEN", "Cụm sân đã bị ẩn trước đó.");

        var bookings = await venues.GetUpcomingBookingsAsync(venue.Id, clock.GetUtcNow(), cancellationToken);
        foreach (var booking in bookings)
        {
            booking.Status = "cancelled";
            booking.CancelReason = $"Cụm sân bị quản trị viên ẩn: {text}";
        }
        var payments = await venues.GetPaidPaymentsAsync(bookings.Select(b => b.Id).ToList(), cancellationToken);
        foreach (var payment in payments)
        {
            payment.Status = "refund_pending";
            // Chưa chuyển tiền cho chủ sân thì không còn khoản nào phải trả (ck_payments_payout_state);
            // đã chuyển rồi thì giữ "paid" để admin thu hồi khi đối soát.
            if (payment.PayoutStatus == "pending") payment.PayoutStatus = "none";
        }
        venue.Status = "deleted";
        venue.RejectReason = text;
        await venues.SaveChangesAsync(cancellationToken);
        return new AdminVenueActionResult(ToDetail(venue, 0), bookings.Count, payments.Count);
    }

    private async Task<Venue> Load(long venueId, CancellationToken cancellationToken) =>
        await venues.GetAsync(venueId, cancellationToken)
            ?? throw new ApiException(404, "VENUE_NOT_FOUND", "Không tìm thấy cụm sân.");

    private static void RequireStatus(Venue venue, string status, string message)
    {
        if (venue.Status != status) throw new ApiException(409, "INVALID_VENUE_STATUS", message);
    }

    public static string ValidReason(string? reason)
    {
        var text = (reason ?? "").Trim();
        if (text.Length is < MinReason or > MaxReason)
            throw new ApiException(400, "VALIDATION_ERROR", $"Lý do từ {MinReason} đến {MaxReason} ký tự để chủ sân biết cần sửa gì.");
        return text;
    }

    private static AdminVenueDetailDto ToDetail(Venue v, int upcoming) => new(
        Id(v.Id), v.Name, v.Address, v.Lat, v.Lng,
        OwnerVenueRules.FormatTime(v.OpenTime), OwnerVenueRules.FormatTime(v.CloseTime),
        VenueCards.ReadStrings(v.Amenities), v.Description, v.Status, v.RejectReason, v.CreatedAt,
        new AdminOwnerDto(Id(v.OwnerId), v.Owner?.Name ?? "", v.Owner?.Email ?? "", v.Owner?.Phone, v.Owner?.Status ?? ""),
        v.Courts.OrderBy(c => c.Id).Select(c => new OwnerCourtDto(Id(c.Id), c.Sport, c.Name, c.Environment, c.IsActive)).ToList(),
        v.PriceRules.OrderBy(p => Array.IndexOf(OwnerVenueRules.Sports, p.Sport)).ThenBy(p => p.Weekday).ThenBy(p => p.StartHour)
            .Select(p => new OwnerPriceDto(p.Sport, p.Weekday, p.StartHour, p.EndHour, p.PricePerHour)).ToList(),
        upcoming);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);
}
