namespace DatSan.Api.Repositories.Interface;

public interface ISettingRepository
{
    /// <summary>Giá trị JSON thô của dbo.ai_settings theo key, hoặc null nếu chưa có.</summary>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken);
}
