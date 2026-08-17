using System.Diagnostics;
using System.Text;
using AgentDesk.Core.Usage;
using AgentDesk.Server.Usage;
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

    [Fact]
    public void Parse_TabDelimited_TwoGroupsWithIsoTimestamps()
    {
        string sample =
            "Gemini Models\tWeekly Limit Remaining\t94%\t2026-08-19T03:44:47Z\n" +
            "Gemini Models\tFive Hour Limit Remaining\t85%\t2026-08-14T14:34:00Z\n" +
            "Claude and GPT Models\tWeekly Limit Remaining\t100%\t2026-08-19T03:44:47Z\n" +
            "Claude and GPT Models\tFive Hour Limit Remaining\t70%\t2026-08-14T15:00:00Z";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Equal(94, snapshot.WeeklyRemainingPercent);
        Assert.Equal("08/19 11:44", snapshot.WeeklyRefreshText);
        Assert.Equal(85, snapshot.FiveHourRemainingPercent);
        Assert.Equal("08/14 22:34", snapshot.FiveHourRefreshText);

        Assert.Equal(85, snapshot.GeminiFiveHourRemainingPercent);
        Assert.Equal("08/14 22:34", snapshot.GeminiFiveHourRefreshText);

        Assert.Equal(70, snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal("08/14 23:00", snapshot.ClaudeFiveHourRefreshText);
    }

    [Fact]
    public void Parse_TabDelimited_ClaudeDisabled_ReturnsNullPercentAndDisabledText()
    {
        string sample =
            "Gemini Models\tWeekly Limit Remaining\t94%\t2026-08-19T03:44:47Z\n" +
            "Gemini Models\tFive Hour Limit Remaining\t85%\t2026-08-14T14:34:00Z\n" +
            "Claude and GPT Models\tWeekly Limit Remaining\t100%\t2026-08-19T03:44:47Z\n" +
            "Claude and GPT Models\tFive Hour Limit Remaining\tDisabled\t";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Equal(94, snapshot.WeeklyRemainingPercent);
        Assert.Equal("08/19 11:44", snapshot.WeeklyRefreshText);
        Assert.Equal(85, snapshot.FiveHourRemainingPercent);
        Assert.Equal("08/14 22:34", snapshot.FiveHourRefreshText);

        Assert.Equal(85, snapshot.GeminiFiveHourRemainingPercent);
        Assert.Equal("08/14 22:34", snapshot.GeminiFiveHourRefreshText);

        Assert.Null(snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal("disabled", snapshot.ClaudeFiveHourRefreshText);
    }

    [Fact]
    public void Parse_TabDelimited_GeminiDisabled_ReturnsNullPercentAndDisabledText()
    {
        string sample =
            "Gemini Models\tWeekly Limit Remaining\t94%\t2026-08-19T03:44:47Z\n" +
            "Gemini Models\tFive Hour Limit Remaining\tDisabled: weekly limit reached\t\n" +
            "Claude and GPT Models\tWeekly Limit Remaining\t100%\t2026-08-19T03:44:47Z\n" +
            "Claude and GPT Models\tFive Hour Limit Remaining\t60%\t2026-08-14T15:00:00Z";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Equal(94, snapshot.WeeklyRemainingPercent);
        Assert.Equal("08/19 11:44", snapshot.WeeklyRefreshText);

        Assert.Null(snapshot.FiveHourRemainingPercent);
        Assert.Equal("disabled", snapshot.FiveHourRefreshText);

        Assert.Null(snapshot.GeminiFiveHourRemainingPercent);
        Assert.Equal("disabled", snapshot.GeminiFiveHourRefreshText);

        Assert.Equal(60, snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal("08/14 23:00", snapshot.ClaudeFiveHourRefreshText);
    }

    [Fact]
    public void Parse_TabDelimited_WithAnsiAndEmptyLines_ParsesCorrectly()
    {
        string sample =
            "\n" +
            "\u001b[32mGemini Models\tWeekly Limit Remaining\t96.26%\t2026-08-19T03:44:47Z\u001b[0m\n" +
            "\n" +
            "Gemini Models\tFive Hour Limit Remaining\t78.19%\t2026-08-14T14:34:00Z\n" +
            "\n";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Equal(96, snapshot.WeeklyRemainingPercent);
        Assert.Equal("08/19 11:44", snapshot.WeeklyRefreshText);
        Assert.Equal(78, snapshot.FiveHourRemainingPercent);
        Assert.Equal("08/14 22:34", snapshot.FiveHourRefreshText);
        Assert.Equal(78, snapshot.GeminiFiveHourRemainingPercent);
        Assert.Null(snapshot.ClaudeFiveHourRemainingPercent);
    }

    [Fact]
    public void Parse_TabDelimited_HeaderRowAndUnrelatedRows_Ignored()
    {
        string sample =
            "Group\tWindow\tRemaining\tReset Time\n" +
            "Gemini Models\tWeekly Limit Remaining\t90%\t2026-08-19T03:44:47Z\n" +
            "Unrelated Header\tInvalid Column\tFoo\tBar\n" +
            "Claude and GPT Models\tWeekly Limit Remaining\t100%\t2026-08-19T03:44:47Z";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Equal(90, snapshot.WeeklyRemainingPercent);
        Assert.Equal("08/19 11:44", snapshot.WeeklyRefreshText);
    }

    [Fact]
    public void Parse_TabDelimited_IsoTimestamp_ExactUserExample_FormatsAsUtcPlus8()
    {
        string sample =
            "Gemini Models\tWeekly Limit Remaining\t90%\t2026-08-17T07:36:06Z\n" +
            "Gemini Models\tFive Hour Limit Remaining\t80%\t2026-08-17T07:36:06Z";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Equal(90, snapshot.WeeklyRemainingPercent);
        Assert.Equal("08/17 15:36", snapshot.WeeklyRefreshText);
        Assert.Equal(80, snapshot.FiveHourRemainingPercent);
        Assert.Equal("08/17 15:36", snapshot.FiveHourRefreshText);
        Assert.Equal(80, snapshot.GeminiFiveHourRemainingPercent);
        Assert.Equal("08/17 15:36", snapshot.GeminiFiveHourRefreshText);
    }

    [Fact]
    public void Parse_TabDelimited_IsoTimestamp_CrossesUtcPlus8DateBoundary()
    {
        // 2026-08-17T20:30:00Z + 8h = 2026-08-18 04:30
        string sample =
            "Claude and GPT Models\tWeekly Limit Remaining\t95%\t2026-08-17T20:30:00Z\n" +
            "Claude and GPT Models\tFive Hour Limit Remaining\t65%\t2026-08-17T23:59:59Z";

        var snapshot = AntigravityUsageParser.Parse(sample);

        Assert.NotNull(snapshot);
        Assert.Equal(95, snapshot.WeeklyRemainingPercent);
        Assert.Equal("08/18 04:30", snapshot.WeeklyRefreshText);
        Assert.Equal(65, snapshot.FiveHourRemainingPercent);
        Assert.Equal("08/18 07:59", snapshot.FiveHourRefreshText);
        Assert.Equal(65, snapshot.ClaudeFiveHourRemainingPercent);
        Assert.Equal("08/18 07:59", snapshot.ClaudeFiveHourRefreshText);
    }

    [Fact]
    public void BuildStartInfo_ConfiguresProcessStartInfoCorrectly()
    {
        var startInfo = PowerShellAntigravityRunner.BuildStartInfo(
            cli: "agy",
            pwshPath: "pwsh.exe",
            cwd: @"C:\test\working_dir");

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Hidden, startInfo.WindowStyle);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.Equal(Encoding.UTF8, startInfo.StandardOutputEncoding);
        Assert.Equal(Encoding.UTF8, startInfo.StandardErrorEncoding);
        Assert.Equal(@"C:\test\working_dir", startInfo.WorkingDirectory);

        Assert.Contains("-NoLogo", startInfo.ArgumentList);
        Assert.Contains("-NoProfile", startInfo.ArgumentList);
        Assert.Contains("-NonInteractive", startInfo.ArgumentList);
        Assert.Contains("-WindowStyle", startInfo.ArgumentList);
        Assert.Contains("Hidden", startInfo.ArgumentList);
        Assert.Contains("-Command", startInfo.ArgumentList);

        int cmdIndex = startInfo.ArgumentList.IndexOf("-Command");
        Assert.True(cmdIndex >= 0 && cmdIndex + 1 < startInfo.ArgumentList.Count);
        string staticCmd = startInfo.ArgumentList[cmdIndex + 1];
        int expectedInnerTimeoutSeconds = (int)PowerShellAntigravityRunner.DefaultInnerTimeout.TotalSeconds;
        Assert.Equal($"& $env:AGENTDESK_AGY_EXE -p \"/usage\" --print-timeout {expectedInnerTimeoutSeconds}s; exit $LASTEXITCODE", staticCmd);

        Assert.Equal("1", startInfo.EnvironmentVariables["AGY_CLI_HIDE_ACCOUNT_INFO"]);
        Assert.False(string.IsNullOrEmpty(startInfo.EnvironmentVariables["AGENTDESK_AGY_EXE"]));
    }

    [Fact]
    public void PowerShellAntigravityRunner_OuterTimeout_IsStrictlyGreaterThanAgyPrintTimeout()
    {
        var runner = new PowerShellAntigravityRunner();

        var startInfo = PowerShellAntigravityRunner.BuildStartInfo(
            cli: "agy",
            pwshPath: "pwsh.exe",
            cwd: @"C:\test\working_dir");

        int cmdIndex = startInfo.ArgumentList.IndexOf("-Command");
        Assert.True(cmdIndex >= 0 && cmdIndex + 1 < startInfo.ArgumentList.Count);
        string cmd = startInfo.ArgumentList[cmdIndex + 1];

        var match = System.Text.RegularExpressions.Regex.Match(cmd, @"--print-timeout\s+(\d+)s");
        Assert.True(match.Success, "Command must specify --print-timeout with integer seconds");
        int innerTimeoutSeconds = int.Parse(match.Groups[1].Value);

        Assert.Equal((int)PowerShellAntigravityRunner.DefaultInnerTimeout.TotalSeconds, innerTimeoutSeconds);
        Assert.True(runner.Timeout.TotalSeconds > innerTimeoutSeconds,
            $"Outer runner timeout ({runner.Timeout.TotalSeconds}s) must be strictly greater than inner agy print timeout ({innerTimeoutSeconds}s)");
        Assert.True(runner.Timeout.TotalSeconds - innerTimeoutSeconds >= 10,
            $"Outer runner timeout grace ({runner.Timeout.TotalSeconds - innerTimeoutSeconds}s) must provide sufficient bounded cleanup time");
        Assert.Equal(PowerShellAntigravityRunner.DefaultOuterTimeout, runner.Timeout);
        Assert.Equal(PowerShellAntigravityRunner.DefaultInnerTimeout, TimeSpan.FromSeconds(innerTimeoutSeconds));
    }
}
