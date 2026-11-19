using System.Globalization;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Interface;
using DatSan.Api.Utils;

namespace DatSan.Api.Services.Implements;

public sealed class PlayerBookingService(
    IUserRepository users, IPlayerBookingRepository bookings, ISettingRepository settings, TimeProvider clock)
    : IPlayerBookingService
{
    private static readonly string[] SportOrder = ["tennis", "pickleball", "badminton", "table_tennis"];

    public async Task<VenueDetailResponse> GetVenueDetailAsync(
        long userId, long venueId, DateOnly? date, double? lat, double? lng, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = BookingRules.Today(now);
        var day = date ?? today;
        if (day < today || day > today.AddDays(BookingRules.MaxDaysAhead))
            throw Invalid("INVALID_DATE", $"Chỉ xem được lịch sân trong {BookingRules.MaxDaysAhead} ngày tới.");
        if (lat.HasValue != lng.HasValue || (lat is double a && lng is double b && !Geo.IsValid(a, b)))
            throw Invalid("INVALID_LOCATION", "Vị trí không hợp lệ.");

        var venue = await bookings.GetApprovedVenueAsync(venueId, cancellationToken)
            ?? throw new ApiException(404, "VENUE_NOT_FOUND", "Không tìm thấy cụm sân hoặc cụm sân đã ngừng nhận đặt.");
        var user = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new ApiException(401, "UNAUTHORIZED", "Vui lòng đăng nhập lại.");

        var courts = venue.Courts.Where(c => c.IsActive)
            .OrderBy(c => SportRank(c.Sport)).ThenBy(c => c.Name, VenueCards.NameOrder).ToList();
        var rules = venue.PriceRules
            .Select(p => new PriceRuleInfo(p.VenueId, p.Sport, p.Weekday, p.StartHour, p.EndHour, p.PricePerHour)).ToList();
        var summary = new VenueSummary(
            venue.Id, venue.Name, venue.Address, venue.Lat, venue.Lng, venue.OpenTime, venue.CloseTime,
            venue.Amenities, venue.Photos,
            courts.Select(c => c.Sport).Distinct().ToList(), courts.Select(c => c.Environment).Distinct().ToList(),
            rules.Count == 0 ? null : rules.Min(r => r.PricePerHour),
            await bookings.IsVipAsync(venue.Id, now, cancellationToken));
        var localTime = TimeOnly.FromTimeSpan(VietnamTime.ToLocal(now).TimeOfDay);
        var card = VenueCards.ToCard(summary, VenueCards.ResolveLocation(user, lat, lng), localTime);

        var hours = Enumerable.Range(0, 24)
            .Where(h => SlotPricing.WithinOpeningHours(venue.OpenTime, venue.CloseTime, h, 1)).ToList();
        var dayStart = BookingRules.SlotStart(day, 0);
        var busy = courts.Count == 0 ? []
            : await bookings.GetBusyAsync(courts.Select(c => c.Id).ToList(), dayStart, dayStart.AddDays(1), now, cancellationToken);

        var schedules = courts.Select(court => new CourtScheduleDto(
            Id(court.Id), court.Name, court.Sport, court.Environment,
            hours.Select(hour =>
            {
                var start = BookingRules.SlotStart(day, hour);
                var price = SlotPricing.PriceFor(rules, venue.Id, court.Sport, day.DayOfWeek, hour, 1);
                var state = start <= now ? "past"
                    : busy.Any(x => x.CourtId == court.Id && BookingRules.Overlaps(x.StartAt, x.EndAt, start, start.AddHours(1))) ? "booked"
                    : price is null ? "unpriced"
                    : "free";
                return new SlotCellDto(hour, price, state);
            }).ToList())).ToList();

        var holdMinutes = BookingRules.ParseHoldMinutes(await settings.GetValueAsync("booking_hold_minutes", cancellationToken));
        return new VenueDetailResponse(
            card, venue.Description, VenueCards.ReadStrings(venue.Photos), day, today.AddDays(BookingRules.MaxDaysAhead),
            hours, BookingRules.MaxHours, holdMinutes, now, schedules);
    }

    public async Task<BookingDetailDto> CreateHoldAsync(long userId, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (!long.TryParse(request.CourtId, NumberStyles.None, CultureInfo.InvariantCulture, out var courtId))
            throw Invalid("INVALID_COURT", "Vui lòng chọn sân con.");
        if (request.Date is not DateOnly date || request.StartHour is not int startHour)
            throw Invalid("INVALID_SLOT", "Vui lòng chọn ngày và giờ bắt đầu.");
        var hours = request.Hours ?? 1;
        if (startHour is < 0 or > 23 || hours < 1 || hours > BookingRules.MaxHours || startHour + hours > 24)
            throw Invalid("INVALID_SLOT", $"Mỗi lần đặt từ 1 đến {BookingRules.MaxHours} giờ liên tiếp, kết thúc trước 24:00.");
        var today = BookingRules.Today(now);
        if (date > today.AddDays(BookingRules.MaxDaysAhead))
            throw Invalid("INVALID_DATE", $"Chỉ đặt được sân trong {BookingRules.MaxDaysAhead} ngày tới.");
        var start = BookingRules.SlotStart(date, startHour);
        if (start <= now)
            throw Invalid("SLOT_IN_PAST", "Giờ chơi đã qua. Vui lòng chọn giờ khác.");

        var venue = await bookings.GetVenueOfActiveCourtAsync(courtId, cancellationToken)
            ?? throw new ApiException(404, "COURT_NOT_FOUND", "Sân con không còn nhận đặt. Vui lòng chọn sân khác.");
        var court = venue.Courts.Single(c => c.Id == courtId);
        if (!SlotPricing.WithinOpeningHours(venue.OpenTime, venue.CloseTime, startHour, hours))
            throw Invalid("OUTSIDE_OPENING_HOURS",
                $"Khung giờ nằm ngoài giờ mở cửa ({OwnerVenueRules.FormatTime(venue.OpenTime)}–{OwnerVenueRules.FormatTime(venue.CloseTime)}).");
        var rules = venue.PriceRules
            .Select(p => new PriceRuleInfo(p.VenueId, p.Sport, p.Weekday, p.StartHour, p.EndHour, p.PricePerHour)).ToList();
        var price = SlotPricing.PriceFor(rules, venue.Id, court.Sport, date.DayOfWeek, startHour, hours)
            ?? throw Invalid("SLOT_NOT_PRICED", "Chủ sân chưa đặt giá cho khung giờ này. Vui lòng chọn giờ khác.");

        if (await bookings.GetActiveHoldIdAsync(userId, now, cancellationToken) is not null)
            throw new ApiException(409, "HOLD_EXISTS",
                "Bạn đang giữ chỗ một sân khác. Hãy thanh toán hoặc hủy chỗ đó trước khi giữ chỗ mới.");

        var holdMinutes = BookingRules.ParseHoldMinutes(await settings.GetValueAsync("booking_hold_minutes", cancellationToken));
        var booking = new Booking
        {
            UserId = userId, CourtId = courtId, StartAt = start, EndAt = start.AddHours(hours),
            Status = "pending", Price = price, Source = "manual", HoldExpiresAt = now.AddMinutes(holdMinutes),
        };
        switch (await bookings.CreateHoldAsync(booking, now, cancellationToken))
        {
            case HoldResult.SlotTaken:
                throw new ApiException(409, "SLOT_TAKEN", "Khung giờ vừa có người đặt. Vui lòng chọn giờ khác.");
            case HoldResult.Busy:
                throw new ApiException(409, "TRY_AGAIN", "Hệ thống đang bận. Vui lòng thử lại.");
        }

        return ToDto(new BookingView(
            booking.Id, userId, booking.Status, venue.Id, venue.Name, venue.Address, venue.Lat, venue.Lng,
            court.Name, court.Sport, court.Environment, booking.StartAt, booking.EndAt, booking.Price,
            booking.HoldExpiresAt, null, booking.CreatedAt == default ? now : booking.CreatedAt), now);
    }

    public async Task<BookingDetailDto?> GetActiveHoldAsync(long userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await bookings.GetActiveHoldIdAsync(userId, now, cancellationToken) is long id
            ? await GetBookingAsync(userId, id, cancellationToken)
            : null;
    }

    public async Task<PlayerBookingsResponse> GetBookingsAsync(
        long userId, string view, int page, int pageSize, CancellationToken cancellationToken)
    {
        view = (view ?? "all").Trim().ToLowerInvariant();
        if (view is not ("all" or "upcoming" or "history"))
            throw Invalid("INVALID_BOOKING_VIEW", "view chỉ nhận all, upcoming hoặc history.");
        if (page < 1 || (long)(page - 1) * pageSize > int.MaxValue)
            throw Invalid("INVALID_PAGE", "page không hợp lệ.");
        if (pageSize is < 1 or > 50) throw Invalid("INVALID_PAGE_SIZE", "pageSize phải từ 1 đến 50.");

        var now = clock.GetUtcNow();
        var result = await bookings.GetPlayerBookingsAsync(userId, view, page, pageSize, now, cancellationToken);
        return new PlayerBookingsResponse(
            result.Items.Select(v => ToListItem(v, now)).ToList(), page, pageSize, result.TotalCount,
            result.TotalCount == 0 ? 0 : (int)Math.Ceiling(result.TotalCount / (double)pageSize), view, now);
    }

    public async Task<BookingDetailDto> GetBookingAsync(long userId, long bookingId, CancellationToken cancellationToken)
    {
        var view = await bookings.GetViewAsync(bookingId, cancellationToken);
        if (view is null || view.UserId != userId) throw NotFound();
        return ToDto(view, clock.GetUtcNow());
    }

    public async Task<BookingDetailDto> CancelBookingAsync(long userId, long bookingId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var result = await bookings.CancelOwnedBookingAsync(bookingId, userId, now, cancellationToken);
        switch (result)
        {
            case CancelBookingResult.NotFound:
                throw NotFound();
            case CancelBookingResult.NotCancellable:
                throw new ApiException(409, "BOOKING_NOT_CANCELLABLE", "Booking đã hủy/hoàn thành hoặc không ở trạng thái có thể hủy.");
            case CancelBookingResult.TooLate:
                throw new ApiException(409, "CANCELLATION_WINDOW_PASSED", "Chỉ có thể hủy booking đã thanh toán khi còn ít nhất 24 giờ trước giờ chơi.");
            case CancelBookingResult.PaidPaymentNotFound:
                throw new ApiException(409, "PAID_PAYMENT_NOT_FOUND", "Không tìm thấy giao dịch đã thanh toán hợp lệ cho booking này.");
        }
        return await GetBookingAsync(userId, bookingId, cancellationToken);
    }

    private static PlayerBookingListItemDto ToListItem(BookingView v, DateTimeOffset now)
    {
        var status = BookingRules.EffectiveStatus(v.Status, v.HoldExpiresAt, now);
        var cancellation = BookingRules.CancellationAvailability(status, v.HoldExpiresAt, v.StartAt, v.PaymentStatus, now);
        return new PlayerBookingListItemDto(
            Id(v.Id), status, Id(v.VenueId), v.VenueName, v.VenueAddress, v.CourtName, v.Sport, v.Environment,
            v.StartAt, v.EndAt, (int)Math.Round((v.EndAt - v.StartAt).TotalHours), v.Price,
            v.HoldExpiresAt, v.CancelReason, v.CreatedAt, v.PaymentStatus, cancellation.CanCancel, cancellation.Info);
    }

    private static BookingDetailDto ToDto(BookingView v, DateTimeOffset now)
    {
        var status = BookingRules.EffectiveStatus(v.Status, v.HoldExpiresAt, now);
        var cancellation = BookingRules.CancellationAvailability(status, v.HoldExpiresAt, v.StartAt, v.PaymentStatus, now);
        return new BookingDetailDto(
            Id(v.Id), status, Id(v.VenueId), v.VenueName, v.VenueAddress,
            v.Lat, v.Lng, v.CourtName, v.Sport, v.Environment, v.StartAt, v.EndAt,
            (int)Math.Round((v.EndAt - v.StartAt).TotalHours), v.Price, v.HoldExpiresAt, v.CancelReason, v.CreatedAt, now,
            v.PaymentStatus, cancellation.CanCancel, cancellation.Info);
    }

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static int SportRank(string sport)
    {
        var index = Array.IndexOf(SportOrder, sport);
        return index < 0 ? int.MaxValue : index;
    }

    private static ApiException Invalid(string code, string message) => new(400, code, message);
    private static ApiException NotFound() => new(404, "BOOKING_NOT_FOUND", "Không tìm thấy lượt đặt sân.");
}
