using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Implements;
using Xunit;

namespace DatSan.Api.Tests;

public sealed class PlayerBookingServiceTests
{
    // 2026-11-19 23:30 UTC = 06:30 thứ Sáu 20/11/2026 giờ Việt Nam.
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 23, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 11, 20);
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    [Fact]
    public async Task Venue_detail_lists_opening_hours_and_marks_each_slot()
    {
        var repo = new FakeRepo { Busy = [new BusyInterval(1, At(18), At(20))] };
        var detail = await Service(repo).GetVenueDetailAsync(4, 1, null, null, null, default);

        Assert.Equal(Friday, detail.Date);
        Assert.Equal(Enumerable.Range(6, 16), detail.Hours); // Mở 06:00–22:00 → giờ bắt đầu 6..21.
        Assert.Equal(new[] { "1", "2", "11" }, detail.Courts.Select(c => c.Id)); // Tennis trước, sân con ngừng hoạt động bị bỏ.
        var tennis1 = detail.Courts[0].Slots.ToDictionary(s => s.Hour);
        Assert.Equal("past", tennis1[6].State);           // 06:00 đã bắt đầu lúc 06:30.
        Assert.Equal(("free", (long?)120000), (tennis1[7].State, tennis1[7].Price));
        Assert.Equal(("free", (long?)150000), (tennis1[17].State, tennis1[17].Price));
        Assert.Equal("booked", tennis1[18].State);
        Assert.Equal("booked", tennis1[19].State);
        Assert.Equal("free", tennis1[20].State);
        Assert.Equal("free", detail.Courts[1].Slots.Single(s => s.Hour == 18).State); // Sân khác cùng giờ vẫn trống.
        var badminton = detail.Courts[2].Slots.ToDictionary(s => s.Hour);
        Assert.Equal(("unpriced", (long?)null), (badminton[7].State, badminton[7].Price));
        Assert.Equal("free", badminton[17].State);
        Assert.Equal(3, detail.MaxHours);
        Assert.Equal(10, detail.HoldMinutes);
        Assert.Equal(new[] { "tennis", "badminton" }, detail.Venue.Sports);
        Assert.Equal(1.2, detail.Venue.DistanceKm); // Từ vị trí trong hồ sơ người chơi.
    }

    [Fact]
    public async Task Venue_detail_on_a_later_day_has_no_past_slots_and_reads_hold_minutes()
    {
        var detail = await Service(new FakeRepo(), holdMinutes: "15").GetVenueDetailAsync(4, 1, Friday.AddDays(30), null, null, default);
        Assert.DoesNotContain(detail.Courts.SelectMany(c => c.Slots), s => s.State == "past");
        Assert.Equal(15, detail.HoldMinutes);
        Assert.Equal(Friday.AddDays(30), detail.MaxDate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public async Task Venue_detail_rejects_days_outside_the_window(int offset)
    {
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            Service(new FakeRepo()).GetVenueDetailAsync(4, 1, Friday.AddDays(offset), null, null, default));
        Assert.Equal("INVALID_DATE", error.Code);
    }

    [Fact]
    public async Task Unknown_or_unapproved_venue_is_not_found()
    {
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            Service(new FakeRepo()).GetVenueDetailAsync(4, 99, null, null, null, default));
        Assert.Equal((404, "VENUE_NOT_FOUND"), (error.StatusCode, error.Code));
    }

    [Fact]
    public async Task Hold_prices_each_hour_and_expires_after_configured_minutes()
    {
        var repo = new FakeRepo();
        var result = await Service(repo, holdMinutes: "15").CreateHoldAsync(4, Request("1", Friday, 16, 2), default);

        var saved = Assert.Single(repo.Created);
        Assert.Equal((4L, 1L, "pending", "manual"), (saved.UserId, saved.CourtId, saved.Status, saved.Source));
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 16, 0, 0, Vn), saved.StartAt);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 18, 0, 0, Vn), saved.EndAt);
        Assert.Equal(270000, saved.Price); // 16h thường 120k + 17h cao điểm 150k.
        Assert.Equal(Now.AddMinutes(15), saved.HoldExpiresAt);
        Assert.Equal(("pending", 2, "Tennis 1", "Cụm sân A"), (result.Status, result.Hours, result.CourtName, result.VenueName));
        Assert.Equal(Now, result.ServerNow);
    }

    [Theory]
    [InlineData("x", 0, 16, 1, 400, "INVALID_COURT")]
    [InlineData("1", null, 16, 1, 400, "INVALID_SLOT")]
    [InlineData("1", 0, 16, 4, 400, "INVALID_SLOT")]      // Tối đa 3 giờ.
    [InlineData("1", 0, 16, 0, 400, "INVALID_SLOT")]
    [InlineData("1", 0, 22, 3, 400, "INVALID_SLOT")]      // Quá 24:00.
    [InlineData("1", 0, 6, 1, 400, "SLOT_IN_PAST")]       // 06:00 đã qua lúc 06:30.
    [InlineData("1", 31, 16, 1, 400, "INVALID_DATE")]
    [InlineData("1", 1, 5, 1, 400, "OUTSIDE_OPENING_HOURS")]
    [InlineData("1", 1, 21, 2, 400, "OUTSIDE_OPENING_HOURS")] // Đóng cửa 22:00.
    [InlineData("12", 1, 16, 1, 404, "COURT_NOT_FOUND")]  // Sân con ngừng hoạt động.
    [InlineData("11", 1, 7, 1, 400, "SLOT_NOT_PRICED")]   // Cầu lông chỉ có giá 17–21h.
    public async Task Invalid_holds_are_rejected(string courtId, int? dayOffset, int startHour, int hours, int status, string code)
    {
        var repo = new FakeRepo();
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(repo).CreateHoldAsync(
            4, Request(courtId, dayOffset is int d ? Friday.AddDays(d) : null, startHour, hours), default));
        Assert.Equal((status, code), (error.StatusCode, error.Code));
        Assert.Empty(repo.Created);
    }

    [Fact]
    public async Task A_player_can_hold_only_one_slot_at_a_time()
    {
        var repo = new FakeRepo { ActiveHoldId = 50 };
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(repo).CreateHoldAsync(4, Request("1", Friday, 16, 1), default));
        Assert.Equal((409, "HOLD_EXISTS"), (error.StatusCode, error.Code));
        Assert.Empty(repo.Created);
    }

    [Theory]
    [InlineData(HoldResult.SlotTaken, "SLOT_TAKEN")]
    [InlineData(HoldResult.Busy, "TRY_AGAIN")]
    public async Task Conflicts_from_the_database_become_409(HoldResult outcome, string code)
    {
        var error = await Assert.ThrowsAsync<ApiException>(() =>
            Service(new FakeRepo { Outcome = outcome }).CreateHoldAsync(4, Request("1", Friday, 16, 1), default));
        Assert.Equal((409, code), (error.StatusCode, error.Code));
    }

    [Fact]
    public async Task Bookings_of_other_players_are_hidden()
    {
        var repo = new FakeRepo();
        repo.Add(View(7, userId: 5, "pending", Now.AddMinutes(5)));
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(repo).GetBookingAsync(4, 7, default));
        Assert.Equal((404, "BOOKING_NOT_FOUND"), (error.StatusCode, error.Code));
    }

    [Theory]
    [InlineData("pending", 5, "pending")]
    [InlineData("pending", -1, "expired")]
    [InlineData("confirmed", null, "confirmed")]
    public async Task Status_shows_expired_holds(string stored, int? minutesLeft, string expected)
    {
        var repo = new FakeRepo();
        repo.Add(View(7, userId: 4, stored, minutesLeft is int m ? Now.AddMinutes(m) : null));
        Assert.Equal(expected, (await Service(repo).GetBookingAsync(4, 7, default)).Status);
    }

    [Theory]
    [InlineData(5, BookingRules.PlayerCancelReason)]
    [InlineData(-1, BookingRules.ExpiredHoldReason)]
    public async Task Cancelling_a_hold_frees_the_slot(int minutesLeft, string reason)
    {
        var repo = new FakeRepo();
        repo.Add(View(7, userId: 4, "pending", Now.AddMinutes(minutesLeft)));
        var result = await Service(repo).CancelHoldAsync(4, 7, default);
        Assert.Equal(("cancelled", reason), (result.Status, result.CancelReason));
        Assert.Equal(1, repo.Saves);
    }

    [Fact]
    public async Task Paid_bookings_are_not_cancelled_here()
    {
        var repo = new FakeRepo();
        repo.Add(View(7, userId: 4, "confirmed", null));
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(repo).CancelHoldAsync(4, 7, default));
        Assert.Equal((409, "BOOKING_NOT_CANCELLABLE"), (error.StatusCode, error.Code));
        Assert.Equal(0, repo.Saves);
    }

    [Theory]
    [InlineData("10", 10)]
    [InlineData("\"15\"", 15)]
    [InlineData("0", 10)]
    [InlineData("61", 10)]
    [InlineData("abc", 10)]
    [InlineData(null, 10)]
    public void Hold_minutes_fall_back_to_ten(string? value, int expected) =>
        Assert.Equal(expected, BookingRules.ParseHoldMinutes(value));

    private static DateTimeOffset At(int hour) => BookingRules.SlotStart(Friday, hour);

    private static CreateBookingRequest Request(string courtId, DateOnly? date, int startHour, int hours) =>
        new() { CourtId = courtId, Date = date, StartHour = startHour, Hours = hours };

    private static BookingView View(long id, long userId, string status, DateTimeOffset? holdExpiresAt) => new(
        id, userId, status, 1, "Cụm sân A", "Cầu Giấy, Hà Nội", 21.045, 105.798, "Tennis 1", "tennis", "outdoor",
        At(16), At(17), 120000, holdExpiresAt, null, Now.AddMinutes(-5));

    private static PlayerBookingService Service(FakeRepo repo, string? holdMinutes = null) =>
        new(new FakeUsers(), repo, new FakeSettings(holdMinutes), new FixedClock(Now));

    private static Venue VenueA()
    {
        var venue = new Venue
        {
            Id = 1, OwnerId = 2, Name = "Cụm sân A", Address = "Cầu Giấy, Hà Nội", Lat = 21.0450, Lng = 105.7980,
            OpenTime = new TimeOnly(6, 0), CloseTime = new TimeOnly(22, 0), Amenities = """["gui_xe"]""", Status = "approved",
        };
        venue.Courts.AddRange([
            new Court { Id = 11, VenueId = 1, Sport = "badminton", Name = "Cầu lông A", Environment = "indoor", IsActive = true },
            new Court { Id = 2, VenueId = 1, Sport = "tennis", Name = "Tennis 2", Environment = "outdoor", IsActive = true },
            new Court { Id = 1, VenueId = 1, Sport = "tennis", Name = "Tennis 1", Environment = "outdoor", IsActive = true },
            new Court { Id = 12, VenueId = 1, Sport = "tennis", Name = "Tennis cũ", Environment = "outdoor", IsActive = false },
        ]);
        for (short day = 0; day < 7; day++)
        {
            venue.PriceRules.Add(new PriceRule { VenueId = 1, Sport = "tennis", Weekday = day, StartHour = 5, EndHour = 17, PricePerHour = 120000 });
            venue.PriceRules.Add(new PriceRule { VenueId = 1, Sport = "tennis", Weekday = day, StartHour = 17, EndHour = 21, PricePerHour = 150000 });
            venue.PriceRules.Add(new PriceRule { VenueId = 1, Sport = "tennis", Weekday = day, StartHour = 21, EndHour = 24, PricePerHour = 120000 });
            venue.PriceRules.Add(new PriceRule { VenueId = 1, Sport = "badminton", Weekday = day, StartHour = 17, EndHour = 21, PricePerHour = 100000 });
        }
        return venue;
    }

    private sealed class FakeRepo : IPlayerBookingRepository
    {
        private readonly Venue venue = VenueA();
        private readonly Dictionary<long, (BookingView View, Booking Entity)> stored = [];
        public List<BusyInterval> Busy { get; init; } = [];
        public long? ActiveHoldId { get; init; }
        public HoldResult Outcome { get; init; } = HoldResult.Created;
        public List<Booking> Created { get; } = [];
        public int Saves { get; private set; }

        public void Add(BookingView view) => stored[view.Id] = (view, new Booking
        {
            Id = view.Id, UserId = view.UserId, Status = view.Status, HoldExpiresAt = view.HoldExpiresAt,
            StartAt = view.StartAt, EndAt = view.EndAt, Price = view.Price,
        });

        public Task<Venue?> GetApprovedVenueAsync(long venueId, CancellationToken cancellationToken) =>
            Task.FromResult(venueId == venue.Id ? venue : null);
        public Task<Venue?> GetVenueOfActiveCourtAsync(long courtId, CancellationToken cancellationToken) =>
            Task.FromResult(venue.Courts.Any(c => c.Id == courtId && c.IsActive) ? venue : null);
        public Task<bool> IsVipAsync(long venueId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<BusyInterval>> GetBusyAsync(IReadOnlyCollection<long> courtIds, DateTimeOffset from,
            DateTimeOffset to, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BusyInterval>>(Busy);
        public Task<long?> GetActiveHoldIdAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(ActiveHoldId);

        public Task<HoldResult> CreateHoldAsync(Booking booking, DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (Outcome != HoldResult.Created) return Task.FromResult(Outcome);
            booking.Id = 100;
            Created.Add(booking);
            return Task.FromResult(HoldResult.Created);
        }

        public Task<BookingView?> GetViewAsync(long bookingId, CancellationToken cancellationToken) =>
            Task.FromResult(stored.TryGetValue(bookingId, out var x)
                ? x.View with { Status = x.Entity.Status, CancelReason = x.Entity.CancelReason }
                : null);
        public Task<Booking?> GetOwnedForUpdateAsync(long bookingId, long userId, CancellationToken cancellationToken) =>
            Task.FromResult(stored.TryGetValue(bookingId, out var x) && x.View.UserId == userId ? x.Entity : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken) { Saves++; return Task.CompletedTask; }
    }

    private sealed class FakeUsers : IUserRepository
    {
        private readonly User player = new()
        {
            Id = 4, Name = "An", Email = "player1@datsan.vn", Role = "player", Status = "active",
            Address = "Cầu Giấy, Hà Nội", Lat = 21.0362, Lng = 105.7906,
        };
        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(player);
        public Task<User?> FindByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<User?>(player);
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(User newUser, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSettings(string? holdMinutes) : ISettingRepository
    {
        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(key == "booking_hold_minutes" ? holdMinutes : null);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
