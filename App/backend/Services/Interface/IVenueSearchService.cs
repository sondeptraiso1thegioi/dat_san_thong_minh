using DatSan.Api.DTOs;

namespace DatSan.Api.Services.Interface;

public interface IVenueSearchService
{
    Task<VenueSearchResponse> SearchAsync(long userId, VenueSearchQuery query, CancellationToken cancellationToken);
}
