namespace DatSan.Api.Config;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Key { get; init; } = "";
    public int LifetimeMinutes { get; init; } = 60;
}
