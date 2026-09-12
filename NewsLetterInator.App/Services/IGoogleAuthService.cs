using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public interface IGoogleAuthService
{
    Task<GoogleUser?> GetCurrentUserAsync();

    Task<GoogleAccessToken?> RequestAccessTokenAsync();

    Task SignOutAsync();
}
