using DatSan.Api.DTOs;

namespace DatSan.Api.Services.Interface;

public interface IOwnerVenueService
{
    Task<IReadOnlyList<OwnerVenueListItemDto>> ListAsync(long ownerId, CancellationToken cancellationToken);
    Task<OwnerVenueDetailDto> GetAsync(long ownerId, long venueId, CancellationToken cancellationToken);
    Task<OwnerVenueSaveResult> CreateAsync(long ownerId, OwnerVenueInput input, CancellationToken cancellationToken);
    Task<OwnerVenueSaveResult> UpdateAsync(long ownerId, long venueId, OwnerVenueInput input, CancellationToken cancellationToken);
}
