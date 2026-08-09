using AgentDesk.Hook;
using Xunit;

namespace AgentDesk.Hook.Tests;

public class HookConfigTests
{
    [Theory]
    [InlineData("http://127.0.0.1:8765", "http://127.0.0.1:8765/api/v1/hooks/codex")]
    [InlineData("http://localhost:8765", "http://localhost:8765/api/v1/hooks/codex")]
    [InlineData("http://[::1]:8765", "http://[::1]:8765/api/v1/hooks/codex")]
    [InlineData("http://127.0.0.1", "http://127.0.0.1/api/v1/hooks/codex")]
    [InlineData(null, "http://127.0.0.1:8765/api/v1/hooks/codex")]
    [InlineData("", "http://127.0.0.1:8765/api/v1/hooks/codex")]
    public void TryParseBridgeUrl_ValidLoopback_ReturnsTrueAndCorrectUri(string? inputUrl, string expectedUriStr)
    {
        bool success = HookConfig.TryParseBridgeUrl(inputUrl, out Uri? endpointUri);

        Assert.True(success);
        Assert.NotNull(endpointUri);
        Assert.Equal(new Uri(expectedUriStr), endpointUri);
    }

    [Theory]
    [InlineData("https://127.0.0.1:8765")]
    [InlineData("http://192.168.1.100:8765")]
    [InlineData("http://example.com:8765")]
    [InlineData("ftp://localhost:8765")]
    [InlineData("invalid-url")]
    public void TryParseBridgeUrl_NonLoopbackOrNonHttp_ReturnsFalse(string? inputUrl)
    {
        bool success = HookConfig.TryParseBridgeUrl(inputUrl, out Uri? endpointUri);

        Assert.False(success);
        Assert.Null(endpointUri);
    }

    [Theory]
    [InlineData("310", 310.0)]
    [InlineData("60.5", 60.5)]
    [InlineData(null, 310.0)]
    [InlineData("", 310.0)]
    [InlineData("invalid", 310.0)]
    [InlineData("-10", 310.0)]
    [InlineData("0", 310.0)]
    [InlineData("NaN", 310.0)]
    [InlineData("Infinity", 310.0)]
    public void ParseTimeout_ValidAndInvalidInputs_ReturnsExpectedTimeSpan(string? inputTimeout, double expectedSeconds)
    {
        TimeSpan timeout = HookConfig.ParseTimeout(inputTimeout);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), timeout);
    }
}
