using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Implements;
using Xunit;

namespace DatSan.Api.Tests;

public sealed class AdminVenueServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 7, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_defaults_to_pending_and_counts_every_status()
    {
        var repository = new FakeRepository(Venue(1, "pending"), Venue(2, "pending"), Venue(3, "approved"), Venue(4, "rejected"));
        var result = await Service(repository).ListAsync(null, default);

        Assert.Equal(new[] { "2", "1" }, result.Venues.Select(v => v.Id)); // Chờ lâu nhất lên trước (cụm 2 tạo sớm hơn).
        Assert.Equal(2, result.Counts.Pending);
        Assert.Equal(1, result.Counts.Approved);
        Assert.Equal(1, result.Counts.Rejected);
        Assert.Equal(0, result.Counts.Deleted);
        Assert.Equal("Chủ sân A", result.Venues[0].OwnerName);
    }

    [Fact]
    public async Task Unknown_status_filter_is_rejected()
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(new FakeRepository()).ListAsync("all", default));
        Assert.Equal("INVALID_STATUS", error.Code);
    }

    [Fact]
    public async Task Approve_pending_venue_and_clear_old_reason()
    {
        var venue = Venue(1, "pending");
        venue.RejectReason = "Lý do cũ";
        var repository = new FakeRepository(venue);
        await Service(repository).ApproveAsync(1, default);

        Assert.Equal("approved", venue.Status);
        Assert.Null(venue.RejectReason);
        Assert.Equal(1, repository.Saves);
    }

    [Theory]
    [InlineData("approved")]
    [InlineData("rejected")]
    [InlineData("deleted")]
    public async Task Only_pending_venue_can_be_approved_or_rejected(string status)
    {
        var repository = new FakeRepository(Venue(1, status));
        var approve = await Assert.ThrowsAsync<ApiException>(() => Service(repository).ApproveAsync(1, default));
        var reject = await Assert.ThrowsAsync<ApiException>(() => Service(repository).RejectAsync(1, "Ảnh không rõ ràng", default));
        Assert.Equal(409, approve.StatusCode);
        Assert.Equal(409, reject.StatusCode);
        Assert.Equal(0, repository.Saves);
    }

    [Fact]
    public async Task Venue_without_active_court_or_price_cannot_be_approved()
    {
        var venue = Venue(1, "pending");
        venue.PriceRules.Clear();
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(new FakeRepository(venue)).ApproveAsync(1, default));
        Assert.Equal("VENUE_INCOMPLETE", error.Code);
    }

    [Fact]
    public async Task Reject_stores_trimmed_reason()
    {
        var venue = Venue(1, "pending");
        await Service(new FakeRepository(venue)).RejectAsync(1, "  Vị trí ghim không khớp địa chỉ  ", default);
        Assert.Equal("rejected", venue.Status);
        Assert.Equal("Vị trí ghim không khớp địa chỉ", venue.RejectReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("sai")]
    public async Task Reject_and_hide_need_a_reason(string? reason)
    {
        var repository = new FakeRepository(Venue(1, "pending"));
        var reject = await Assert.ThrowsAsync<ApiException>(() => Service(repository).RejectAsync(1, reason, default));
        var hide = await Assert.ThrowsAsync<ApiException>(() => Service(repository).HideAsync(1, reason, default));
        Assert.Equal("VALIDATION_ERROR", reject.Code);
        Assert.Equal("VALIDATION_ERROR", hide.Code);
    }

    [Fact]
    public async Task Hide_cancels_upcoming_bookings_and_requests_refunds()
    {
        var venue = Venue(1, "approved");
        var paidNotPaidOut = Booking(10, "confirmed");
        var paidAndPaidOut = Booking(11, "confirmed");
        var holding = Booking(12, "pending");
        var repository = new FakeRepository(venue)
        {
            Upcoming = [paidNotPaidOut, paidAndPaidOut, holding],
            Payments =
            [
                new Payment { Id = 1, Type = "booking", BookingId = 10, Amount = 150000, Status = "paid", PayoutStatus = "pending" },
                new Payment { Id = 2, Type = "booking", BookingId = 11, Amount = 150000, Status = "paid", PayoutStatus = "paid" },
            ],
        };
        var result = await Service(repository).HideAsync(1, "Sân ngừng kinh doanh", default);

        Assert.Equal("deleted", venue.Status);
        Assert.Equal("Sân ngừng kinh doanh", venue.RejectReason);
        Assert.Equal(3, result.CancelledBookings);
        Assert.Equal(2, result.RefundsRequested);
        Assert.All(new[] { paidNotPaidOut, paidAndPaidOut, holding }, b => Assert.Equal("cancelled", b.Status));
        Assert.Contains("Sân ngừng kinh doanh", holding.CancelReason);
        Assert.All(repository.Payments, p => Assert.Equal("refund_pending", p.Status));
        // Chưa trả chủ sân → không còn khoản phải trả; đã trả → giữ "paid" để admin thu hồi.
        Assert.Equal("none", repository.Payments[0].PayoutStatus);
        Assert.Equal("paid", repository.Payments[1].PayoutStatus);
        Assert.Equal(1, repository.Saves); // Một lần lưu = một transaction.
    }

    [Fact]
    public async Task Hidden_venue_cannot_be_hidden_again_and_missing_venue_is_404()
    {
        var hidden = await Assert.ThrowsAsync<ApiException>(() => Service(new FakeRepository(Venue(1, "deleted"))).HideAsync(1, "Vi phạm điều khoản", default));
        Assert.Equal("VENUE_ALREADY_HIDDEN", hidden.Code);
        var missing = await Assert.ThrowsAsync<ApiException>(() => Service(new FakeRepository()).GetAsync(99, default));
        Assert.Equal(404, missing.StatusCode);
    }

    [Fact]
    public async Task Detail_reports_upcoming_bookings_for_hide_warning()
    {
        var repository = new FakeRepository(Venue(1, "approved")) { Upcoming = [Booking(10, "confirmed"), Booking(11, "pending")] };
        var detail = await Service(repository).GetAsync(1, default);
        Assert.Equal(2, detail.UpcomingBookings);
        Assert.Equal("owner1@datsan.vn", detail.Owner.Email);
        Assert.Equal("22:00", detail.CloseTime);
    }

    private static AdminVenueService Service(FakeRepository repository) => new(repository, new FixedClock(Now));

    private static Venue Venue(long id, string status) => new()
    {
        Id = id, OwnerId = 2, Name = $"Cụm sân {id}", Address = "Cầu Giấy, Hà Nội", Lat = 21.04, Lng = 105.79,
        OpenTime = new TimeOnly(6, 0), CloseTime = new TimeOnly(22, 0), Status = status, CreatedAt = Now.AddDays(-id),
        Owner = new User { Id = 2, Name = "Chủ sân A", Email = "owner1@datsan.vn", Phone = "0900000002", Role = "owner", Status = "active" },
        Courts = [new Court { Id = 100 + id, VenueId = id, Sport = "badminton", Name = "Cầu lông 1", Environment = "indoor", IsActive = true }],
        PriceRules = [new PriceRule { VenueId = id, Sport = "badminton", Weekday = 1, StartHour = 6, EndHour = 22, PricePerHour = 80000 }],
    };

    private static Booking Booking(long id, string status) => new()
    {
        Id = id, CourtId = 101, UserId = 4, Status = status, Price = 150000,
        StartAt = Now.AddDays(1), EndAt = Now.AddDays(1).AddHours(1),
    };

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeRepository(params Venue[] venues) : IAdminVenueRepository
    {
        public List<Booking> Upcoming { get; init; } = [];
        public List<Payment> Payments { get; init; } = [];
        public int Saves { get; private set; }

        public Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(venues.GroupBy(v => v.Status).ToDictionary(g => g.Key, g => g.Count()));
        public Task<IReadOnlyList<Venue>> ListAsync(string status, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Venue>>(venues.Where(v => v.Status == status).OrderBy(v => v.CreatedAt).ToList());
        public Task<Venue?> GetAsync(long venueId, CancellationToken cancellationToken) =>
            Task.FromResult(venues.SingleOrDefault(v => v.Id == venueId));
        public Task<IReadOnlyList<Booking>> GetUpcomingBookingsAsync(long venueId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Booking>>(Upcoming);
        public Task<IReadOnlyList<Payment>> GetPaidPaymentsAsync(IReadOnlyCollection<long> bookingIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Payment>>(Payments.Where(p => p.BookingId is long id && bookingIds.Contains(id) && p.Status == "paid").ToList());
        public Task SaveChangesAsync(CancellationToken cancellationToken) { Saves++; return Task.CompletedTask; }
    }
}
