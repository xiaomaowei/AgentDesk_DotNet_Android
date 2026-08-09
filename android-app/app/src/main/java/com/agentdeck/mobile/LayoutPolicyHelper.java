package com.agentdeck.mobile;

import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.List;
import java.util.Locale;

public final class LayoutPolicyHelper {
    private LayoutPolicyHelper() {}

    public static String formatButtonText(String action, float fontScale, boolean isPending) {
        if (isPending) {
            if ("approve".equals(action) || "reject".equals(action)) return "提交中...";
            return "";
        }
        if ("approve".equals(action)) return "核准";
        if ("reject".equals(action)) return "拒絕";
        return "";
    }

    public static float calculateFontSize(float baseSp, float fontScale) {
        float scaled = baseSp * fontScale;
        return Math.min(scaled, baseSp * 2.2f);
    }

    public static String formatAgentStatusLabel(List<String> models) {
        if (models == null || models.isEmpty()) return "目前 Agent 狀態";
        StringBuilder modelLabel = new StringBuilder();
        for (String model : models) {
            if (model != null && !model.isBlank()) {
                if (modelLabel.length() > 0) modelLabel.append('、');
                modelLabel.append(model);
            }
        }
        return modelLabel.length() == 0
                ? "目前 Agent 狀態"
                : "目前 Agent 狀態\n" + modelLabel;
    }

    public static String formatCodexHeader(DashboardState.CodexUsage usage, boolean compact) {
        if (usage == null || usage.weeklyRemainingPercent == null) {
            return "--";
        }
        String resetStr = (usage.resetText != null && !usage.resetText.isBlank()) ? " (" + usage.resetText + ")" : "";
        return usage.weeklyRemainingPercent + "%" + resetStr;
    }

    /**
     * Converts "Refreshes in 1h 32m" -> "Re:1h32m",
     *          "Refreshes in 4m"     -> "Re:4m",
     *          "Quota available"     -> "Re:--",
     *          null / blank          -> "Re:--"
     */
    static String formatRefreshText(String raw) {
        if (raw == null || raw.isBlank()) return "Re:--";
        String trimmed = raw.trim();
        // "Quota available" or similar non-time values
        if (trimmed.equalsIgnoreCase("quota available")) return "Re:--";
        // Match "Refreshes in <duration>" case-insensitively
        Pattern p = Pattern.compile(
            "(?:refreshes?\\s+in\\s+)(\\d+(?:\\s*[dhm])(?:\\s*\\d+(?:\\s*[dhm]))*)$",
            Pattern.CASE_INSENSITIVE
        );
        Matcher m = p.matcher(trimmed);
        if (m.find()) {
            // Strip all whitespace inside the duration: "1h 32m" -> "1h32m"
            String duration = m.group(1).replaceAll("\\s+", "");
            return "Re:" + duration;
        }
        return "Re:--";
    }

    /**
     * Formats the ANTIGRAVITY usage header showing Gemini and Claude five-hour buckets only.
     *
     * Target format: "Ge 0%/5H - Re:1h32m 。 Cl 63.56%/5H - Re:4h30m"
     *
     * Rules:
     * - Show Ge block only if geminiRemainingPercent is non-null.
     * - Show Cl block only if claudeRemainingPercent is non-null.
     * - If neither is available, fall back to the first five-hour bucket, or "--".
     * - Never display weekly usage.
     * - The separator " 。 " is used between the two blocks.
     */
    public static String formatAntigravityHeader(DashboardState.AntigravityUsage usage, boolean compact) {
        if (usage == null) return "--";

        StringBuilder sb = new StringBuilder();
        boolean isGeminiDisabled = usage.geminiRemainingPercent == null && isDisabled(usage.geminiRefreshText);
        boolean isClaudeDisabled = usage.claudeRemainingPercent == null && isDisabled(usage.claudeRefreshText);

        boolean hasGemini = usage.geminiRemainingPercent != null || isGeminiDisabled;
        boolean hasClaude = usage.claudeRemainingPercent != null || isClaudeDisabled;

        if (hasGemini) {
            if (usage.geminiRemainingPercent != null) {
                sb.append("Ge ").append(formatPercent(usage.geminiRemainingPercent)).append("%/5H");
                String re = formatRefreshText(usage.geminiRefreshText);
                sb.append(" - ").append(re);
            } else {
                sb.append("Ge disabled/5H");
            }
        }

        if (hasClaude) {
            if (sb.length() > 0) sb.append(" 。 ");
            if (usage.claudeRemainingPercent != null) {
                sb.append("Cl ").append(formatPercent(usage.claudeRemainingPercent)).append("%/5H");
                String re = formatRefreshText(usage.claudeRefreshText);
                sb.append(" - ").append(re);
            } else {
                sb.append("Cl disabled/5H");
            }
        }

        if (sb.length() == 0) {
            // Fallback: use the first five-hour bucket if available
            if (usage.fiveHourRemainingPercent != null) {
                sb.append(usage.fiveHourRemainingPercent).append("%/5H");
                String re = formatRefreshText(usage.fiveHourRefreshText);
                sb.append(" - ").append(re);
            } else {
                return "--";
            }
        }

        return sb.toString();
    }

    private static boolean isDisabled(String text) {
        return text != null && text.trim().equalsIgnoreCase("disabled");
    }

    private static String formatPercent(Double value) {
        if (value == null) return "";
        return String.format(Locale.ROOT, "%.2f", value)
                .replaceAll("0+$", "")
                .replaceAll("\\.$", "");
    }
}
