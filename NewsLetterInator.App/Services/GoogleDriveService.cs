using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public sealed class GoogleDriveService(HttpClient httpClient, IGoogleAuthService authService) : IGoogleDriveService
{
    private const string AppFolderName = "NewsLetterInator";
    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private string? appFolderId;

    public async Task<IReadOnlyList<SheetInfo>> GetSheetsAsync()
    {
        _ = await EnsureAppFolderIdAsync();

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

    public async Task EnsureSheetInAppFolderAsync(string spreadsheetId)
    {
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            throw new InvalidOperationException("Spreadsheet id is required.");
        }

        var folderId = await EnsureAppFolderIdAsync();
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        using var detailsRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(spreadsheetId)}?fields=id,name,parents");
        detailsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var detailsResponse = await httpClient.SendAsync(detailsRequest);
        detailsResponse.EnsureSuccessStatusCode();

        var details = await detailsResponse.Content.ReadFromJsonAsync<DriveFile>();
        var parents = details?.Parents ?? [];

        if (parents.Contains(folderId, StringComparer.Ordinal))
        {
            return;
        }

        var removeParentsQuery = parents.Count > 0
            ? $"&removeParents={Uri.EscapeDataString(string.Join(',', parents))}"
            : string.Empty;

        using var moveRequest = new HttpRequestMessage(HttpMethod.Patch,
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(spreadsheetId)}?addParents={Uri.EscapeDataString(folderId)}{removeParentsQuery}&fields=id,parents");
        moveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var moveResponse = await httpClient.SendAsync(moveRequest);
        moveResponse.EnsureSuccessStatusCode();
    }

    public async Task<MailAttachment> UploadTemplateAttachmentAsync(string spreadsheetId, string templateName, MailAttachment attachment)
    {
        if (string.IsNullOrWhiteSpace(spreadsheetId))
        {
            throw new InvalidOperationException("Spreadsheet id is required.");
        }

        if (attachment.Content is null || attachment.Content.Length == 0)
        {
            throw new InvalidOperationException($"Attachment '{attachment.FileName}' has no content.");
        }

        var folderId = await EnsureAppFolderIdAsync();
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");
        var safeTemplateName = string.IsNullOrWhiteSpace(templateName) ? "Template" : templateName.Trim();
        var safeFileName = string.IsNullOrWhiteSpace(attachment.FileName) ? "attachment.bin" : attachment.FileName;
        var driveFileName = $"{spreadsheetId}__{safeTemplateName}__{safeFileName}";

        var metadataJson = $"{{\"name\":\"{EscapeJson(driveFileName)}\",\"parents\":[\"{EscapeJson(folderId)}\"]}}";
        var metadataContent = new StringContent(metadataJson, Encoding.UTF8, "application/json; charset=UTF-8");
        var fileContentType = string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType;
        var uploadContent = new ByteArrayContent(attachment.Content);
        uploadContent.Headers.ContentType = new MediaTypeHeaderValue(fileContentType);

        using var multipart = new MultipartContent("related", $"upload_{Guid.NewGuid():N}")
        {
            metadataContent,
            uploadContent
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,mimeType,size")
        {
            Content = multipart
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var file = await response.Content.ReadFromJsonAsync<DriveFile>()
            ?? throw new InvalidOperationException("Failed to parse uploaded attachment metadata.");

        return new MailAttachment
        {
            DriveFileId = file.Id,
            FileName = file.Name ?? safeFileName,
            ContentType = string.IsNullOrWhiteSpace(file.MimeType) ? fileContentType : file.MimeType,
            Size = file.Size ?? attachment.Size,
            IsStoredOnDrive = true,
            Content = []
        };
    }

    public async Task<MailAttachment> GetAttachmentAsync(string fileId, bool includeContent)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new InvalidOperationException("Attachment file id is required.");
        }

        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        using var metadataRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}?fields=id,name,mimeType,size");
        metadataRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var metadataResponse = await httpClient.SendAsync(metadataRequest);
        metadataResponse.EnsureSuccessStatusCode();

        var metadata = await metadataResponse.Content.ReadFromJsonAsync<DriveFile>()
            ?? throw new InvalidOperationException("Failed to parse attachment metadata.");

        var attachment = new MailAttachment
        {
            DriveFileId = metadata.Id,
            FileName = metadata.Name ?? "Attachment",
            ContentType = string.IsNullOrWhiteSpace(metadata.MimeType) ? "application/octet-stream" : metadata.MimeType,
            Size = metadata.Size ?? 0,
            IsStoredOnDrive = true,
            Content = []
        };

        if (!includeContent)
        {
            return attachment;
        }

        using var contentRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(fileId)}?alt=media");
        contentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var contentResponse = await httpClient.SendAsync(contentRequest);
        contentResponse.EnsureSuccessStatusCode();

        attachment.Content = await contentResponse.Content.ReadAsByteArrayAsync();
        if (attachment.Size <= 0)
        {
            attachment.Size = attachment.Content.LongLength;
        }

        return attachment;
    }

    private async Task<string> EnsureAppFolderIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(appFolderId))
        {
            return appFolderId;
        }

        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");
        var query = $"name='{EscapeDriveQueryValue(AppFolderName)}' and mimeType='{FolderMimeType}' and trashed=false";

        using var searchRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://www.googleapis.com/drive/v3/files?q={Uri.EscapeDataString(query)}&fields=files(id,name)&pageSize=1");
        searchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var searchResponse = await httpClient.SendAsync(searchRequest);
        searchResponse.EnsureSuccessStatusCode();

        var found = await searchResponse.Content.ReadFromJsonAsync<DriveFilesResponse>();
        var existingId = found?.Files?.FirstOrDefault()?.Id;
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            appFolderId = existingId;
            return appFolderId;
        }

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "https://www.googleapis.com/drive/v3/files?fields=id,name")
        {
            Content = JsonContent.Create(new
            {
                name = AppFolderName,
                mimeType = FolderMimeType
            })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var createResponse = await httpClient.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<DriveFile>()
            ?? throw new InvalidOperationException("Failed to create NewsLetterInator folder in Google Drive.");

        appFolderId = created.Id ?? throw new InvalidOperationException("NewsLetterInator folder id was missing.");
        return appFolderId;
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeDriveQueryValue(string value)
    {
        return value.Replace("'", "\\'", StringComparison.Ordinal);
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

        [JsonPropertyName("mimeType")]
        public string? MimeType { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("parents")]
        public List<string>? Parents { get; set; }
    }
}
