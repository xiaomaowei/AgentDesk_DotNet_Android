package com.agentdeck.mobile;

import java.util.Locale;

public final class TokenFormatter {
    private TokenFormatter() {}

    public static String formatCompact(Integer tokens) {
        if (tokens == null || tokens < 0) {
            return "--";
        }
        if (tokens < 1000) {
            return String.valueOf(tokens);
        }
        if (tokens < 1_000_000) {
            double val = tokens / 1000.0;
            if (tokens % 1000 == 0 || (int) (val * 10) % 10 == 0) {
                return String.format(Locale.US, "%.0fK", val);
            }
            return String.format(Locale.US, "%.1fK", val);
        }
        double val = tokens / 1_000_000.0;
        if (tokens % 1_000_000 == 0 || (int) (val * 10) % 10 == 0) {
            return String.format(Locale.US, "%.0fM", val);
        }
        return String.format(Locale.US, "%.1fM", val);
    }

    public static String formatFull(Integer tokens) {
        if (tokens == null || tokens < 0) {
            return "--";
        }
        return String.format(Locale.US, "%,d", tokens);
    }
}
