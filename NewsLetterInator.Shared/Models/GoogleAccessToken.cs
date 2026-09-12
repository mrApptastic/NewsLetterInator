namespace NewsLetterInator.Shared.Models;

public sealed class GoogleAccessToken
{
    public string AccessToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string Scope { get; set; } = string.Empty;
}
