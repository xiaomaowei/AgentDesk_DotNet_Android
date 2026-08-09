using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace AgentDesk.Hook;

public static class HookConfig
{
    public const string DefaultBridgeUrl = "http://127.0.0.1:8765";
    public const double DefaultTimeoutSeconds = 310.0;
    public const string HookEndpointPath = "/api/v1/hooks/codex";

    public static bool TryParseBridgeUrl(string? rawUrl, [NotNullWhen(true)] out Uri? endpointUri)
    {
        endpointUri = null;
        string urlToParse = string.IsNullOrWhiteSpace(rawUrl) ? DefaultBridgeUrl : rawUrl.Trim();

        if (!Uri.TryCreate(urlToParse, UriKind.Absolute, out var baseUri))
        {
            return false;
        }

        if (!string.Equals(baseUri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string host = baseUri.Host.Trim('[', ']');
        if (!string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new UriBuilder(baseUri)
        {
            Path = HookEndpointPath
        };

        endpointUri = builder.Uri;
        return true;
    }

    public static TimeSpan ParseTimeout(string? rawTimeout)
    {
        if (!string.IsNullOrWhiteSpace(rawTimeout) &&
            double.TryParse(rawTimeout, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) &&
            parsed > 0 &&
            !double.IsNaN(parsed) &&
            !double.IsInfinity(parsed))
        {
            return TimeSpan.FromSeconds(parsed);
        }

        return TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    }
}
