using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NewsLetterInator.Shared.Models;

namespace NewsLetterInator.App.Services;

public sealed class GoogleSheetsService(HttpClient httpClient, IGoogleAuthService authService, IGoogleDriveService driveService)
{
    private const string TemplateSheetName = "Templates";

    public Task<IReadOnlyList<SheetInfo>> GetSheetsAsync()
    {
        return driveService.GetSheetsAsync();
    }

    public async Task<SheetInfo> CreateSheetAsync(string name)
    {
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        var payload = new
        {
            properties = new
            {
                title = string.IsNullOrWhiteSpace(name) ? "People Sheet" : name.Trim()
            }
        };

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "https://sheets.googleapis.com/v4/spreadsheets")
        {
            Content = JsonContent.Create(payload)
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var createResponse = await httpClient.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<CreateSpreadsheetResponse>()
            ?? throw new InvalidOperationException("Failed to parse create spreadsheet response.");

        await AddHeaderRowAsync(created.SpreadsheetId, token.AccessToken);
        await EnsureTemplateSheetSetupAsync(created.SpreadsheetId, token.AccessToken);

        return new SheetInfo
        {
            Id = created.SpreadsheetId,
            Name = created.Properties?.Title ?? "People Sheet"
        };
    }

    public async Task<IReadOnlyList<Person>> GetPeopleAsync(string spreadsheetId)
    {
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/A:C");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ValuesResponse>();
        var rows = payload?.Values ?? [];

        if (rows.Count == 0)
        {
            return [];
        }

        var people = new List<Person>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count == 0)
            {
                continue;
            }

            var person = new Person
            {
                Name = row.Count > 0 ? row[0]?.ToString() ?? string.Empty : string.Empty,
                Email = row.Count > 1 ? row[1]?.ToString() ?? string.Empty : string.Empty,
                Active = row.Count > 2 && bool.TryParse(row[2]?.ToString(), out var activeValue) && activeValue
            };

            if (!string.IsNullOrWhiteSpace(person.Email))
            {
                people.Add(person);
            }
        }

        return people;
    }

    public async Task AddPersonAsync(string spreadsheetId, Person person)
    {
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        var payload = new
        {
            values = new[]
            {
                new[]
                {
                    person.Name,
                    person.Email,
                    person.Active.ToString().ToLowerInvariant()
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/A:C:append?valueInputOption=USER_ENTERED")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<MailTemplate>> GetMailTemplatesAsync(string spreadsheetId)
    {
        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        await EnsureTemplateSheetSetupAsync(spreadsheetId, token.AccessToken);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString($"'{TemplateSheetName}'!A:D")}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ValuesResponse>();
        var rows = payload?.Values ?? [];

        if (rows.Count <= 1)
        {
            return [];
        }

        var templates = new List<MailTemplate>();
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count == 0)
            {
                continue;
            }

            var template = new MailTemplate
            {
                Name = row.Count > 0 ? row[0]?.ToString() ?? string.Empty : string.Empty,
                Subject = row.Count > 1 ? row[1]?.ToString() ?? string.Empty : string.Empty,
                Body = row.Count > 2 ? row[2]?.ToString() ?? string.Empty : string.Empty,
                IsHtml = row.Count > 3 && bool.TryParse(row[3]?.ToString(), out var isHtml) && isHtml
            };

            if (!string.IsNullOrWhiteSpace(template.Name))
            {
                templates.Add(template);
            }
        }

        return templates;
    }

    public async Task SaveMailTemplateAsync(string spreadsheetId, MailTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            throw new InvalidOperationException("Template name is required.");
        }

        var token = await authService.RequestAccessTokenAsync() ?? throw new InvalidOperationException("Google access token was not granted.");

        await EnsureTemplateSheetSetupAsync(spreadsheetId, token.AccessToken);

        using var readRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString($"'{TemplateSheetName}'!A:D")}");
        readRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var readResponse = await httpClient.SendAsync(readRequest);
        readResponse.EnsureSuccessStatusCode();

        var existingPayload = await readResponse.Content.ReadFromJsonAsync<ValuesResponse>();
        var rows = existingPayload?.Values ?? [];

        var values = new[]
        {
            new[]
            {
                template.Name.Trim(),
                template.Subject,
                template.Body,
                template.IsHtml.ToString().ToLowerInvariant()
            }
        };

        var matchingRowIndex = -1;
        for (var i = 1; i < rows.Count; i++)
        {
            var name = rows[i].Count > 0 ? rows[i][0]?.ToString() : null;
            if (string.Equals(name, template.Name, StringComparison.OrdinalIgnoreCase))
            {
                matchingRowIndex = i + 1;
                break;
            }
        }

        HttpRequestMessage writeRequest;
        if (matchingRowIndex > 0)
        {
            writeRequest = new HttpRequestMessage(HttpMethod.Put,
                $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString($"'{TemplateSheetName}'!A{matchingRowIndex}:D{matchingRowIndex}")}?valueInputOption=RAW")
            {
                Content = JsonContent.Create(new { values })
            };
        }
        else
        {
            writeRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString($"'{TemplateSheetName}'!A:D")}:append?valueInputOption=RAW")
            {
                Content = JsonContent.Create(new { values })
            };
        }

        using (writeRequest)
        {
            writeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            using var writeResponse = await httpClient.SendAsync(writeRequest);
            writeResponse.EnsureSuccessStatusCode();
        }
    }

    private async Task AddHeaderRowAsync(string spreadsheetId, string accessToken)
    {
        var payload = new
        {
            values = new[]
            {
                new[] { "Name", "Email", "Active" }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/A1:C1?valueInputOption=RAW")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task EnsureTemplateSheetSetupAsync(string spreadsheetId, string accessToken)
    {
        try
        {
            await AddTemplateHeaderRowAsync(spreadsheetId, accessToken);
        }
        catch
        {
            await CreateTemplateSheetAsync(spreadsheetId, accessToken);
            await AddTemplateHeaderRowAsync(spreadsheetId, accessToken);
        }
    }

    private async Task AddTemplateHeaderRowAsync(string spreadsheetId, string accessToken)
    {
        var payload = new
        {
            values = new[]
            {
                new[] { "TemplateName", "Subject", "Body", "IsHtml" }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}/values/{Uri.EscapeDataString($"'{TemplateSheetName}'!A1:D1")}?valueInputOption=RAW")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task CreateTemplateSheetAsync(string spreadsheetId, string accessToken)
    {
        var payload = new
        {
            requests = new object[]
            {
                new
                {
                    addSheet = new
                    {
                        properties = new
                        {
                            title = TemplateSheetName
                        }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(spreadsheetId)}:batchUpdate")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (!content.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                response.EnsureSuccessStatusCode();
            }
        }
    }

    private sealed class CreateSpreadsheetResponse
    {
        [JsonPropertyName("spreadsheetId")]
        public string SpreadsheetId { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public SpreadsheetProperties? Properties { get; set; }
    }

    private sealed class SpreadsheetProperties
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    private sealed class ValuesResponse
    {
        [JsonPropertyName("values")]
        public List<List<object?>>? Values { get; set; }
    }
}
