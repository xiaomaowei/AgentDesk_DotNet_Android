using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentDesk.Hook;

public static class HookRunner
{
    public static async Task<int> RunAsync(
        Stream stdin,
        TextWriter stdout,
        TextWriter stderr,
        IReadOnlyDictionary<string, string?> env,
        HttpClient httpClient,
        CancellationToken cancellationToken = default)
    {
        void FailOpen(string? diagnosticMessage = null)
        {
            stdout.Write("{}");
            if (!string.IsNullOrWhiteSpace(diagnosticMessage))
            {
                stderr.WriteLine($"AgentDesk hook error: {diagnosticMessage}");
            }
        }

        // 1. Validate Bridge URL configuration
        env.TryGetValue("AGENTDECK_BRIDGE_URL", out string? envUrl);
        if (!HookConfig.TryParseBridgeUrl(envUrl, out Uri? endpointUri))
        {
            FailOpen("Invalid or forbidden bridge URL configuration");
            return 0;
        }

        // 2. Parse Timeout configuration
        env.TryGetValue("AGENTDECK_HOOK_TIMEOUT", out string? envTimeout);
        TimeSpan timeout = HookConfig.ParseTimeout(envTimeout);

        // 3. Read Stdin raw bytes
        byte[] rawBytes;
        try
        {
            using var ms = new MemoryStream();
            await stdin.CopyToAsync(ms, cancellationToken);
            rawBytes = ms.ToArray();
        }
        catch (Exception ex)
        {
            FailOpen($"Failed to read stdin: {ex.Message}");
            return 0;
        }

        if (rawBytes.Length == 0)
        {
            FailOpen("stdin is empty");
            return 0;
        }

        // 4. Parse stdin UTF-8 JSON object
        JsonObject jsonObject;
        try
        {
            var node = JsonNode.Parse(rawBytes);
            if (node is not JsonObject obj)
            {
                FailOpen("stdin JSON must be an object");
                return 0;
            }
            jsonObject = obj;
        }
        catch (Exception ex)
        {
            FailOpen($"stdin JSON parse error: {ex.Message}");
            return 0;
        }

        // 5. Compact Payload
        byte[] payloadBytes;
        try
        {
            payloadBytes = HookCompactor.CompactPayload(rawBytes, jsonObject);
        }
        catch (Exception ex)
        {
            FailOpen($"Payload compaction error: {ex.Message}");
            return 0;
        }

        // 6. Send POST Request
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri);
            request.Content = new ByteArrayContent(payloadBytes);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            using var response = await httpClient.SendAsync(request, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                FailOpen($"Server returned non-2xx status code: {(int)response.StatusCode}");
                return 0;
            }

            string responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                stdout.Write(responseBody);
                return 0;
            }
            catch
            {
                FailOpen("Server response body is not valid JSON");
                return 0;
            }
        }
        catch (Exception ex)
        {
            FailOpen($"HTTP forward error: {ex.Message}");
            return 0;
        }
    }
}
