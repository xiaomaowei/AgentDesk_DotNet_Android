using AgentDesk.Core.Usage;
using Xunit;

namespace AgentDesk.Core.Tests;

public class AntigravityUsageParserTests
{
    private static readonly string AntigravitySample =
        "\u001b[36mGEMINI MODELS\u001b[0m\n" +
        "  Models within this group: Gemini Flash, Gemini Pro\n" +
        "Weekly Limit Remaining\n" +
        "[████████████████████████████████████████████████] 96.26%\n" +
        "96% remaining · Refreshes in 141h 33m\n" +
        "Five Hour Limit Remaining\n" +
        "[████████████████████████████████████████        ] 78.19%\n" +
        "78% remaining · Refreshes in 4m\n" +
        "CLAUDE AND GPT MODELS\n" +
        "Models within this group: Claude Opus, Claude Sonnet, GPT-OSS\n" +
        "Weekly Limit Remaining\n" +
        "[████████████████████████████████████████████████] 100.00%\n" +
        "Quota available";

    [Fact]
    public void Parse_SupportsAnsiAndMultipleQuotaGroups()
    {
        var snapshot = AntigravityUsageParser.Parse(AntigravitySample);

        Assert.NotNull(snapshot);
        Assert.Equal(96, snapshot.WeeklyRemainingPercent);
        Assert.Equal("Refreshes in 141h 33m", snapshot.WeeklyRefreshText);
        Assert.Equal(78, snapshot.FiveHourRemainingPercent);
        Assert.Equal("Refreshes in 4m", snapshot.FiveHourRefreshText);

        Assert.Equal(78, snapshot.GeminiFiveHourRemainingPercent);
        Assert.Equal("Refreshes in 4m", snapshot.GeminiFiveHourRefreshText);

        // Claude group has weekly but no 5-hour window -> null five hour percentage
        Assert.Null(snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal(string.Empty, snapshot.ClaudeFiveHourRefreshText);
    }

    [Fact]
    public void Parse_NonQuotaOutput_ReturnsNull()
    {
        var snapshot = AntigravityUsageParser.Parse("The /usage command shows quota information.");
        Assert.Null(snapshot);
    }

    [Fact]
    public void Parse_NullOrEmptyOutput_ReturnsNull()
    {
        Assert.Null(AntigravityUsageParser.Parse(""));
        Assert.Null(AntigravityUsageParser.Parse("   "));
    }

    [Fact]
    public void Parse_ExplicitClaudeDisabled_ReturnsNullPercentAndDisabledText()
    {
        string sample =
            "GEMINI MODELS\n" +
            "Weekly Limit Remaining\n" +
            "90% remaining · Refreshes in 1d\n" +
            "Five Hour Limit Remaining\n" +
            "80% remaining · Refreshes in 10m\n" +
            "CLAUDE AND GPT MODELS\n" +
            "Weekly Limit Remaining\n" +
            "100% remaining\n" +
            "Five Hour Limit Remaining\n" +
            "disabled\n";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal("disabled", snapshot.ClaudeFiveHourRefreshText);
        Assert.Equal(80, snapshot.GeminiFiveHourRemainingPercent);
    }

    [Fact]
    public void Parse_ExplicitGeminiDisabled_ReturnsNullPercentAndDisabledText()
    {
        string sample =
            "GEMINI MODELS\n" +
            "Weekly Limit Remaining\n" +
            "90% remaining · Refreshes in 1d\n" +
            "Five Hour Limit Remaining\n" +
            "disabled\n" +
            "CLAUDE AND GPT MODELS\n" +
            "Weekly Limit Remaining\n" +
            "100% remaining\n" +
            "Five Hour Limit Remaining\n" +
            "60% remaining · Refreshes in 30m\n";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.GeminiFiveHourRemainingPercent);
        Assert.Equal("disabled", snapshot.GeminiFiveHourRefreshText);
        Assert.Equal(60, snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal("Refreshes in 30m", snapshot.ClaudeFiveHourRefreshText);
    }

    [Fact]
    public void Parse_MissingFiveHourSection_NotTreatedAsDisabled()
    {
        string sample =
            "GEMINI MODELS\n" +
            "Weekly Limit Remaining\n" +
            "90% remaining · Refreshes in 1d\n" +
            "CLAUDE AND GPT MODELS\n" +
            "Weekly Limit Remaining\n" +
            "100% remaining\n";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal(string.Empty, snapshot.ClaudeFiveHourRefreshText);
        Assert.Null(snapshot.GeminiFiveHourRemainingPercent);
        Assert.Equal(string.Empty, snapshot.GeminiFiveHourRefreshText);
    }

    [Fact]
    public void Parse_RealDisabledLineWithReasonSuffix_ProducesNullPercentAndDisabledText()
    {
        string sample =
            "CLAUDE AND GPT MODELS\n" +
            "Models within this group: Claude Opus, Claude Sonnet, GPT-OSS\n" +
            "Weekly Limit Remaining\n" +
            "Five Hour Limit Remaining\n" +
            "Disabled: You have hit your weekly limit, the 5-hour limit does not currently apply. Your weekly limit will fully refresh in 4 days, 9 hours.";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal("disabled", snapshot.ClaudeFiveHourRefreshText);
    }

    [Fact]
    public void Parse_ArbitraryExplanatoryText_NotTreatedAsDisabled()
    {
        string sample =
            "CLAUDE AND GPT MODELS\n" +
            "Models within this group: Claude Opus, Claude Sonnet, GPT-OSS\n" +
            "Weekly Limit Remaining\n" +
            "Five Hour Limit Remaining\n" +
            "You have hit your weekly limit, the 5-hour limit does not currently apply.";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.Null(snapshot?.ClaudeFiveHourRemainingPercent);
        Assert.Equal(string.Empty, snapshot?.ClaudeFiveHourRefreshText ?? string.Empty);
    }
}
