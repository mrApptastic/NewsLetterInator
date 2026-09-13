using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public sealed class GmailService(HttpClient httpClient, IGoogleAuthService authService)
{
    public async Task SendEmailAsync(string recipient, string subject, string body, bool isHtml, IReadOnlyList<MailAttachment>? attachments = null)
    {
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        var mimeMessage = BuildMimeMessage(recipient, subject, body, isHtml, attachments ?? []);

        var raw = Base64UrlEncode(Encoding.UTF8.GetBytes(mimeMessage));
        var payload = new { raw };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildMimeMessage(string recipient, string subject, string body, bool isHtml, IReadOnlyList<MailAttachment> attachments)
    {
        var bodyContentType = isHtml ? "text/html" : "text/plain";
        var message = new StringBuilder()
            .Append($"To: {recipient}\r\n")
            .Append($"Subject: {subject}\r\n")
            .Append("MIME-Version: 1.0\r\n");

        if (attachments.Count == 0)
        {
            message
                .Append($"Content-Type: {bodyContentType}; charset=UTF-8\r\n")
                .Append("Content-Transfer-Encoding: 8bit\r\n")
                .Append("\r\n")
                .Append(body);

            return message.ToString();
        }

        var mixedBoundary = $"mixed_{Guid.NewGuid():N}";
        message
            .Append($"Content-Type: multipart/mixed; boundary=\"{mixedBoundary}\"\r\n")
            .Append("\r\n")
            .Append($"--{mixedBoundary}\r\n")
            .Append($"Content-Type: {bodyContentType}; charset=UTF-8\r\n")
            .Append("Content-Transfer-Encoding: 8bit\r\n")
            .Append("\r\n")
            .Append(body)
            .Append("\r\n");

        foreach (var attachment in attachments)
        {
            var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType;
            var base64Content = Convert.ToBase64String(attachment.Content, Base64FormattingOptions.InsertLineBreaks);

            message
                .Append($"--{mixedBoundary}\r\n")
                .Append($"Content-Type: {contentType}; name=\"{attachment.FileName}\"\r\n")
                .Append("Content-Transfer-Encoding: base64\r\n")
                .Append($"Content-Disposition: attachment; filename=\"{attachment.FileName}\"\r\n")
                .Append("\r\n")
                .Append(base64Content)
                .Append("\r\n");
        }

        message.Append($"--{mixedBoundary}--");

        return message.ToString();
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
