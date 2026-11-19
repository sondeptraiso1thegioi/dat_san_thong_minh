using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Implements;
using Xunit;

namespace DatSan.Api.Tests;

public sealed class OwnerVenueServiceTests
{
    private const long Owner = 2;

    [Fact]
    public async Task Create_saves_pending_venue_with_courts_and_prices()
    {
        var repository = new FakeRepository();
        var result = await new OwnerVenueService(repository, TimeProvider.System).CreateAsync(Owner, Input(), default);

        var saved = Assert.Single(repository.Venues);
        Assert.Equal("pending", saved.Status);
        Assert.Equal(Owner, saved.OwnerId);
        Assert.Equal("Sân Mới Cầu Giấy", saved.Name); // Khoảng trắng thừa được gộp.
        Assert.Equal("""["gui_xe","nha_tam"]""", saved.Amenities);
        Assert.Equal(2, saved.Courts.Count);
        Assert.Equal(6, saved.PriceRules.Count);
        Assert.True(result.ReviewRequired);
        Assert.Equal("24:00", result.Venue.CloseTime);
        Assert.Equal(OwnerVenueRules.Midnight, saved.CloseTime);
    }

    [Theory]
    [InlineData("pending", false, "pending")]
    [InlineData("pending", true, "pending")]
    [InlineData("rejected", false, "pending")] // Sửa sân bị từ chối = gửi lại.
    [InlineData("approved", false, "approved")]
    [InlineData("approved", true, "pending")]
    public void Status_after_edit(string current, bool identityChanged, string expected) =>
        Assert.Equal(expected, OwnerVenueRules.NextStatus(current, identityChanged));

    [Fact]
    public async Task Editing_price_of_approved_venue_keeps_it_approved()
    {
        var repository = new FakeRepository();
        var venue = repository.Seed(Approved());
        var input = Input(name: venue.Name, address: venue.Address, lat: venue.Lat, lng: venue.Lng, courtIds: venue.Courts, price: 99000);
        var result = await Service(repository).UpdateAsync(Owner, venue.Id, input, default);

        Assert.Equal("approved", venue.Status);
        Assert.False(result.ReviewRequired);
        Assert.All(venue.PriceRules, p => Assert.Equal(99000, p.PricePerHour));
    }

    [Fact]
    public async Task Renaming_approved_venue_sends_it_back_for_review()
    {
        var repository = new FakeRepository();
        var venue = repository.Seed(Approved());
        var result = await Service(repository).UpdateAsync(Owner, venue.Id,
            Input(name: "Tên mới hoàn toàn", address: venue.Address, lat: venue.Lat, lng: venue.Lng, courtIds: venue.Courts), default);

        Assert.Equal("pending", venue.Status);
        Assert.True(result.ReviewRequired);
    }

    [Fact]
    public async Task Resubmitting_rejected_venue_clears_reason()
    {
        var repository = new FakeRepository();
        var rejected = Approved();
        rejected.Status = "rejected";
        rejected.RejectReason = "Sai địa chỉ";
        var venue = repository.Seed(rejected);
        await Service(repository).UpdateAsync(Owner, venue.Id,
            Input(name: venue.Name, address: "Số 1 Trần Duy Hưng, Cầu Giấy", lat: venue.Lat, lng: venue.Lng, courtIds: venue.Courts), default);

        Assert.Equal("pending", venue.Status);
        Assert.Null(venue.RejectReason);
    }

    [Fact]
    public async Task Omitted_court_is_deactivated_not_deleted_and_new_court_is_added()
    {
        var repository = new FakeRepository();
        var venue = repository.Seed(Approved());
        var keep = venue.Courts[0];
        var input = Input(name: venue.Name, address: venue.Address, lat: venue.Lat, lng: venue.Lng,
            courts: [Court(keep.Id.ToString(), "badminton", "Cầu lông 1"), Court(null, "badminton", "Cầu lông 3")],
            prices: Prices("badminton", 80000));
        await Service(repository).UpdateAsync(Owner, venue.Id, input, default);

        Assert.Equal(3, venue.Courts.Count);
        Assert.False(venue.Courts.Single(c => c.Name == "Pickleball 1").IsActive);
        Assert.True(venue.Courts.Single(c => c.Name == "Cầu lông 3").IsActive);
    }

