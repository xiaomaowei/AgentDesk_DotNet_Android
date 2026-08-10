package com.agentdeck.mobile;

import java.util.Arrays;

import org.junit.Test;

import static org.junit.Assert.assertEquals;

public final class LayoutPolicyHelperTest {

    @Test
    public void testButtonTextFormattingAtDifferentFontScales() {
        // FontScale 1.0 (Normal)
        assertEquals("核准", LayoutPolicyHelper.formatButtonText("approve", 1.0f, false));
        assertEquals("拒絕", LayoutPolicyHelper.formatButtonText("reject", 1.0f, false));

        // Pending states
        assertEquals("提交中...", LayoutPolicyHelper.formatButtonText("approve", 1.0f, true));
        assertEquals("提交中...", LayoutPolicyHelper.formatButtonText("reject", 1.0f, true));
        assertEquals("", LayoutPolicyHelper.formatButtonText("unknown", 1.0f, false));
    }

    @Test
    public void testFontSizeCalculation() {
        assertEquals(14.0f, LayoutPolicyHelper.calculateFontSize(14.0f, 1.0f), 0.001f);
        assertEquals(18.2f, LayoutPolicyHelper.calculateFontSize(14.0f, 1.3f), 0.001f);
        assertEquals(28.0f, LayoutPolicyHelper.calculateFontSize(14.0f, 2.0f), 0.001f);
    }

    @Test
    public void testAgentStatusLabelFormatting() {
        assertEquals("目前 Agent 狀態", LayoutPolicyHelper.formatAgentStatusLabel(null));
        assertEquals("目前 Agent 狀態", LayoutPolicyHelper.formatAgentStatusLabel(Arrays.asList("", "  ", null)));
        assertEquals("目前 Agent 狀態\nSol High、Claude Sonnet 4.6",
                LayoutPolicyHelper.formatAgentStatusLabel(
                        Arrays.asList("Sol High", "", "Claude Sonnet 4.6")));
    }

    @Test
    public void testCodexHeaderFormatting() {
        assertEquals("--", LayoutPolicyHelper.formatCodexHeader(null, false));
        assertEquals("--", LayoutPolicyHelper.formatCodexHeader(null, true));

        DashboardState.CodexUsage cu = new DashboardState.CodexUsage(41, "Re: 8/16 18:00", "8/16 18:00", 0);
        assertEquals("41%/1W Re: 8/16 18:00", LayoutPolicyHelper.formatCodexHeader(cu, false));
        assertEquals("41%/1W Re: 8/16 18:00", LayoutPolicyHelper.formatCodexHeader(cu, true));

        // Legacy format normalization test
        DashboardState.CodexUsage cuLegacy = new DashboardState.CodexUsage(85, "Resets 8/12 18:00", "8/12 18:00", 0);
        assertEquals("85%/1W Re: 8/12 18:00", LayoutPolicyHelper.formatCodexHeader(cuLegacy, false));
    }

    @Test
    public void testFormatRefreshText_refreshesInPattern() {
        assertEquals("Re:1h32m", LayoutPolicyHelper.formatRefreshText("Refreshes in 1h 32m"));
        assertEquals("Re:4m", LayoutPolicyHelper.formatRefreshText("Refreshes in 4m"));
        assertEquals("Re:141h33m", LayoutPolicyHelper.formatRefreshText("Refreshes in 141h 33m"));
        assertEquals("Re:4h30m", LayoutPolicyHelper.formatRefreshText("Refreshes in 4h 30m"));
    }

    @Test
    public void testFormatRefreshText_quotaAvailable() {
        assertEquals("Re:--", LayoutPolicyHelper.formatRefreshText("Quota available"));
        assertEquals("Re:--", LayoutPolicyHelper.formatRefreshText("quota available"));
        assertEquals("Re:--", LayoutPolicyHelper.formatRefreshText("unexpected refresh state"));
    }

    @Test
    public void testFormatRefreshText_nullOrBlank() {
        assertEquals("Re:--", LayoutPolicyHelper.formatRefreshText(null));
        assertEquals("Re:--", LayoutPolicyHelper.formatRefreshText(""));
        assertEquals("Re:--", LayoutPolicyHelper.formatRefreshText("   "));
    }

    @Test
    public void testAntigravityHeaderFormattingNullReturnsPlaceholder() {
        assertEquals("--", LayoutPolicyHelper.formatAntigravityHeader(null, false));
        assertEquals("--", LayoutPolicyHelper.formatAntigravityHeader(null, true));
    }

