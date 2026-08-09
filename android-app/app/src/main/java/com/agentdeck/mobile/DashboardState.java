package com.agentdeck.mobile;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;

public final class DashboardState {
    public static final DashboardState EMPTY = new DashboardState(null, Collections.emptyList());

    public final AgentState current;
    public final List<AgentState> projects;

    private DashboardState(AgentState current, List<AgentState> projects) {
        this.current = current;
        this.projects = Collections.unmodifiableList(projects);
    }

    public static DashboardState fromJson(String json) throws JSONException {
        JSONObject root = new JSONObject(json);
        AgentState current = root.isNull("current") ? null : parseEnvelope(root.getJSONObject("current"));
        JSONArray projectArray = root.optJSONArray("projects");
        List<AgentState> projects = new ArrayList<>();
        if (projectArray != null) {
            for (int i = 0; i < projectArray.length(); i++) {
                projects.add(parseEnvelope(projectArray.getJSONObject(i)));
            }
        }
        return new DashboardState(current, projects);
    }

    public static final class CodexUsage {
        public final Integer weeklyRemainingPercent;
        public final String resetText;
        public final String resetDate;
        public final Integer resetAvailable;

        public CodexUsage(Integer weeklyRemainingPercent, String resetText, String resetDate, Integer resetAvailable) {
            this.weeklyRemainingPercent = weeklyRemainingPercent;
            this.resetText = resetText;
            this.resetDate = resetDate;
            this.resetAvailable = resetAvailable;
        }

        public static CodexUsage fromJson(JSONObject obj) {
            if (obj == null) return null;
            Integer weekly = obj.has("weekly_remaining_percent") && !obj.isNull("weekly_remaining_percent")
                    ? obj.optInt("weekly_remaining_percent") : null;
            String resetText = obj.optString("reset_text", "");
            String resetDate = obj.optString("reset_date", "");
            Integer available = obj.has("reset_available") && !obj.isNull("reset_available")
                    ? obj.optInt("reset_available") : null;
            return new CodexUsage(weekly, resetText, resetDate, available);
        }
    }

    public static final class AntigravityUsage {
        public final Integer weeklyRemainingPercent;
        public final String weeklyRefreshText;
        public final Integer fiveHourRemainingPercent;
        public final String fiveHourRefreshText;
        /** Gemini 5-hour remaining percent as a JSON number (may have decimals). */
        public final Double geminiRemainingPercent;
        public final String geminiRefreshText;
        /** Claude 5-hour remaining percent as a JSON number (may have decimals). */
        public final Double claudeRemainingPercent;
        public final String claudeRefreshText;

        public AntigravityUsage(Integer weeklyRemainingPercent, String weeklyRefreshText,
                                Integer fiveHourRemainingPercent, String fiveHourRefreshText) {
            this(weeklyRemainingPercent, weeklyRefreshText, fiveHourRemainingPercent, fiveHourRefreshText,
                    null, null, null, null);
        }

        public AntigravityUsage(Integer weeklyRemainingPercent, String weeklyRefreshText,
                                Integer fiveHourRemainingPercent, String fiveHourRefreshText,
                                Double geminiRemainingPercent, String geminiRefreshText,
                                Double claudeRemainingPercent, String claudeRefreshText) {
            this.weeklyRemainingPercent = weeklyRemainingPercent;
            this.weeklyRefreshText = weeklyRefreshText;
            this.fiveHourRemainingPercent = fiveHourRemainingPercent;
            this.fiveHourRefreshText = fiveHourRefreshText;
            this.geminiRemainingPercent = geminiRemainingPercent;
            this.geminiRefreshText = geminiRefreshText;
            this.claudeRemainingPercent = claudeRemainingPercent;
            this.claudeRefreshText = claudeRefreshText;
        }

        public static AntigravityUsage fromJson(JSONObject obj) {
            if (obj == null) return null;
            Integer weekly = obj.has("weekly_remaining_percent") && !obj.isNull("weekly_remaining_percent")
                    ? obj.optInt("weekly_remaining_percent") : null;
            String weeklyRefresh = obj.optString("weekly_refresh_text", "");
            Integer fiveHour = obj.has("five_hour_remaining_percent") && !obj.isNull("five_hour_remaining_percent")
                    ? obj.optInt("five_hour_remaining_percent") : null;
            String fiveHourRefresh = obj.optString("five_hour_refresh_text", "");
            Double geminiPct = readPercent(obj, "gemini_five_hour_remaining_percent");
            String geminiRefresh = obj.optString("gemini_five_hour_refresh_text", "");
            Double claudePct = readPercent(obj, "claude_five_hour_remaining_percent");
            String claudeRefresh = obj.optString("claude_five_hour_refresh_text", "");
            return new AntigravityUsage(weekly, weeklyRefresh, fiveHour, fiveHourRefresh,
                    geminiPct, geminiRefresh, claudePct, claudeRefresh);
        }