    [Fact]
    public async Task Other_owners_venue_is_not_found()
    {
        var repository = new FakeRepository();
        var venue = repository.Seed(Approved());
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(repository).UpdateAsync(99, venue.Id, Input(), default));
        Assert.Equal(404, error.StatusCode);
        await Assert.ThrowsAsync<ApiException>(() => Service(repository).GetAsync(99, venue.Id, default));
    }

    [Fact]
    public async Task Deleted_venue_cannot_be_edited()
    {
        var repository = new FakeRepository();
        var deleted = Approved();
        deleted.Status = "deleted";
        var venue = repository.Seed(deleted);
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(repository).UpdateAsync(Owner, venue.Id, Input(), default));
        Assert.Equal("VENUE_DELETED", error.Code);
    }

    [Fact]
    public async Task Changing_sport_of_existing_court_is_rejected()
    {
        var repository = new FakeRepository();
        var venue = repository.Seed(Approved());
        var input = Input(courts: [Court(venue.Courts[0].Id.ToString(), "tennis", "Cầu lông 1")], prices: Prices("tennis", 100000));
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(repository).UpdateAsync(Owner, venue.Id, input, default));
        Assert.Contains("Không đổi được môn", error.Message);
    }

    [Fact]
    public async Task New_court_cannot_reuse_name_of_hidden_existing_court()
    {
        var repository = new FakeRepository();
        var venue = repository.Seed(Approved());
        // "Pickleball 1" không gửi lên (sẽ ngừng hoạt động) nhưng vẫn còn trong DB, nên tên vẫn bị chiếm.
        var input = Input(courts: [Court(venue.Courts[0].Id.ToString(), "badminton", "Cầu lông 1"), Court(null, "badminton", "pickleball 1")],
            prices: Prices("badminton", 80000));
        var error = await Assert.ThrowsAsync<ApiException>(() => Service(repository).UpdateAsync(Owner, venue.Id, input, default));
        Assert.Contains("bị trùng", error.Message);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("location")]
    [InlineData("hours")]
    [InlineData("time-format")]
    [InlineData("amenity")]
    [InlineData("no-court")]
    [InlineData("overlap")]
    [InlineData("price-step")]
    [InlineData("missing-price")]
    [InlineData("price-for-missing-sport")]
    public void Invalid_input_is_rejected_with_vietnamese_message(string problem)
    {
        var input = problem switch
        {
            "name" => Input(name: "  A "),
            "location" => Input(lat: 0, lng: 0),
            "hours" => Input(open: "22:00", close: "06:00"),
            "time-format" => Input(open: "06:15"),
            "amenity" => Input(amenities: ["wifi"]),
            "no-court" => Input(courts: [new OwnerCourtInput { Sport = "badminton", Name = "Cầu lông 1", Environment = "indoor", IsActive = false }]),
            "overlap" => Input(prices: [Price("badminton", 1, 6, 18, 80000), Price("badminton", 1, 17, 22, 120000), .. Prices("pickleball", 100000)]),
            "price-step" => Input(prices: [.. Prices("badminton", 80500), .. Prices("pickleball", 100000)]),
            "missing-price" => Input(prices: Prices("badminton", 80000)),
            "price-for-missing-sport" => Input(prices: [.. Prices("badminton", 80000), .. Prices("pickleball", 100000), Price("tennis", 0, 6, 8, 100000)]),
            _ => throw new ArgumentOutOfRangeException(nameof(problem)),
        };
        var error = Assert.Throws<ApiException>(() => OwnerVenueRules.Validate(input, []));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("VALIDATION_ERROR", error.Code);
    }

    [Fact]
    public void Missing_fields_get_vietnamese_messages_not_crashes()
    {
        var error = Assert.Throws<ApiException>(() => OwnerVenueRules.Validate(new OwnerVenueInput(), []));
        Assert.Equal("Tên cụm sân từ 3 đến 150 ký tự.", error.Message);
    }

    [Fact]
    public void Adjacent_price_bands_do_not_overlap()
    {
        var valid = OwnerVenueRules.Validate(Input(prices: [Price("badminton", 1, 6, 17, 80000), Price("badminton", 1, 17, 21, 120000),
            Price("badminton", 1, 21, 24, 80000), .. Prices("pickleball", 100000)]), []);
        Assert.Equal(6, valid.Prices.Count); // 3 khung cầu lông + 3 dòng pickleball.
    }

    // ---- dữ liệu thử ----

    private static OwnerVenueService Service(FakeRepository repository) => new(repository, TimeProvider.System);

    private static OwnerVenueInput Input(string name = "  Sân Mới   Cầu Giấy ", string address = "Số 1 Xuân Thủy, Cầu Giấy",
        double lat = 21.0362, double lng = 105.7906, string open = "06:00", string close = "24:00", string[]? amenities = null,
        OwnerCourtInput[]? courts = null, OwnerPriceInput[]? prices = null, List<Court>? courtIds = null, long? price = null) => new()
    {
        Name = name, Address = address, Lat = lat, Lng = lng, OpenTime = open, CloseTime = close,
        Amenities = (amenities ?? ["gui_xe", "nha_tam", "gui_xe"]).ToList(),
        Courts = (courts ?? (courtIds is null
            ? [Court(null, "badminton", "Cầu lông 1"), Court(null, "pickleball", "Pickleball 1")]
            : courtIds.Select(c => Court(c.Id.ToString(), c.Sport, c.Name)).ToArray())).ToList(),
        Prices = (prices ?? [.. Prices("badminton", price ?? 80000), .. Prices("pickleball", price ?? 100000)]).ToList(),
    };

    private static OwnerCourtInput Court(string? id, string sport, string name) =>
        new() { Id = id, Sport = sport, Name = name, Environment = "indoor", IsActive = true };

    private static OwnerPriceInput Price(string sport, int weekday, int start, int end, long amount) =>
        new() { Sport = sport, Weekday = weekday, StartHour = start, EndHour = end, PricePerHour = amount };

    /// <summary>Thứ 2, 4, 6 từ 6:00 đến 24:00.</summary>
    private static OwnerPriceInput[] Prices(string sport, long amount) =>
        [Price(sport, 1, 6, 24, amount), Price(sport, 3, 6, 24, amount), Price(sport, 5, 6, 24, amount)];

    private static Venue Approved() => new()
    {
        OwnerId = Owner, Name = "Cụm sân Xuân Thủy", Address = "Số 1 Xuân Thủy, Cầu Giấy", Lat = 21.0362, Lng = 105.7906,
        OpenTime = new TimeOnly(6, 0), CloseTime = new TimeOnly(22, 0), Status = "approved",
        Courts = [new Court { Sport = "badminton", Name = "Cầu lông 1", Environment = "indoor", IsActive = true },
                  new Court { Sport = "pickleball", Name = "Pickleball 1", Environment = "covered", IsActive = true }],
        PriceRules = [new PriceRule { Sport = "badminton", Weekday = 1, StartHour = 6, EndHour = 22, PricePerHour = 80000 },
                      new PriceRule { Sport = "pickleball", Weekday = 1, StartHour = 6, EndHour = 22, PricePerHour = 100000 }],
    };

    /// <summary>Kho cụm sân trong bộ nhớ; gán id như database khi lưu.</summary>
    private sealed class FakeRepository : IOwnerVenueRepository
    {
        private long nextId = 100;
        public List<Venue> Venues { get; } = [];

        public Venue Seed(Venue venue)
        {
            AssignIds(venue);
            Venues.Add(venue);
            return venue;
        }

        public Task<IReadOnlyList<Venue>> GetOwnedAsync(long ownerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Venue>>(Venues.Where(v => v.OwnerId == ownerId).ToList());

        public Task<Venue?> GetOwnedForEditAsync(long venueId, long ownerId, CancellationToken cancellationToken) =>
            Task.FromResult(Venues.SingleOrDefault(v => v.Id == venueId && v.OwnerId == ownerId));

        public Task<IReadOnlySet<long>> GetVipVenueIdsAsync(IEnumerable<long> venueIds, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<long>>(new HashSet<long>());

        public Task AddAsync(Venue venue, CancellationToken cancellationToken)
        {
            Seed(venue);
            return Task.CompletedTask;
        }

        public Task SaveAsync(Venue venue, IReadOnlyList<PriceRule> newPrices, CancellationToken cancellationToken)
        {
            venue.PriceRules = newPrices.ToList();
            AssignIds(venue);
            return Task.CompletedTask;
        }

        private void AssignIds(Venue venue)
        {
            if (venue.Id == 0) venue.Id = nextId++;
            foreach (var court in venue.Courts.Where(c => c.Id == 0)) { court.Id = nextId++; court.VenueId = venue.Id; }
            foreach (var rule in venue.PriceRules.Where(r => r.Id == 0)) { rule.Id = nextId++; rule.VenueId = venue.Id; }
        }
    }
}
