using DatSan.Api.DTOs;

namespace DatSan.Api.Services.Interface;

public interface IAdminVenueService
{
    Task<AdminVenueListResponse> ListAsync(string? status, CancellationToken cancellationToken);
    Task<AdminVenueDetailDto> GetAsync(long venueId, CancellationToken cancellationToken);
    Task<AdminVenueActionResult> ApproveAsync(long venueId, CancellationToken cancellationToken);
    Task<AdminVenueActionResult> RejectAsync(long venueId, string? reason, CancellationToken cancellationToken);
    Task<AdminVenueActionResult> HideAsync(long venueId, string? reason, CancellationToken cancellationToken);
}
