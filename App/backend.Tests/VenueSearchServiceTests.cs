using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Implements;
using Xunit;

namespace DatSan.Api.Tests;

public sealed class VenueSearchServiceTests
{
    // 2026-11-19 23:30 UTC = 06:30 thứ Sáu 20/11/2026 giờ Việt Nam.
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 23, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 11, 20);

    // Khai báo Courts/Rules trước các cụm sân vì trường tĩnh được khởi tạo theo thứ tự trong file.
    private static readonly CourtInfo[] Courts =
    [
        new(1, 1, "tennis", "Tennis 1", "outdoor"), new(2, 1, "tennis", "Tennis 2", "outdoor"),
        new(11, 1, "badminton", "Cầu lông A", "indoor"),
        new(3, 2, "tennis", "Tennis mái 1", "covered"), new(4, 2, "pickleball", "Pickleball 1", "covered"),
        new(5, 2, "badminton", "Cầu lông 1", "indoor"),
        new(6, 3, "tennis", "Tennis trong nhà", "indoor"), new(7, 3, "badminton", "Cầu lông 1", "indoor"),
        new(8, 3, "badminton", "Cầu lông 2", "indoor"), new(9, 3, "table_tennis", "Bóng bàn 1", "indoor"),
        new(20, 9, "tennis", "Tennis Ba Vì", "outdoor"),
    ];

    // Giá giống bản demo: 5–17h thường, 17–21h cao điểm, 21–24h thường; mọi ngày trong tuần. Ba Vì không có bảng giá.
    private static readonly PriceRuleInfo[] Rules =
        new (long Venue, string Sport, long Normal, long Peak)[]
        {
            (1, "tennis", 120000, 150000), (2, "tennis", 160000, 200000), (2, "pickleball", 100000, 130000),
            (2, "badminton", 80000, 120000), (3, "tennis", 250000, 300000), (3, "badminton", 90000, 120000),
            (3, "table_tennis", 50000, 70000),
        }
        .SelectMany(v => Enumerable.Range(0, 7).SelectMany(day => new[]
        {
            new PriceRuleInfo(v.Venue, v.Sport, (short)day, 5, 17, v.Normal),
            new PriceRuleInfo(v.Venue, v.Sport, (short)day, 17, 21, v.Peak),
            new PriceRuleInfo(v.Venue, v.Sport, (short)day, 21, 24, v.Normal),
        }))
        .ToArray();

    // Dữ liệu theo bản demo SQL (A, B, C quanh Cầu Giấy; Far ≈ 21 km). A có thêm sân cầu lông trong nhà để thử lọc "có mái" theo từng sân con.
    private static readonly VenueSummary A = Venue(1, "Cụm sân A", "Cầu Giấy, Hà Nội", 21.0450, 105.7980, "06:00", "22:00", """["gui_xe","nuoc_uong"]""");
    private static readonly VenueSummary B = Venue(2, "Cụm sân B", "Ba Đình, Hà Nội", 21.0340, 105.8150, "06:00", "23:00", """["gui_xe","phong_thay_do","cho_thue_vot"]""");
    private static readonly VenueSummary C = Venue(3, "Cụm sân C", "Tây Hồ, Hà Nội", 21.0700, 105.8200, "05:30", "22:00", """["gui_xe","phong_thay_do","nha_tam"]""", vip: true);
    private static readonly VenueSummary Far = Venue(9, "Cụm sân Ba Vì", "Ba Vì, Hà Nội", 21.2000, 105.9000, "06:00", "22:00", "[]");

    [Fact]
    public async Task Sport_filter_keeps_distance_order_and_does_not_promote_vip()
    {
        var result = await Search(new VenueSearchQuery { Sport = "badminton" });
        Assert.Equal(new[] { "1", "2", "3" }, result.Venues.Select(v => v.Venue.Id)); // A 1,2 km, B 2,5 km, C (VIP) 4,8 km.
        Assert.All(result.Venues, v => Assert.Null(v.FreeCourts));
    }

    [Fact]
    public async Task Default_radius_comes_from_settings_and_custom_radius_narrows()
    {
        var byDefault = await Search(new VenueSearchQuery());
        Assert.Equal(5, byDefault.RadiusKm);
        Assert.DoesNotContain(byDefault.Venues, v => v.Venue.Id == "9");

        var narrow = await Search(new VenueSearchQuery { RadiusKm = 2 });
        Assert.Equal(new[] { "1" }, narrow.Venues.Select(v => v.Venue.Id));
    }

    [Fact]
    public async Task Without_location_radius_is_ignored_and_results_sort_by_name()
    {
        var result = await Search(new VenueSearchQuery { RadiusKm = 2 }, withLocation: false);
        Assert.Null(result.RadiusKm);
        Assert.Equal(new[] { "Cụm sân A", "Cụm sân B", "Cụm sân Ba Vì", "Cụm sân C" }, result.Venues.Select(v => v.Venue.Name));
    }

    [Fact]
    public async Task Device_location_is_used_for_distance()
    {
        var result = await Search(new VenueSearchQuery { Lat = 21.2001, Lng = 105.9001, RadiusKm = 3 });
        Assert.Equal("device", result.Location.Source);
        Assert.Equal(new[] { "9" }, result.Venues.Select(v => v.Venue.Id));
    }

    [Fact]
    public async Task Sheltered_filter_checks_courts_of_the_selected_sport()
    {
        // A có sân cầu lông trong nhà nhưng sân tennis ngoài trời → không đạt "tennis có mái".
        var tennis = await Search(new VenueSearchQuery { Sport = "tennis", Sheltered = true });
        Assert.Equal(new[] { "2", "3" }, tennis.Venues.Select(v => v.Venue.Id));

        var any = await Search(new VenueSearchQuery { Sheltered = true });
        Assert.Contains(any.Venues, v => v.Venue.Id == "1");
    }

    [Theory]
    [InlineData("tay ho", "3")]
    [InlineData("CỤM SÂN B", "2")]
    [InlineData("cau giay", "1")]
    public async Task Text_search_ignores_case_and_vietnamese_accents(string text, string expectedId)
    {
        var result = await Search(new VenueSearchQuery { Q = text });
        Assert.Equal(new[] { expectedId }, result.Venues.Select(v => v.Venue.Id));
    }

    [Fact]
    public async Task Every_requested_amenity_is_required()
    {
        var result = await Search(new VenueSearchQuery { Amenities = "gui_xe, nha_tam" });
        Assert.Equal(new[] { "3" }, result.Venues.Select(v => v.Venue.Id));
    }

    [Fact]
    public async Task Slot_search_counts_free_courts_and_prices_across_peak_boundary()
    {
        // 16:00–18:00 thứ Sáu: 1 giờ giá thường + 1 giờ cao điểm. Sân Tennis 1 của A đã có người đặt.
        var result = await Search(new VenueSearchQuery { Sport = "tennis", Date = Friday, StartHour = 16, Hours = 2 }, busy: [1]);
        var byId = result.Venues.ToDictionary(v => v.Venue.Id);

        Assert.NotNull(result.Slot);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 16, 0, 0, TimeSpan.FromHours(7)), result.Slot.StartAt);
        Assert.Equal(1, byId["1"].FreeCourts);
        Assert.Equal(270000, byId["1"].SlotPrice);
        Assert.Equal(360000, byId["2"].SlotPrice);
        Assert.Equal(550000, byId["3"].SlotPrice);
    }

    [Fact]
    public async Task Venue_with_every_matching_court_busy_is_excluded()
    {
        var result = await Search(new VenueSearchQuery { Sport = "tennis", Date = Friday, StartHour = 19 }, busy: [1, 2]);
        Assert.DoesNotContain(result.Venues, v => v.Venue.Id == "1");
    }

    [Fact]
    public async Task Slot_must_fit_opening_hours_and_have_a_price()
    {
        // 22:00–23:00: chỉ B mở tới 23:00. Ba Vì không có bảng giá nên không bao giờ hiện khi tìm theo giờ.
        var late = await Search(new VenueSearchQuery { Date = Friday, StartHour = 22 }, withLocation: false);
        Assert.Equal(new[] { "2" }, late.Venues.Select(v => v.Venue.Id));

        var day = await Search(new VenueSearchQuery { Date = Friday, StartHour = 10 }, withLocation: false);
        Assert.DoesNotContain(day.Venues, v => v.Venue.Id == "9");
    }

    [Theory]
    [InlineData("golf", null, null, null, null, "INVALID_SPORT")]
    [InlineData(null, 100.0, null, null, null, "INVALID_RADIUS")]
    [InlineData(null, null, "2026-11-20", null, null, "INVALID_SLOT")]
    [InlineData(null, null, null, 19, null, "INVALID_SLOT")]
    [InlineData(null, null, "2026-11-20", 19, 5, "INVALID_SLOT")]
    [InlineData(null, null, "2026-11-20", 19, 4, "INVALID_SLOT")] // Tối đa 3 giờ, khớp với đặt sân.
    [InlineData(null, null, "2026-11-20", 22, 3, "INVALID_SLOT")]
    [InlineData(null, null, "2027-01-30", 19, null, "INVALID_SLOT")]
    [InlineData(null, null, "2026-11-20", 6, null, "SLOT_IN_PAST")]
    public async Task Invalid_queries_are_rejected(string? sport, double? radius, string? date, int? startHour, int? hours, string code)
    {
        var query = new VenueSearchQuery
        {
            Sport = sport, RadiusKm = radius, StartHour = startHour, Hours = hours,
            Date = date is null ? null : DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)
        };
        var error = await Assert.ThrowsAsync<ApiException>(() => Search(query));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal(code, error.Code);
    }

    [Theory]
    [InlineData(16, 2, 270000)]
    [InlineData(20, 2, 270000)] // 20h cao điểm + 21h thường.
    [InlineData(5, 1, 120000)]
    public void Slot_price_sums_each_hour(int startHour, int hours, long expected) =>
        Assert.Equal(expected, SlotPricing.PriceFor(Rules, 1, "tennis", DayOfWeek.Friday, startHour, hours));

    [Fact]
    public void Slot_price_is_null_when_an_hour_has_no_rule() =>
        Assert.Null(SlotPricing.PriceFor(Rules, 1, "tennis", DayOfWeek.Friday, 4, 2)); // 04:00 chưa có giá.

    private static Task<VenueSearchResponse> Search(VenueSearchQuery query, bool withLocation = true, long[]? busy = null)
    {
        var user = new User
        {
            Id = 4, Name = "Người chơi An", Email = "an@datsan.vn", Role = "player", Status = "active",
            Address = "Cầu Giấy, Hà Nội", Lat = withLocation ? 21.0362 : null, Lng = withLocation ? 105.7906 : null
        };
        var service = new VenueSearchService(new FakeUsers(user), new FakeVenues(), new FakeBookings(busy ?? []),
            new FakeSettings(), new FixedClock(Now));
        return service.SearchAsync(4, query, default);
    }

    private static VenueSummary Venue(long id, string name, string address, double lat, double lng,
        string open, string close, string amenities, bool vip = false)
    {
        var courts = Courts.Where(c => c.VenueId == id).ToList();
        return new VenueSummary(id, name, address, lat, lng, TimeOnly.Parse(open), TimeOnly.Parse(close), amenities, "[]",
            courts.Select(c => c.Sport).Distinct().ToList(), courts.Select(c => c.Environment).Distinct().ToList(), 50000, vip);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeUsers(User user) : IUserRepository
    {
        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(user);
        public Task<User?> FindByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult<User?>(user);
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(User newUser, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeVenues : IVenueRepository
    {
        public Task<IReadOnlyList<VenueSummary>> GetApprovedSummariesAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VenueSummary>>([C, Far, B, A]);
        public Task<IReadOnlyList<CourtInfo>> GetActiveCourtsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CourtInfo>>(Courts);
        public Task<IReadOnlyList<PriceRuleInfo>> GetPriceRulesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PriceRuleInfo>>(Rules);
    }

    private sealed class FakeBookings(long[] busy) : IBookingRepository
    {
        public Task<UpcomingBooking?> GetNextUpcomingAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<UpcomingBooking?>(null);
        public Task<IReadOnlySet<long>> GetBusyCourtIdsAsync(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlySet<long>>(busy.ToHashSet());
    }

    private sealed class FakeSettings : ISettingRepository
    {
        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(key == "default_radius_km" ? "5" : null);
    }
}
