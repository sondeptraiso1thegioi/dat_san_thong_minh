using DatSan.Api.Data;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DatSan.Api.Repositories.Implements;

public sealed class AdminVenueRepository(AppDbContext db) : IAdminVenueRepository
{
    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken cancellationToken) =>
        await db.Venues.AsNoTracking()
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

    public async Task<IReadOnlyList<Venue>> ListAsync(string status, CancellationToken cancellationToken) =>
        await db.Venues.AsNoTracking()
            .Where(v => v.Status == status)
            .Include(v => v.Owner)
            .Include(v => v.Courts)
            .AsSplitQuery()
            .OrderBy(v => v.CreatedAt) // Chờ lâu nhất lên trước.
            .ToListAsync(cancellationToken);

    public Task<Venue?> GetAsync(long venueId, CancellationToken cancellationToken) =>
        db.Venues
            .Where(v => v.Id == venueId)
            .Include(v => v.Owner)
            .Include(v => v.Courts)
            .Include(v => v.PriceRules)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Booking>> GetUpcomingBookingsAsync(long venueId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.Bookings
            .Where(b => b.Court!.VenueId == venueId && b.StartAt > now && (b.Status == "pending" || b.Status == "confirmed"))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> GetPaidPaymentsAsync(IReadOnlyCollection<long> bookingIds, CancellationToken cancellationToken)
    {
        if (bookingIds.Count == 0) return [];
        var ids = bookingIds.ToList();
        return await db.Payments
            .Where(p => p.Type == "booking" && p.BookingId != null && ids.Contains(p.BookingId.Value) && p.Status == "paid")
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