    @Test
    public void testAntigravityHeaderBothProviders() {
        // Both Gemini and Claude present with decimal percents and refresh info
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                96, "Refreshes in 141h 33m",
                null, null,
                63.56, "Refreshes in 4h 30m",
                78.1, "Refreshes in 1h 32m"
        );
        // Exact format: "Ge 63.56%/5H - Re:4h30m 。 Cl 78.1%/5H - Re:1h32m"
        assertEquals("Ge 63.56%/5H - Re:4h30m 。 Cl 78.1%/5H - Re:1h32m",
                LayoutPolicyHelper.formatAntigravityHeader(au, false));
        // Compact mode: same (compact flag ignored for Ge/Cl split)
        assertEquals("Ge 63.56%/5H - Re:4h30m 。 Cl 78.1%/5H - Re:1h32m",
                LayoutPolicyHelper.formatAntigravityHeader(au, true));
    }

    @Test
    public void testAntigravityHeaderGeminiZeroPercent() {
        // Ge 0%/5H is still shown
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                96, "Refreshes in 141h 33m",
                null, null,
                0.0, "Refreshes in 1h 32m",
                63.56, "Refreshes in 4h 30m"
        );
        String result = LayoutPolicyHelper.formatAntigravityHeader(au, false);
        assertEquals("Ge 0%/5H - Re:1h32m 。 Cl 63.56%/5H - Re:4h30m", result);
    }

    @Test
    public void testAntigravityHeaderGeminiOnly() {
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                96, "Refreshes in 141h 33m",
                null, null,
                78.1, "Refreshes in 4m",
                null, null
        );
        assertEquals("Ge 78.1%/5H - Re:4m", LayoutPolicyHelper.formatAntigravityHeader(au, false));
    }

    @Test
    public void testAntigravityHeaderClaudeOnly() {
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                96, "Refreshes in 141h 33m",
                null, null,
                null, null,
                63.56, "Quota available"
        );
        assertEquals("Cl 63.56%/5H - Re:--", LayoutPolicyHelper.formatAntigravityHeader(au, false));
    }

    @Test
    public void testAntigravityHeaderFallbackToFiveHourWhenNoPerProvider() {
        // No Gemini or Claude per-provider data: fall back to generic five-hour bucket
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                90, "Refreshes in 5d", 100, "Refreshes in 4m");
        assertEquals("100%/5H - Re:4m", LayoutPolicyHelper.formatAntigravityHeader(au, false));
        assertEquals("100%/5H - Re:4m", LayoutPolicyHelper.formatAntigravityHeader(au, true));
    }

    @Test
    public void testAntigravityHeaderFallbackNoFiveHour() {
        // No Gemini/Claude, no five-hour either: return "--"
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                75, "Refreshes in 2d", null, null);
        assertEquals("--", LayoutPolicyHelper.formatAntigravityHeader(au, false));
        assertEquals("--", LayoutPolicyHelper.formatAntigravityHeader(au, true));
    }

    @Test
    public void testAntigravityHeaderNoRefreshText() {
        // Blank refresh text uses the explicit placeholder.
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                96, "",
                null, null,
                63.56, "",
                78.1, null
        );
        assertEquals("Ge 63.56%/5H - Re:-- 。 Cl 78.1%/5H - Re:--",
                LayoutPolicyHelper.formatAntigravityHeader(au, false));
    }

    @Test
    public void testAntigravityHeaderClaudeDisabled() {
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                50, "Refreshes in 5d",
                95, "Refreshes in 10m",
                95.0, "Refreshes in 10m",
                null, "disabled"
        );
        assertEquals("Ge 95%/5H - Re:10m 。 Cl disabled/5H",
                LayoutPolicyHelper.formatAntigravityHeader(au, false));
    }

    @Test
    public void testAntigravityHeaderGeminiDisabled() {
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                50, "Refreshes in 5d",
                95, "Refreshes in 10m",
                null, "disabled",
                78.1, "Refreshes in 1h 32m"
        );
        assertEquals("Ge disabled/5H 。 Cl 78.1%/5H - Re:1h32m",
                LayoutPolicyHelper.formatAntigravityHeader(au, false));
    }

    @Test
    public void testAntigravityHeaderMissingProviderNotDisabled() {
        // Gemini present, Claude missing (refresh text is empty, percent is null)
        DashboardState.AntigravityUsage au = new DashboardState.AntigravityUsage(
                50, "Refreshes in 5d",
                95, "Refreshes in 10m",
                95.0, "Refreshes in 10m",
                null, ""
        );
        // Claude block is not rendered and NOT shown as disabled
        assertEquals("Ge 95%/5H - Re:10m",
                LayoutPolicyHelper.formatAntigravityHeader(au, false));
    }
}
