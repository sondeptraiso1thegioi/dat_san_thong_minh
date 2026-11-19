using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Implements;
using Xunit;

namespace DatSan.Api.Tests;

public sealed class PlayerHomeServiceTests
{
    // 2026-11-19 23:30 UTC = 06:30 ngày 20/11 giờ Việt Nam.
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 23, 30, 0, TimeSpan.Zero);

    // Toạ độ lấy từ dữ liệu demo: người chơi An ở Cầu Giấy; A ≈ 1,2 km, B ≈ 2,5 km, C ≈ 4,8 km (VIP), Far ≈ 21 km (VIP).
    private static readonly VenueSummary A = Venue(1, "Cụm sân A", 21.0450, 105.7980, environments: ["outdoor"]);
    private static readonly VenueSummary B = Venue(2, "Cụm sân B", 21.0340, 105.8150, environments: ["covered", "indoor"]);
    private static readonly VenueSummary C = Venue(3, "Cụm sân C", 21.0700, 105.8200, vip: true);
    private static readonly VenueSummary Far = Venue(9, "Cụm sân Ba Vì", 21.2000, 105.9000, vip: true);

    [Fact]
    public async Task Profile_location_sorts_by_distance_and_features_vip_within_radius()
    {
        var home = await Service(Player(withLocation: true), [Far, C, B, A]).GetHomeAsync(4, null, null, default);

        Assert.Equal("profile", home.Location.Source);
        Assert.Equal("Cầu Giấy, Hà Nội", home.Location.Address);
        Assert.Equal(new[] { "1", "2", "3", "9" }, home.Nearby.Select(v => v.Id));
        Assert.Equal(1.2, home.Nearby[0].DistanceKm);
        Assert.Equal(new[] { "3" }, home.Featured.Select(v => v.Id)); // Far nằm ngoài bán kính 5 km.
        Assert.Equal(5, home.RadiusKm);
    }

    [Fact]
    public async Task Device_location_overrides_profile()
    {
        // Đứng gần sân Ba Vì: sân này lên đầu và trở thành sân nổi bật.
        var home = await Service(Player(withLocation: true), [A, B, C, Far]).GetHomeAsync(4, 21.2001, 105.9001, default);

        Assert.Equal("device", home.Location.Source);
        Assert.Equal("9", home.Nearby[0].Id);
        Assert.Equal(new[] { "9" }, home.Featured.Select(v => v.Id));
    }

    [Fact]
    public async Task Without_location_sorts_by_name_and_features_every_vip()
    {
        var home = await Service(Player(withLocation: false), [C, Far, B, A]).GetHomeAsync(4, null, null, default);

        Assert.Equal("none", home.Location.Source);
        Assert.All(home.Nearby, v => Assert.Null(v.DistanceKm));
        Assert.Equal(new[] { "Cụm sân A", "Cụm sân B", "Cụm sân Ba Vì", "Cụm sân C" }, home.Nearby.Select(v => v.Name));
        Assert.Equal(new[] { "9", "3" }, home.Featured.Select(v => v.Id));
    }

    [Fact]
    public async Task Sections_are_limited_to_six_venues()
    {
        var many = Enumerable.Range(1, 10).Select(i => Venue(i, $"Sân {i:00}", 21.04 + i / 1000.0, 105.79, vip: true)).ToList();
        var home = await Service(Player(withLocation: true), many).GetHomeAsync(4, null, null, default);

        Assert.Equal(PlayerHomeService.SectionSize, home.Nearby.Count);
        Assert.Equal(PlayerHomeService.SectionSize, home.Featured.Count);
    }

    [Fact]
    public async Task Card_reads_json_columns_shelter_and_opening_hours_in_vietnam_time()
    {
        var early = Venue(5, "Mở sớm", 21.04, 105.79, open: new TimeOnly(6, 0),
            amenities: """["gui_xe","nuoc_uong"]""", photos: """["https://img/1.jpg","https://img/2.jpg"]""");
        var late = Venue(6, "Mở muộn", 21.04, 105.79, open: new TimeOnly(7, 0), amenities: "not json", photos: "[]");
        var home = await Service(Player(withLocation: false), [early, late, B]).GetHomeAsync(4, null, null, default);
        var cards = home.Nearby.ToDictionary(v => v.Id);

        Assert.True(cards["5"].IsOpenNow);   // 06:30 nằm trong 06:00–22:00.
        Assert.False(cards["6"].IsOpenNow);  // Chưa tới 07:00.
        Assert.Equal("06:00", cards["5"].OpenTime);
        Assert.Equal(new[] { "gui_xe", "nuoc_uong" }, cards["5"].Amenities);
        Assert.Equal("https://img/1.jpg", cards["5"].PhotoUrl);
        Assert.Empty(cards["6"].Amenities);  // JSON hỏng không làm hỏng trang chủ.
        Assert.Null(cards["6"].PhotoUrl);
        Assert.False(cards["5"].HasShelter); // Chỉ có sân ngoài trời.
        Assert.True(cards["2"].HasShelter);
    }

    [Fact]
    public async Task Upcoming_booking_is_returned_with_string_ids()
    {
        var booking = new UpcomingBooking(9007199254740993, Now.AddDays(1), Now.AddDays(1).AddHours(1), "confirmed",
            150000, null, "Tennis 1", "tennis", "outdoor", 1, "Cụm sân A", "Cầu Giấy, Hà Nội");
        var home = await Service(Player(withLocation: true), [A], booking).GetHomeAsync(4, null, null, default);

        Assert.NotNull(home.Upcoming);
        Assert.Equal("9007199254740993", home.Upcoming.Id);
        Assert.Equal("1", home.Upcoming.VenueId);
        Assert.Equal("Tennis 1", home.Upcoming.CourtName);
    }

    [Theory]
    [InlineData("8", 8)]
    [InlineData(" 3.5 ", 3.5)]
    [InlineData("0", 5)]
    [InlineData("-2", 5)]
    [InlineData("abc", 5)]
    [InlineData(null, 5)]
    public void Radius_setting_falls_back_to_five_km(string? value, double expected) =>
        Assert.Equal(expected, PlayerHomeService.ParseRadius(value));

    [Fact]
    public async Task Missing_user_is_unauthorized()
    {
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(null, [A]).GetHomeAsync(4, null, null, default));
        Assert.Equal(401, error.StatusCode);
    }

    private static User Player(bool withLocation) => new()
    {
        Id = 4, Name = "Người chơi An", Email = "an@datsan.vn", Role = "player", Status = "active",
        Address = "Cầu Giấy, Hà Nội", Lat = withLocation ? 21.0362 : null, Lng = withLocation ? 105.7906 : null
    };

    private static VenueSummary Venue(long id, string name, double lat, double lng, bool vip = false,
        string[]? environments = null, TimeOnly? open = null, string amenities = "[]", string photos = "[]") =>
        new(id, name, "Hà Nội", lat, lng, open ?? new TimeOnly(6, 0), new TimeOnly(22, 0), amenities, photos,
            ["tennis"], environments ?? ["outdoor"], 120000, vip);

    private static PlayerHomeService Service(User? user, IReadOnlyList<VenueSummary> venues, UpcomingBooking? upcoming = null) =>
        new(new FakeUsers(user), new FakeVenues(venues), new FakeBookings(upcoming), new FakeSettings("5"), new FixedClock(Now));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeUsers(User? user) : IUserRepository
    {
        public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(user);
        public Task<User?> FindByIdAsync(long id, CancellationToken cancellationToken) => Task.FromResult(user);
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task AddAsync(User newUser, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeVenues(IReadOnlyList<VenueSummary> venues) : IVenueRepository
    {
        public Task<IReadOnlyList<VenueSummary>> GetApprovedSummariesAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(venues);
        public Task<IReadOnlyList<CourtInfo>> GetActiveCourtsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CourtInfo>>([]);
        public Task<IReadOnlyList<PriceRuleInfo>> GetPriceRulesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PriceRuleInfo>>([]);
    }

    private sealed class FakeBookings(UpcomingBooking? upcoming) : IBookingRepository
    {
        public Task<UpcomingBooking?> GetNextUpcomingAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(upcoming);
        public Task<IReadOnlySet<long>> GetBusyCourtIdsAsync(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlySet<long>>(new HashSet<long>());
    }

    private sealed class FakeSettings(string? radius) : ISettingRepository
    {
        public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(key == "default_radius_km" ? radius : null);
    }
}