        private static Double readPercent(JSONObject obj, String key) {
            if (!obj.has(key) || obj.isNull(key)) return null;
            Object raw = obj.opt(key);
            if (raw instanceof Number) return ((Number) raw).doubleValue();
            if (raw instanceof String) {
                try {
                    return Double.parseDouble((String) raw);
                } catch (NumberFormatException ignored) {
                    return null;
                }
            }
            return null;
        }
    }

    public static final class RecentEvent {
        public final String kind;
        public final String label;
        public final String content;

        public RecentEvent(String kind, String label, String content) {
            this.kind = kind != null ? kind : "status";
            this.label = label != null ? label : "";
            this.content = content;
        }

        public static RecentEvent fromJson(JSONObject obj) {
            if (obj == null) return null;
            String kind = obj.optString("kind", "status");
            String label = obj.optString("label", "");
            String content = obj.isNull("content") ? null : obj.optString("content", null);
            return new RecentEvent(kind, label, content);
        }
    }

    private static AgentState parseEnvelope(JSONObject envelope) throws JSONException {
        JSONObject payload = envelope.getJSONObject("payload");
        List<String> actions = new ArrayList<>();
        JSONArray actionArray = payload.optJSONArray("actions");
        if (actionArray != null) {
            for (int i = 0; i < actionArray.length(); i++) {
                actions.add(actionArray.getString(i));
            }
        }
        Integer usageRemaining = payload.has("usage_remaining_percent") && !payload.isNull("usage_remaining_percent")
                ? payload.getInt("usage_remaining_percent") : null;
        Integer conversationTokens = payload.has("conversation_tokens") && !payload.isNull("conversation_tokens")
                ? payload.getInt("conversation_tokens") : null;
        List<String> steps = null;
        if (payload.has("steps") && !payload.isNull("steps")) {
            JSONArray stepArray = payload.optJSONArray("steps");
            if (stepArray != null) {
                steps = new ArrayList<>();
                for (int i = 0; i < stepArray.length(); i++) {
                    steps.add(stepArray.getString(i));
                }
            }
        }
        Integer currentStep = payload.has("current_step") && !payload.isNull("current_step")
                ? payload.getInt("current_step") : null;
        List<RecentEvent> recentEvents = null;
        if (payload.has("recent_events") && !payload.isNull("recent_events")) {
            JSONArray evArray = payload.optJSONArray("recent_events");
            if (evArray != null) {
                recentEvents = new ArrayList<>();
                for (int i = 0; i < evArray.length(); i++) {
                    RecentEvent ev = RecentEvent.fromJson(evArray.optJSONObject(i));
                    if (ev != null) {
                        recentEvents.add(ev);
                    }
                }
            }
        }
        List<String> models = null;
        if (payload.has("models") && !payload.isNull("models")) {
            JSONArray modelArray = payload.optJSONArray("models");
            if (modelArray != null && modelArray.length() > 0) {
                models = new ArrayList<>();
                for (int i = 0; i < modelArray.length(); i++) {
                    String m = modelArray.optString(i, null);
                    if (m != null && !m.isEmpty()) models.add(m);
                }
            }
        }
        CodexUsage codexUsage = CodexUsage.fromJson(payload.optJSONObject("codex_usage"));
        AntigravityUsage antigravityUsage = AntigravityUsage.fromJson(payload.optJSONObject("antigravity_usage"));
        return new AgentState(
                envelope.optString("id", ""),
                envelope.optString("timestamp", ""),
                payload.getString("agent"),
                payload.getString("project"),
                payload.optString("conversation_name", ""),
                payload.getString("status"),
                payload.optString("message", ""),
                payload.optInt("elapsed", 0),
                payload.optBoolean("requires_action", false),
                actions,
                payload.isNull("target_id") ? null : payload.optString("target_id", null),
                conversationTokens,
                usageRemaining,
                steps,
                currentStep,
                codexUsage,
                antigravityUsage,
                recentEvents,
                models
        );
    }

    public static final class AgentState {
        public final String eventId;
        public final String timestamp;
        public final String agent;
        public final String project;
        public final String conversation;
        public final String status;
        public final String message;
        public final int elapsed;
        public final boolean requiresAction;
        public final List<String> actions;
        public final String targetId;
        public final Integer conversationTokens;
        public final Integer usageRemaining;
        public final List<String> steps;
        public final Integer currentStep;
        public final CodexUsage codexUsage;
        public final AntigravityUsage antigravityUsage;
        public final List<RecentEvent> recentEvents;
        /** Ordered, de-duped list of model labels used in this session (may be null/empty). */
        public final List<String> models;

