namespace CnabApi.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "cnab-api";
    public string Audience { get; set; } = "cnab-api-client";
    public string SigningKey { get; set; } = string.Empty; // must be at least 32 chars
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}
