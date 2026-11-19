using DatSan.Api.Data;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DatSan.Api.Repositories.Implements;

public sealed class OwnerVenueRepository(AppDbContext db) : IOwnerVenueRepository
{
    public async Task<IReadOnlyList<Venue>> GetOwnedAsync(long ownerId, CancellationToken cancellationToken) =>
        await db.Venues.AsNoTracking()
            .Where(v => v.OwnerId == ownerId)
            .Include(v => v.Courts)
            .Include(v => v.PriceRules)
            .AsSplitQuery()
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Venue?> GetOwnedForEditAsync(long venueId, long ownerId, CancellationToken cancellationToken) =>
        db.Venues
            .Where(v => v.Id == venueId && v.OwnerId == ownerId)
            .Include(v => v.Courts)
            .Include(v => v.PriceRules)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlySet<long>> GetVipVenueIdsAsync(
        IEnumerable<long> venueIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = venueIds.ToList();
        return (await db.VipSubscriptions.AsNoTracking()
            .Where(s => ids.Contains(s.VenueId) && s.Status == "active" && s.StartAt <= now && s.EndAt > now)
            .Select(s => s.VenueId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();
    }

    public async Task AddAsync(Venue venue, CancellationToken cancellationToken)
    {
        db.Venues.Add(venue);
        await db.SaveChangesAsync(cancellationToken);
    }

    // Xóa bảng giá cũ và thêm bảng giá mới ở hai lần lưu riêng trong cùng transaction:
    // trigger trg_price_rules_no_overlap sẽ báo chồng giờ nếu dòng mới được thêm trước khi dòng cũ bị xóa.
    public async Task SaveAsync(Venue venue, IReadOnlyList<PriceRule> newPrices, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PriceRules.RemoveRange(venue.PriceRules);
        await db.SaveChangesAsync(cancellationToken);
        venue.PriceRules.Clear();
        venue.PriceRules.AddRange(newPrices);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