        AgentState(
                String eventId,
                String timestamp,
                String agent,
                String project,
                String conversation,
                String status,
                String message,
                int elapsed,
                boolean requiresAction,
                List<String> actions,
                String targetId,
                Integer conversationTokens,
                Integer usageRemaining
        ) {
            this(eventId, timestamp, agent, project, conversation, status, message, elapsed,
                    requiresAction, actions, targetId, conversationTokens, usageRemaining, null, null, null, null, null, null);
        }

        AgentState(
                String eventId,
                String timestamp,
                String agent,
                String project,
                String conversation,
                String status,
                String message,
                int elapsed,
                boolean requiresAction,
                List<String> actions,
                String targetId,
                Integer conversationTokens,
                Integer usageRemaining,
                List<String> steps,
                Integer currentStep
        ) {
            this(eventId, timestamp, agent, project, conversation, status, message, elapsed,
                    requiresAction, actions, targetId, conversationTokens, usageRemaining, steps, currentStep, null, null, null, null);
        }

        AgentState(
                String eventId,
                String timestamp,
                String agent,
                String project,
                String conversation,
                String status,
                String message,
                int elapsed,
                boolean requiresAction,
                List<String> actions,
                String targetId,
                Integer conversationTokens,
                Integer usageRemaining,
                List<String> steps,
                Integer currentStep,
                CodexUsage codexUsage,
                AntigravityUsage antigravityUsage
        ) {
            this(eventId, timestamp, agent, project, conversation, status, message, elapsed,
                    requiresAction, actions, targetId, conversationTokens, usageRemaining, steps, currentStep, codexUsage, antigravityUsage, null, null);
        }

        AgentState(
                String eventId,
                String timestamp,
                String agent,
                String project,
                String conversation,
                String status,
                String message,
                int elapsed,
                boolean requiresAction,
                List<String> actions,
                String targetId,
                Integer conversationTokens,
                Integer usageRemaining,
                List<String> steps,
                Integer currentStep,
                CodexUsage codexUsage,
                AntigravityUsage antigravityUsage,
                List<RecentEvent> recentEvents
        ) {
            this(eventId, timestamp, agent, project, conversation, status, message, elapsed,
                    requiresAction, actions, targetId, conversationTokens, usageRemaining, steps, currentStep, codexUsage, antigravityUsage, recentEvents, null);
        }

        AgentState(
                String eventId,
                String timestamp,
                String agent,
                String project,
                String conversation,
                String status,
                String message,
                int elapsed,
                boolean requiresAction,
                List<String> actions,
                String targetId,
                Integer conversationTokens,
                Integer usageRemaining,
                List<String> steps,
                Integer currentStep,
                CodexUsage codexUsage,
                AntigravityUsage antigravityUsage,
                List<RecentEvent> recentEvents,
                List<String> models
        ) {
            this.eventId = eventId;
            this.timestamp = timestamp;
            this.agent = agent;
            this.project = project;
            this.conversation = conversation;
            this.status = status;
            this.message = message;
            this.elapsed = elapsed;
            this.requiresAction = requiresAction;
            this.actions = Collections.unmodifiableList(actions);
            this.targetId = targetId;
            this.conversationTokens = conversationTokens;
            this.usageRemaining = usageRemaining;
            this.steps = steps != null ? Collections.unmodifiableList(steps) : null;
            this.currentStep = currentStep;
            this.codexUsage = codexUsage;
            this.antigravityUsage = antigravityUsage;
            if (recentEvents != null && !recentEvents.isEmpty()) {
                this.recentEvents = Collections.unmodifiableList(recentEvents);
            } else {
                this.recentEvents = Collections.singletonList(new RecentEvent("status", message != null ? message : "", null));
            }
            this.models = (models != null && !models.isEmpty()) ? Collections.unmodifiableList(models) : null;
        }

        public String statusLabel() {
            if (requiresAction) return "需要確認";
            switch (status) {
                case "working": return "執行中";
                case "waiting": return "等待中";
                case "completed": return "已完成";
                case "error": return "錯誤";
                default: return "待命";
            }
        }

        public String elapsedLabel() {
            return formatElapsed(elapsed);
        }
    }

    static String formatElapsed(int totalSeconds) {
        int safe = Math.max(0, totalSeconds);
        int hours = safe / 3600;
        int minutes = (safe % 3600) / 60;
        int seconds = safe % 60;
        return String.format(Locale.US, "%02d:%02d:%02d", hours, minutes, seconds);
    }
}
