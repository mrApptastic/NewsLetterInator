using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public sealed class GoogleDriveService(HttpClient httpClient, IGoogleAuthService authService) : IGoogleDriveService
{
    public async Task<IReadOnlyList<SheetInfo>> GetSheetsAsync()
    {
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://www.googleapis.com/drive/v3/files?q=mimeType='application/vnd.google-apps.spreadsheet'%20and%20trashed=false&fields=files(id,name)&orderBy=modifiedTime%20desc&pageSize=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DriveFilesResponse>();
        return payload?.Files?.Select(f => new SheetInfo { Id = f.Id ?? string.Empty, Name = f.Name ?? "Untitled Sheet" }).ToList()
            ?? [];
    }

    private sealed class DriveFilesResponse
    {
        [JsonPropertyName("files")]
        public List<DriveFile>? Files { get; set; }
    }

    private sealed class DriveFile
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
