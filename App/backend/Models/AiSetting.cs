namespace DatSan.Api.Models;

// Maps existing dbo.ai_settings. value is JSON (object, array or scalar such as 5).
public sealed class AiSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
