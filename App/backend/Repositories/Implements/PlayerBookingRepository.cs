using System.Data;
using DatSan.Api.Data;
using DatSan.Api.Models;
using DatSan.Api.Repositories.Interface;
using DatSan.Api.Services.Implements;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DatSan.Api.Repositories.Implements;

public sealed class PlayerBookingRepository(AppDbContext db) : IPlayerBookingRepository
{
    // Mã lỗi THROW trong trigger trg_bookings_no_overlap và mã deadlock của SQL Server.
    private const int BookingOverlapError = 51001;
    private const int DeadlockError = 1205;

    public Task<Venue?> GetApprovedVenueAsync(long venueId, CancellationToken cancellationToken) =>
        db.Venues.AsNoTracking()
            .Where(v => v.Id == venueId && v.Status == "approved")
            .Include(v => v.Courts.Where(c => c.IsActive))
            .Include(v => v.PriceRules)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<Venue?> GetVenueOfActiveCourtAsync(long courtId, CancellationToken cancellationToken)
    {
        var venueId = await db.Courts.AsNoTracking()
            .Where(c => c.Id == courtId && c.IsActive && c.Venue!.Status == "approved")
            .Select(c => (long?)c.VenueId)
            .SingleOrDefaultAsync(cancellationToken);
        return venueId is long id ? await GetApprovedVenueAsync(id, cancellationToken) : null;
    }

