using DatSan.Api.Models;

namespace DatSan.Api.Repositories.Interface;

public interface IAdminVenueRepository
{
    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(CancellationToken cancellationToken);

    /// <summary>Cụm sân theo trạng thái, kèm chủ sân và sân con, chỉ để đọc.</summary>
    Task<IReadOnlyList<Venue>> ListAsync(string status, CancellationToken cancellationToken);

    /// <summary>Cụm sân kèm chủ sân, sân con, bảng giá; được theo dõi để đổi trạng thái.</summary>
    Task<Venue?> GetAsync(long venueId, CancellationToken cancellationToken);

    /// <summary>Booking chưa diễn ra (pending/confirmed, bắt đầu sau now) của cụm sân; được theo dõi để hủy.</summary>
    Task<IReadOnlyList<Booking>> GetUpcomingBookingsAsync(long venueId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Thanh toán đã nhận tiền (paid) của các booking; được theo dõi để chuyển sang chờ hoàn tiền.</summary>
    Task<IReadOnlyList<Payment>> GetPaidPaymentsAsync(IReadOnlyCollection<long> bookingIds, CancellationToken cancellationToken);

    /// <summary>Lưu mọi thay đổi trong một lần (EF bọc trong một transaction).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
