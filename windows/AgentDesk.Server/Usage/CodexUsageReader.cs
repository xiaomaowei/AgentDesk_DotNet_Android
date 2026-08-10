using System.Net.Http.Headers;
using System.Text.Json;
using AgentDesk.Core.Models;
using AgentDesk.Core.Usage;

namespace AgentDesk.Server.Usage;

public interface ICodexUsageReader
{
    Task<CodexUsagePayload?> FetchAsync(CancellationToken cancellationToken = default);
}

public class CodexUsageReader : ICodexUsageReader
{
    private readonly HttpClient _httpClient;
    private readonly string _authJsonPath;

    public CodexUsageReader(HttpClient httpClient, string? authJsonPath = null)
    {
        _httpClient = httpClient;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile))
        {
            userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
        }
        _authJsonPath = authJsonPath ?? Path.Combine(userProfile, ".codex", "auth.json");
    }

    public async Task<CodexUsagePayload?> FetchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_authJsonPath)) return null;

            var jsonText = await File.ReadAllTextAsync(_authJsonPath, cancellationToken);
            using var authDoc = JsonDocument.Parse(jsonText);
            if (!authDoc.RootElement.TryGetProperty("tokens", out var tokens) ||
                !tokens.TryGetProperty("access_token", out var tokenElement))
            {
                return null;
            }

            var token = tokenElement.GetString();
            if (string.IsNullOrWhiteSpace(token)) return null;

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://chatgpt.com/backend-api/wham/usage");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var response = await _httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode) return null;

            using var responseStream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var usageDoc = await JsonDocument.ParseAsync(responseStream, cancellationToken: cts.Token);
            return CodexUsageParser.Parse(usageDoc.RootElement);
        }
        catch
        {
            return null;
        }
    }
}