    public Task<bool> IsVipAsync(long venueId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.VipSubscriptions.AsNoTracking()
            .AnyAsync(s => s.VenueId == venueId && s.Status == "active" && s.StartAt <= now && s.EndAt > now, cancellationToken);

    public async Task<IReadOnlyList<BusyInterval>> GetBusyAsync(
        IReadOnlyCollection<long> courtIds, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ids = courtIds.ToList();
        return await db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.CourtId) && b.StartAt < to && from < b.EndAt
                && (b.Status == "confirmed" || (b.Status == "pending" && b.HoldExpiresAt > now)))
            .Select(b => new BusyInterval(b.CourtId, b.StartAt, b.EndAt))
            .ToListAsync(cancellationToken);
    }

    public Task<long?> GetActiveHoldIdAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Bookings.AsNoTracking()
            .Where(b => b.UserId == userId && b.Status == "pending" && b.HoldExpiresAt > now)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => (long?)b.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<HoldResult> CreateHoldAsync(Booking booking, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Khóa dòng sân con tới hết transaction: hai người cùng đặt một sân con sẽ đi lần lượt,
            // nên bước kiểm tra bên dưới đáng tin và trigger không bị deadlock.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM dbo.courts WITH (UPDLOCK, HOLDLOCK) WHERE id = {booking.CourtId}", cancellationToken);

            // Trigger chống chồng giờ coi mọi booking 'pending' là đang chiếm sân, kể cả khi đã quá hạn giữ chỗ.
            await db.Bookings
                .Where(b => b.CourtId == booking.CourtId && b.Status == "pending"
                    && (b.HoldExpiresAt == null || b.HoldExpiresAt <= now))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(b => b.Status, "cancelled")
                    .SetProperty(b => b.CancelReason, BookingRules.ExpiredHoldReason), cancellationToken);

            var taken = await db.Bookings.AnyAsync(b => b.CourtId == booking.CourtId
                && b.StartAt < booking.EndAt && booking.StartAt < b.EndAt
                && (b.Status == "confirmed" || b.Status == "pending"), cancellationToken);
            if (taken) return HoldResult.SlotTaken; // Transaction tự rollback khi dispose.

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return HoldResult.Created;
        }
        catch (Exception error) when (SqlNumber(error) is BookingOverlapError or DeadlockError)
        {
            db.Entry(booking).State = EntityState.Detached;
            return SqlNumber(error) == DeadlockError ? HoldResult.Busy : HoldResult.SlotTaken;
        }
    }

    private static int? SqlNumber(Exception error) =>
        (error as SqlException ?? error.InnerException as SqlException)?.Number;

    public Task<BookingView?> GetViewAsync(long bookingId, CancellationToken cancellationToken) =>
        db.Bookings.AsNoTracking()
            .Where(b => b.Id == bookingId)
            .Select(b => new BookingView(
                b.Id, b.UserId, b.Status, b.Court!.VenueId, b.Court.Venue!.Name, b.Court.Venue.Address,
                b.Court.Venue.Lat, b.Court.Venue.Lng, b.Court.Name, b.Court.Sport, b.Court.Environment,
                b.StartAt, b.EndAt, b.Price, b.HoldExpiresAt, b.CancelReason, b.CreatedAt,
                db.Payments.Where(p => p.Type == "booking" && p.BookingId == b.Id)
                    .OrderByDescending(p => p.Status == "paid" || p.Status == "refund_pending" || p.Status == "refunded")
                    .ThenByDescending(p => p.Id).Select(p => p.Status).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<BookingPage> GetPlayerBookingsAsync(
        long userId, string view, int page, int pageSize, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = db.Bookings.AsNoTracking().Where(b => b.UserId == userId);
        query = view switch
        {
            "upcoming" => query.Where(b =>
                (b.Status == "confirmed" && b.EndAt > now)
                || (b.Status == "pending" && b.HoldExpiresAt > now)),
            "history" => query.Where(b => b.Status == "cancelled" || b.Status == "completed"
                || (b.Status == "pending" && (b.HoldExpiresAt == null || b.HoldExpiresAt <= now))
                || (b.Status == "confirmed" && b.EndAt <= now)),
            _ => query
        };

        var total = await query.CountAsync(cancellationToken);
        var ordered = view == "upcoming"
            ? query.OrderBy(b => b.StartAt).ThenByDescending(b => b.Id)
            : query.OrderByDescending(b => b.StartAt).ThenByDescending(b => b.Id);

        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(b => new BookingView(
                b.Id, b.UserId, b.Status, b.Court!.VenueId, b.Court.Venue!.Name, b.Court.Venue.Address,
                b.Court.Venue.Lat, b.Court.Venue.Lng, b.Court.Name, b.Court.Sport, b.Court.Environment,
                b.StartAt, b.EndAt, b.Price, b.HoldExpiresAt, b.CancelReason, b.CreatedAt,
                db.Payments.Where(p => p.Type == "booking" && p.BookingId == b.Id)
                    .OrderByDescending(p => p.Status == "paid" || p.Status == "refund_pending" || p.Status == "refunded")
                    .ThenByDescending(p => p.Id).Select(p => p.Status).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new BookingPage(items, total);
    }

    public async Task<CancelBookingResult> CancelOwnedBookingAsync(
        long bookingId, long userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Serializability + row lock prevents two cancellation requests from racing on the same booking/payment.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var booking = await db.Bookings
            .FromSqlInterpolated($"SELECT * FROM dbo.bookings WITH (UPDLOCK, HOLDLOCK) WHERE id = {bookingId} AND user_id = {userId}")
            .SingleOrDefaultAsync(cancellationToken);

        if (booking is null) return CancelBookingResult.NotFound;

        if (booking.Status == "pending")
        {
            var expired = booking.HoldExpiresAt is null || booking.HoldExpiresAt <= now;
            booking.Status = "cancelled";
            booking.CancelReason = expired ? BookingRules.ExpiredHoldReason : BookingRules.PlayerCancelReason;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CancelBookingResult.Cancelled;
        }

        if (booking.Status != "confirmed") return CancelBookingResult.NotCancellable;
        if (booking.StartAt < now.AddHours(24)) return CancelBookingResult.TooLate;

        var payment = await db.Payments
            .Where(p => p.Type == "booking" && p.BookingId == booking.Id && p.Status == "paid")
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null || payment.Status != "paid") return CancelBookingResult.PaidPaymentNotFound;

        payment.Status = "refund_pending";
        // Schema constraint chỉ cho payout_status 'none' hoặc 'paid' khi payment chờ hoàn tiền.
        if (payment.PayoutStatus == "pending") payment.PayoutStatus = "none";
        booking.Status = "cancelled";
        booking.CancelReason = BookingRules.PlayerPaidCancelReason;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CancelBookingResult.Cancelled;
    }
}
