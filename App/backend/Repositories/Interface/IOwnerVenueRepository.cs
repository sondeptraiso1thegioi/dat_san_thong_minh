using DatSan.Api.Models;

namespace DatSan.Api.Repositories.Interface;

public interface IOwnerVenueRepository
{
    /// <summary>Mọi cụm sân của chủ sân (kể cả chờ duyệt, bị từ chối, đã ẩn), kèm sân con và bảng giá, chỉ để đọc.</summary>
    Task<IReadOnlyList<Venue>> GetOwnedAsync(long ownerId, CancellationToken cancellationToken);

    /// <summary>Cụm sân của đúng chủ sân này, kèm sân con và bảng giá, được theo dõi để sửa. Không phải của mình thì null.</summary>
    Task<Venue?> GetOwnedForEditAsync(long venueId, long ownerId, CancellationToken cancellationToken);

    Task<IReadOnlySet<long>> GetVipVenueIdsAsync(IEnumerable<long> venueIds, DateTimeOffset now, CancellationToken cancellationToken);

    Task AddAsync(Venue venue, CancellationToken cancellationToken);

    /// <summary>Lưu cụm sân và sân con, thay toàn bộ bảng giá bằng newPrices, trong một transaction.</summary>
    Task SaveAsync(Venue venue, IReadOnlyList<PriceRule> newPrices, CancellationToken cancellationToken);
}
