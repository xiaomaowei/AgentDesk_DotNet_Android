package com.agentdeck.mobile;

import org.junit.Test;

import static org.junit.Assert.assertTrue;

public final class ColorContrastTest {

    private static double getLuminance(int color) {
        double r = ((color >> 16) & 0xFF) / 255.0;
        double g = ((color >> 8) & 0xFF) / 255.0;
        double b = (color & 0xFF) / 255.0;

        r = (r <= 0.04045) ? r / 12.92 : Math.pow((r + 0.055) / 1.055, 2.4);
        g = (g <= 0.04045) ? g / 12.92 : Math.pow((g + 0.055) / 1.055, 2.4);
        b = (b <= 0.04045) ? b / 12.92 : Math.pow((b + 0.055) / 1.055, 2.4);

        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    public static double getContrastRatio(int color1, int color2) {
        double l1 = getLuminance(color1);
        double l2 = getLuminance(color2);
        double lighter = Math.max(l1, l2);
        double darker = Math.min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    @Test
    public void verifyAllForegroundBackgroundCombinationsMeetWCAGAA() {
        int surface = 0xFF1E293B;         // #1E293B Slate 800
        int surfaceAlt = 0xFF182232;     // #182232 Dark Slate
        int surfaceSelected = 0xFF1E3A6B; // #1E3A6B Selected Project BG

        int btnPrimaryBg = 0xFF1D4ED8;    // Deep Blue
        int btnRejectBg = 0xFF7F1D1D;     // Deep Red

        int textPrimary = 0xFFF8FAFC;     // High Contrast White/Slate
        int textMuted = 0xFFCBD5E1;       // Secondary Text
        int textDim = 0xFF94A3B8;         // Subdued Text
        int colorWhite = 0xFFFFFFFF;      // Pure White Button Text
        int colorBlueText = 0xFF4D9CFF;   // Status/Border/Metric Blue
        int colorCyan = 0xFF23D0F3;       // Accent Cyan
        int colorGreen = 0xFF34D399;      // Success Emerald
        int colorAmber = 0xFFFBBF24;      // Warning Amber
        int colorRedText = 0xFFFF6B7A;    // Danger Ruby Text
        int colorPurpleText = 0xFFC084FC; // Purple Agent Metric
        int badgeAgentBg = 0xFF271838;    // #271838 Dark Purple Badge BG

        assertContrastAtLeast(colorWhite, btnPrimaryBg, 4.5, "White on Primary Button BG");
        assertContrastAtLeast(colorWhite, btnRejectBg, 4.5, "White on Reject Button BG");
        assertContrastAtLeast(textPrimary, surface, 4.5, "TEXT_PRIMARY on SURFACE");
        assertContrastAtLeast(textMuted, surface, 4.5, "TEXT_MUTED on SURFACE");
        assertContrastAtLeast(textDim, surfaceAlt, 4.5, "TEXT_DIM on SURFACE_ALT");
        assertContrastAtLeast(colorBlueText, surface, 4.5, "COLOR_BLUE_TEXT on SURFACE");
        assertContrastAtLeast(colorRedText, surface, 4.5, "COLOR_RED_TEXT on SURFACE");
        assertContrastAtLeast(colorPurpleText, surfaceAlt, 4.5, "COLOR_PURPLE_TEXT on SURFACE_ALT");
        assertContrastAtLeast(colorPurpleText, badgeAgentBg, 4.5, "COLOR_PURPLE_TEXT on BADGE_AGENT_BG");
        assertContrastAtLeast(colorCyan, surfaceSelected, 4.5, "COLOR_CYAN on SURFACE_SELECTED");
        assertContrastAtLeast(colorCyan, surfaceAlt, 4.5, "COLOR_CYAN on SURFACE_ALT");
        assertContrastAtLeast(colorGreen, surface, 4.5, "COLOR_GREEN on SURFACE");
        assertContrastAtLeast(colorAmber, surface, 4.5, "COLOR_AMBER on SURFACE");
    }

    private void assertContrastAtLeast(int fg, int bg, double expectedMin, String label) {
        double ratio = getContrastRatio(fg, bg);
        assertTrue(label + " contrast ratio " + String.format("%.2f", ratio) + ":1 is less than expected " + expectedMin + ":1",
                ratio >= expectedMin);
    }
}
