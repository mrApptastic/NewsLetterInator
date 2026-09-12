using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public sealed class GmailService(HttpClient httpClient, IGoogleAuthService authService)
{
    public async Task SendEmailAsync(string recipient, string subject, string body, bool isHtml)
    {
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");
        var contentType = isHtml ? "text/html" : "text/plain";

        var message = new StringBuilder()
            .AppendLine($"To: {recipient}")
            .AppendLine($"Subject: {subject}")
            .AppendLine("MIME-Version: 1.0")
            .AppendLine($"Content-Type: {contentType}; charset=UTF-8")
            .AppendLine()
            .Append(body)
            .ToString();

        var raw = Base64UrlEncode(Encoding.UTF8.GetBytes(message));
        var payload = new { raw };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
