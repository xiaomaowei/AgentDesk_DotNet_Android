using System.Text.Json;
using AgentDesk.Core.Usage;
using Xunit;

namespace AgentDesk.Core.Tests;

public class CodexUsageParserTests
{
    [Fact]
    public void Parse_ValidPayload_ReturnsSnapshot()
    {
        string json = """
        {
            "rate_limit": {
                "primary_window": {
                    "used_percent": 26,
                    "reset_at": 1786190400
                }
            },
            "rate_limit_reset_credits": {
                "available_count": 1,
                "applicable_available_count": 0
            }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexUsageParser.Parse(doc.RootElement);

        Assert.NotNull(snapshot);
        Assert.Equal(74, snapshot.WeeklyRemainingPercent);
        Assert.Equal(1, snapshot.ResetAvailable);
        Assert.NotNull(snapshot.ResetDate);
        Assert.False(string.IsNullOrEmpty(snapshot.ResetText));
    }

    [Fact]
    public void Parse_MissingPrimaryWindow_FallbackToSecondaryWindow()
    {
        string json = """
        {
            "rate_limit": {
                "secondary_window": {
                    "used_percent": 40,
                    "reset_at": 1786190400
                }
            }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexUsageParser.Parse(doc.RootElement);

        Assert.NotNull(snapshot);
        Assert.Equal(60, snapshot.WeeklyRemainingPercent);
    }

    [Fact]
    public void Parse_PrimaryWindowTakesPrecedenceOverSecondaryWindow()
    {
        string json = """
        {
            "rate_limit": {
                "primary_window": {
                    "used_percent": 10,
                    "reset_at": 1786190400
                },
                "secondary_window": {
                    "used_percent": 50,
                    "reset_at": 1786190400
                }
            }
        }
        """;
        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexUsageParser.Parse(doc.RootElement);

        Assert.NotNull(snapshot);
        Assert.Equal(90, snapshot.WeeklyRemainingPercent);
    }

    [Fact]
    public void Parse_MissingBothWindows_ReturnsNull()
    {
        string json = """{"rate_limit": {}}""";
        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexUsageParser.Parse(doc.RootElement);

        Assert.Null(snapshot);
    }

    [Fact]
    public void Parse_ClampsRemainingPercent()
    {
        string json = """
        {
            "rate_limit": {
                "primary_window": {
                    "used_percent": 120,
                    "reset_at": 1786190400
                }
            }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexUsageParser.Parse(doc.RootElement);

        Assert.NotNull(snapshot);
        Assert.Equal(0, snapshot.WeeklyRemainingPercent);
        Assert.Equal(0, snapshot.ResetAvailable);
    }

    [Fact]
    public void Parse_FormatsLocalResetDateTime_AndNoResetOrResetsWord()
    {
        // 1786190400 is Aug 16, 2026 18:00:00 UTC
        string json = """
        {
            "rate_limit": {
                "primary_window": {
                    "used_percent": 59,
                    "reset_at": 1786190400
                }
            }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var snapshot = CodexUsageParser.Parse(doc.RootElement);

        Assert.NotNull(snapshot);
        Assert.Equal(41, snapshot.WeeklyRemainingPercent);
        Assert.NotNull(snapshot.ResetText);
        Assert.NotNull(snapshot.ResetDate);

        // Verify format starts with "Re: "
        Assert.StartsWith("Re: ", snapshot.ResetText);

        // Verify no Reset or Resets word anywhere in ResetText or ResetDate
        Assert.DoesNotContain("Reset", snapshot.ResetText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Resets", snapshot.ResetText, StringComparison.OrdinalIgnoreCase);

        // Verify date/time string format contains date and 2-digit zero-padded time M/d HH:mm
        var localTime = DateTimeOffset.FromUnixTimeSeconds(1786190400).ToLocalTime();
        string expectedDateStr = localTime.ToString("M/d HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(expectedDateStr, snapshot.ResetDate);
        Assert.Equal($"Re: {expectedDateStr}", snapshot.ResetText);
    }
}
