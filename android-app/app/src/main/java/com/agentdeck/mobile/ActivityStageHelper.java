package com.agentdeck.mobile;

public final class ActivityStageHelper {

    private ActivityStageHelper() {}

    @FunctionalInterface
    public interface TextMeasurer {
        float measureText(String text);
    }

    public static int totalSteps(DashboardState.AgentState value) {
        if (value != null && value.steps != null && !value.steps.isEmpty()) {
            return value.steps.size();
        }
        return 1;
    }

    public static int currentStage(DashboardState.AgentState value) {
        if (value != null && value.steps != null && !value.steps.isEmpty()) {
            int size = value.steps.size();
            if (value.currentStep != null) {
                return Math.max(1, Math.min(value.currentStep, size));
            }
            if ("completed".equals(value.status)) return size;
            return 1;
        }
        return 1;
    }

    public static String progressTitle(DashboardState.AgentState value) {
        if (value == null) {
            return "等待進度";
        }
        if (value.steps != null && !value.steps.isEmpty()) {
            int stage = currentStage(value);
            return value.steps.get(stage - 1);
        }
        if (value.message != null && !value.message.isBlank()) {
            return value.message;
        }
        return value.statusLabel();
    }

    public static String truncateText(String text, float maxWidth, TextMeasurer measurer) {
        if (text == null) return "";
        if (maxWidth <= 0 || measurer == null) return text;
        if (measurer.measureText(text) <= maxWidth) {
            return text;
        }
        String ellipsis = "…";
        float ellipsisWidth = measurer.measureText(ellipsis);
        if (ellipsisWidth >= maxWidth) {
            return ellipsis;
        }
        float targetWidth = maxWidth - ellipsisWidth;
        int low = 0;
        int high = text.length();
        int best = 0;
        while (low <= high) {
            int mid = (low + high) >>> 1;
            String sub = text.substring(0, mid);
            if (measurer.measureText(sub) <= targetWidth) {
                best = mid;
                low = mid + 1;
            } else {
                high = mid - 1;
            }
        }
        return text.substring(0, best) + ellipsis;
    }
}



