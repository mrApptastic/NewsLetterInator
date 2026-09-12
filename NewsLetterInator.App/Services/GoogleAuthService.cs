using System.Net.Http.Headers;
using System.Net.Http.Json;
using NewsLetterInator.Shared.Models;
using Microsoft.JSInterop;

namespace NewsLetterInator.App.Services;

public sealed class GoogleAuthService(IJSRuntime jsRuntime, IConfiguration configuration) : IGoogleAuthService, IAsyncDisposable
{
    private const string ModulePath = "./js/googleAuth.js";
    private readonly Lazy<Task<IJSObjectReference>> moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask());
    private readonly string? clientId = configuration["Google:ClientId"];

    public async Task<GoogleUser?> GetCurrentUserAsync()
    {
        var module = await moduleTask.Value;
        var user = await module.InvokeAsync<GoogleUser?>("getCurrentUser");

        return user;
    }

    public async Task<GoogleAccessToken?> RequestAccessTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException("Google:ClientId is missing from appsettings.");
        }

        var module = await moduleTask.Value;
        var scope = string.Join(' ', GoogleScopes.RequiredForPoc);
        var token = await module.InvokeAsync<GoogleAccessToken?>("requestAccessToken", clientId, scope);

        return token;
    }

    public async Task SignOutAsync()
    {
        var module = await moduleTask.Value;
        await module.InvokeVoidAsync("signOut", clientId);
    }

    public async ValueTask DisposeAsync()
    {
        if (!moduleTask.IsValueCreated)
        {
            return;
        }

        var module = await moduleTask.Value;
        await module.DisposeAsync();
    }
}
