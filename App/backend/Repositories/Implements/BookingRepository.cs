using DatSan.Api.Data;
using DatSan.Api.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DatSan.Api.Repositories.Implements;

public sealed class BookingRepository(AppDbContext db) : IBookingRepository
{
    public Task<UpcomingBooking?> GetNextUpcomingAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.EndAt > now
                && (b.Status == "confirmed" || (b.Status == "pending" && b.HoldExpiresAt > now)))
            .OrderBy(b => b.StartAt)
            .Select(b => new UpcomingBooking(
                b.Id, b.StartAt, b.EndAt, b.Status, b.Price, b.HoldExpiresAt,
                b.Court!.Name, b.Court.Sport, b.Court.Environment,
                b.Court.VenueId, b.Court.Venue!.Name, b.Court.Venue.Address))
            .FirstOrDefaultAsync(cancellationToken);

    // Cùng điều kiện chồng giờ với trigger trg_bookings_no_overlap: [start, end) giao nhau.
    public async Task<IReadOnlySet<long>> GetBusyCourtIdsAsync(
        DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, CancellationToken cancellationToken) =>
        (await db.Bookings.AsNoTracking()
            .Where(b => b.StartAt < end && start < b.EndAt
                && (b.Status == "confirmed" || (b.Status == "pending" && b.HoldExpiresAt > now)))
            .Select(b => b.CourtId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();
}
