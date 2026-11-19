using DatSan.Api.DTOs;

namespace DatSan.Api.Services.Interface;

public interface IPlayerHomeService
{
    Task<PlayerHomeResponse> GetHomeAsync(long userId, double? lat, double? lng, CancellationToken cancellationToken);
}
