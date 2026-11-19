using System.Globalization;
using System.Text.Json;
using DatSan.Api.DTOs;
using DatSan.Api.Middleware;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Interface;

namespace DatSan.Api.Services.Implements;

public sealed class OwnerVenueService(IOwnerVenueRepository venues, TimeProvider clock) : IOwnerVenueService
{
    public async Task<IReadOnlyList<OwnerVenueListItemDto>> ListAsync(long ownerId, CancellationToken cancellationToken)
    {
        var owned = await venues.GetOwnedAsync(ownerId, cancellationToken);
        var vip = await venues.GetVipVenueIdsAsync(owned.Select(v => v.Id), clock.GetUtcNow(), cancellationToken);
        return owned.Select(v => new OwnerVenueListItemDto(
            Id(v.Id), v.Name, v.Address, v.Status, v.RejectReason, vip.Contains(v.Id),
            v.Courts.Where(c => c.IsActive).Select(c => c.Sport).Distinct()
                .OrderBy(s => Array.IndexOf(OwnerVenueRules.Sports, s)).ToList(),
            v.Courts.Count(c => c.IsActive), v.Courts.Count, v.PriceRules.Count > 0,
            OwnerVenueRules.FormatTime(v.OpenTime), OwnerVenueRules.FormatTime(v.CloseTime), v.CreatedAt)).ToList();
    }

    public async Task<OwnerVenueDetailDto> GetAsync(long ownerId, long venueId, CancellationToken cancellationToken)
    {
        var venue = await venues.GetOwnedForEditAsync(venueId, ownerId, cancellationToken) ?? throw NotFound();
        var vip = await venues.GetVipVenueIdsAsync([venue.Id], clock.GetUtcNow(), cancellationToken);
        return ToDetail(venue, vip.Contains(venue.Id));
    }

    public async Task<OwnerVenueSaveResult> CreateAsync(long ownerId, OwnerVenueInput input, CancellationToken cancellationToken)
    {
        var valid = OwnerVenueRules.Validate(input, []);
        var venue = new Venue { OwnerId = ownerId, Status = "pending" }; // UC04: luôn chờ admin duyệt.
        Apply(venue, valid);
        venue.Courts.AddRange(valid.Courts.Select(c => new Court { Sport = c.Sport!, Name = c.Name!, Environment = c.Environment!, IsActive = c.IsActive }));
        venue.PriceRules.AddRange(ToRules(valid));
        await venues.AddAsync(venue, cancellationToken);
        return new OwnerVenueSaveResult(ToDetail(venue, false), true);
    }

    public async Task<OwnerVenueSaveResult> UpdateAsync(long ownerId, long venueId, OwnerVenueInput input, CancellationToken cancellationToken)
    {
        var venue = await venues.GetOwnedForEditAsync(venueId, ownerId, cancellationToken) ?? throw NotFound();
        if (venue.Status == "deleted")
            throw new ApiException(409, "VENUE_DELETED", "Cụm sân đã bị ẩn nên không sửa được. Vui lòng liên hệ quản trị viên.");
        var valid = OwnerVenueRules.Validate(input, venue.Courts);
        var nextStatus = OwnerVenueRules.NextStatus(venue.Status, OwnerVenueRules.IdentityChanged(venue, valid));
        var reviewRequired = nextStatus == "pending" && venue.Status != "pending";

        Apply(venue, valid);
        venue.Status = nextStatus;
        if (nextStatus == "pending") venue.RejectReason = null;

        foreach (var court in valid.Courts)
        {
            var existing = court.Id is null ? null : venue.Courts.Single(c => Id(c.Id) == court.Id);
            if (existing is null)
            {
                venue.Courts.Add(new Court { Sport = court.Sport!, Name = court.Name!, Environment = court.Environment!, IsActive = court.IsActive });
                continue;
            }
            existing.Name = court.Name!;
            existing.Environment = court.Environment!;
            existing.IsActive = court.IsActive;
        }
        // Sân con cũ không còn trong danh sách: ngừng hoạt động, không xóa (giữ lịch sử booking).
        foreach (var court in venue.Courts.Where(c => c.Id != 0 && !valid.Courts.Any(v => v.Id == Id(c.Id))))
            court.IsActive = false;

        await venues.SaveAsync(venue, ToRules(valid).Select(r => { r.VenueId = venue.Id; return r; }).ToList(), cancellationToken);
        var vip = await venues.GetVipVenueIdsAsync([venue.Id], clock.GetUtcNow(), cancellationToken);
        return new OwnerVenueSaveResult(ToDetail(venue, vip.Contains(venue.Id)), reviewRequired);
    }

    private static void Apply(Venue venue, ValidVenue valid)
    {
        venue.Name = valid.Name;
        venue.Address = valid.Address;
        venue.Lat = valid.Lat;
        venue.Lng = valid.Lng;
        venue.OpenTime = valid.OpenTime;
        venue.CloseTime = valid.CloseTime;
        venue.Amenities = JsonSerializer.Serialize(valid.Amenities);
        venue.Description = valid.Description;
    }

    private static IEnumerable<PriceRule> ToRules(ValidVenue valid) => valid.Prices.Select(p => new PriceRule
    {
        Sport = p.Sport!, Weekday = (short)p.Weekday, StartHour = (short)p.StartHour, EndHour = (short)p.EndHour, PricePerHour = p.PricePerHour,
    });

    private static OwnerVenueDetailDto ToDetail(Venue v, bool isVip) => new(
        Id(v.Id), v.Name, v.Address, v.Lat, v.Lng,
        OwnerVenueRules.FormatTime(v.OpenTime), OwnerVenueRules.FormatTime(v.CloseTime),
        VenueCards.ReadStrings(v.Amenities), v.Description, v.Status, v.RejectReason, isVip,
        v.Courts.OrderBy(c => c.Id == 0 ? long.MaxValue : c.Id)
            .Select(c => new OwnerCourtDto(Id(c.Id), c.Sport, c.Name, c.Environment, c.IsActive)).ToList(),
        v.PriceRules.OrderBy(p => Array.IndexOf(OwnerVenueRules.Sports, p.Sport)).ThenBy(p => p.Weekday).ThenBy(p => p.StartHour)
            .Select(p => new OwnerPriceDto(p.Sport, p.Weekday, p.StartHour, p.EndHour, p.PricePerHour)).ToList(),
        v.CreatedAt);

    private static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);

    private static ApiException NotFound() => new(404, "VENUE_NOT_FOUND", "Không tìm thấy cụm sân của bạn.");
}
