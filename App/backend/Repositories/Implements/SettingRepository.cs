using DatSan.Api.Data;
using DatSan.Api.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace DatSan.Api.Repositories.Implements;

public sealed class SettingRepository(AppDbContext db) : ISettingRepository
{
    public Task<string?> GetValueAsync(string key, CancellationToken cancellationToken) =>
        db.AiSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
}
