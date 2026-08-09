package com.agentdeck.mobile;

import org.json.JSONException;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public final class DashboardStateTest {

    @Test
    public void formatsElapsedTimeCorrectly() {
        assertEquals("00:00:00", DashboardState.formatElapsed(0));
        assertEquals("00:02:31", DashboardState.formatElapsed(151));
        assertEquals("01:01:01", DashboardState.formatElapsed(3661));
        assertEquals("25:00:00", DashboardState.formatElapsed(90000));
    }

    @Test
    public void clampsNegativeElapsedTime() {
        assertEquals("00:00:00", DashboardState.formatElapsed(-5));
        assertEquals("00:00:00", DashboardState.formatElapsed(-100));
    }

    @Test
    public void parsesFullDashboardJson() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_001\",\n" +
                "    \"timestamp\": \"2026-08-07T12:00:00Z\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDeck\",\n" +
                "      \"conversation_name\": \"UI/UX Redesign\",\n" +
                "      \"status\": \"working\",\n" +
                "      \"message\": \"Executing layout overhaul\",\n" +
                "      \"elapsed\": 120,\n" +
                "      \"requires_action\": true,\n" +
                "      \"actions\": [\"approve\", \"reject\"],\n" +
                "      \"target_id\": \"target_abc\",\n" +
                "      \"conversation_tokens\": 12400,\n" +
                "      \"usage_remaining_percent\": 85\n" +
                "    }\n" +
                "  },\n" +
                "  \"projects\": [\n" +
                "    {\n" +
                "      \"id\": \"evt_001\",\n" +
                "      \"timestamp\": \"2026-08-07T12:00:00Z\",\n" +
                "      \"payload\": {\n" +
                "        \"agent\": \"codex\",\n" +
                "        \"project\": \"AgentDeck\",\n" +
                "        \"status\": \"working\",\n" +
                "        \"message\": \"Working\",\n" +
                "        \"elapsed\": 120\n" +
                "      }\n" +
                "    },\n" +
                "    {\n" +
                "      \"id\": \"evt_002\",\n" +
                "      \"timestamp\": \"2026-08-07T11:50:00Z\",\n" +
                "      \"payload\": {\n" +
                "        \"agent\": \"claude\",\n" +
                "        \"project\": \"BridgeServer\",\n" +
                "        \"status\": \"completed\",\n" +
                "        \"message\": \"Done\",\n" +
                "        \"elapsed\": 300\n" +
                "      }\n" +
                "    }\n" +
                "  ]\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertEquals("evt_001", state.current.eventId);
        assertEquals("codex", state.current.agent);
        assertEquals("AgentDeck", state.current.project);
        assertEquals("UI/UX Redesign", state.current.conversation);
        assertEquals("working", state.current.status);
        assertEquals("需要確認", state.current.statusLabel());
        assertEquals("Executing layout overhaul", state.current.message);
        assertEquals(120, state.current.elapsed);
        assertEquals("00:02:00", state.current.elapsedLabel());
        assertTrue(state.current.requiresAction);
        assertEquals(2, state.current.actions.size());
        assertEquals("target_abc", state.current.targetId);
        assertEquals(Integer.valueOf(12400), state.current.conversationTokens);
        assertEquals(Integer.valueOf(85), state.current.usageRemaining);

        assertEquals(2, state.projects.size());
        assertEquals("BridgeServer", state.projects.get(1).project);
        assertEquals("已完成", state.projects.get(1).statusLabel());
    }

    @Test
    public void mapsStatusLabelsToTraditionalChinese() throws JSONException {
        assertEquals("執行中", createAgentState("working", false).statusLabel());
        assertEquals("等待中", createAgentState("waiting", false).statusLabel());
        assertEquals("已完成", createAgentState("completed", false).statusLabel());
        assertEquals("錯誤", createAgentState("error", false).statusLabel());
        assertEquals("待命", createAgentState("unknown_status", false).statusLabel());
        assertEquals("需要確認", createAgentState("working", true).statusLabel());
    }

    @Test
    public void handlesNullAndMissingFieldsInJson() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_002\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"agent_test\",\n" +
                "      \"project\": \"MinimalProj\",\n" +
                "      \"status\": \"idle\"\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertEquals("MinimalProj", state.current.project);
        assertEquals("", state.current.conversation);
        assertEquals("", state.current.message);
        assertEquals(0, state.current.elapsed);
        assertFalse(state.current.requiresAction);
        assertNull(state.current.targetId);
        assertNull(state.current.usageRemaining);
        assertNull(state.current.steps);
        assertNull(state.current.currentStep);
        assertNull(state.current.codexUsage);
        assertNull(state.current.antigravityUsage);
        assertTrue(state.projects.isEmpty());
    }

    @Test
    public void parsesDualProviderUsageFromJson() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_dual_usage\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"idle\",\n" +
                "      \"codex_usage\": {\n" +
                "        \"weekly_remaining_percent\": 80,\n" +
                "        \"reset_text\": \"8/10 14:30\",\n" +
                "        \"reset_date\": \"8/10\",\n" +
                "        \"reset_available\": 0\n" +
                "      },\n" +
                "      \"antigravity_usage\": {\n" +
                "        \"weekly_remaining_percent\": 96,\n" +
                "        \"weekly_refresh_text\": \"Refreshes in 141h 33m\",\n" +
                "        \"five_hour_remaining_percent\": 78,\n" +
                "        \"five_hour_refresh_text\": \"Refreshes in 4m\"\n" +
                "      }\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertNotNull(state.current.codexUsage);
        assertEquals(Integer.valueOf(80), state.current.codexUsage.weeklyRemainingPercent);
        assertEquals("8/10 14:30", state.current.codexUsage.resetText);
        assertEquals("8/10", state.current.codexUsage.resetDate);
        assertEquals(Integer.valueOf(0), state.current.codexUsage.resetAvailable);

        assertNotNull(state.current.antigravityUsage);
        assertEquals(Integer.valueOf(96), state.current.antigravityUsage.weeklyRemainingPercent);
        assertEquals("Refreshes in 141h 33m", state.current.antigravityUsage.weeklyRefreshText);
        assertEquals(Integer.valueOf(78), state.current.antigravityUsage.fiveHourRemainingPercent);
        assertEquals("Refreshes in 4m", state.current.antigravityUsage.fiveHourRefreshText);
    }

    @Test
    public void parsesStepsAndCurrentStepFromJson() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_steps\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"working\",\n" +
                "      \"steps\": [\"Step 1 Title\", \"Step 2 Title\", \"Step 3 Title\"],\n" +
                "      \"current_step\": 2\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertNotNull(state.current.steps);
        assertEquals(3, state.current.steps.size());
        assertEquals("Step 1 Title", state.current.steps.get(0));
        assertEquals("Step 2 Title", state.current.steps.get(1));
        assertEquals(Integer.valueOf(2), state.current.currentStep);
    }

    @Test
    public void parsesRecentEventsFromJson() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_recent\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"completed\",\n" +
                "      \"message\": \"Task completed\",\n" +
                "      \"recent_events\": [\n" +
                "        {\"kind\": \"command\", \"label\": \"執行了指令\"},\n" +
                "        {\"kind\": \"reply\", \"label\": \"已回覆\", \"content\": \"單元測試已全部通過\"}\n" +
                "      ]\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertNotNull(state.current.recentEvents);
        assertEquals(2, state.current.recentEvents.size());
        assertEquals("command", state.current.recentEvents.get(0).kind);
        assertEquals("執行了指令", state.current.recentEvents.get(0).label);
        assertNull(state.current.recentEvents.get(0).content);
        assertEquals("reply", state.current.recentEvents.get(1).kind);
        assertEquals("已回覆", state.current.recentEvents.get(1).label);
        assertEquals("單元測試已全部通過", state.current.recentEvents.get(1).content);
    }

    @Test
    public void fallsBackToMessageWhenRecentEventsMissing() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_legacy\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"working\",\n" +
                "      \"message\": \"Legacy message fallback\"\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertNotNull(state.current.recentEvents);
        assertEquals(1, state.current.recentEvents.size());
        assertEquals("status", state.current.recentEvents.get(0).kind);
        assertEquals("Legacy message fallback", state.current.recentEvents.get(0).label);
        assertNull(state.current.recentEvents.get(0).content);
    }

    @Test
    public void fallsBackToMessageWhenRecentEventsIsEmptyArray() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_empty_events\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"working\",\n" +
                "      \"message\": \"Empty array fallback\",\n" +
                "      \"recent_events\": []\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertNotNull(state.current.recentEvents);
        assertEquals(1, state.current.recentEvents.size());
        assertEquals("status", state.current.recentEvents.get(0).kind);
        assertEquals("Empty array fallback", state.current.recentEvents.get(0).label);
        assertNull(state.current.recentEvents.get(0).content);
    }

    private DashboardState.AgentState createAgentState(String status, boolean requiresAction) throws JSONException {
        String json = String.format("{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_test\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"test_agent\",\n" +
                "      \"project\": \"test_project\",\n" +
                "      \"status\": \"%s\",\n" +
                "      \"requires_action\": %b\n" +
                "    }\n" +
                "  }\n" +
                "}", status, requiresAction);
        return DashboardState.fromJson(json).current;
    }

    @Test
    public void parsesModelsArrayFromJson() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_models\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"working\",\n" +
                "      \"models\": [\"Sol High\", \"Gemini 3.6 Flash\", \"Claude Sonnet 4.6\"]\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertNotNull(state.current.models);
        assertEquals(3, state.current.models.size());
        assertEquals("Sol High", state.current.models.get(0));
        assertEquals("Gemini 3.6 Flash", state.current.models.get(1));
        assertEquals("Claude Sonnet 4.6", state.current.models.get(2));
    }

    @Test
    public void modelsIsNullWhenAbsent() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_no_models\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"idle\"\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertNull(state.current.models);
    }

    @Test
    public void parsesAntigravityGeminiClaudePerProviderFields() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_ag_perp\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"idle\",\n" +
                "      \"antigravity_usage\": {\n" +
                "        \"weekly_remaining_percent\": 96,\n" +
                "        \"weekly_refresh_text\": \"Refreshes in 141h 33m\",\n" +
                "        \"gemini_five_hour_remaining_percent\": 63.56,\n" +
                "        \"gemini_five_hour_refresh_text\": \"Refreshes in 4h 30m\",\n" +
                "        \"claude_five_hour_remaining_percent\": 78.1,\n" +
                "        \"claude_five_hour_refresh_text\": \"Refreshes in 1h 32m\"\n" +
                "      }\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current);
        assertNotNull(state.current.antigravityUsage);
        DashboardState.AntigravityUsage au = state.current.antigravityUsage;
        assertEquals(Double.valueOf(63.56), au.geminiRemainingPercent);
        assertEquals("Refreshes in 4h 30m", au.geminiRefreshText);
        assertEquals(Double.valueOf(78.1), au.claudeRemainingPercent);
        assertEquals("Refreshes in 1h 32m", au.claudeRefreshText);
        assertEquals(Integer.valueOf(96), au.weeklyRemainingPercent);
    }

    @Test
    public void antigravityGeminiClaudeFieldsNullWhenAbsent() throws JSONException {
        String json = "{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_ag_no_perp\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"codex\",\n" +
                "      \"project\": \"AgentDesk\",\n" +
                "      \"status\": \"idle\",\n" +
                "      \"antigravity_usage\": {\n" +
                "        \"weekly_remaining_percent\": 80,\n" +
                "        \"weekly_refresh_text\": \"Refreshes in 2d\",\n" +
                "        \"five_hour_remaining_percent\": 50,\n" +
                "        \"five_hour_refresh_text\": \"Refreshes in 2h\"\n" +
                "      }\n" +
                "    }\n" +
                "  }\n" +
                "}";

        DashboardState state = DashboardState.fromJson(json);
        assertNotNull(state.current.antigravityUsage);
        DashboardState.AntigravityUsage au = state.current.antigravityUsage;
        assertNull(au.geminiRemainingPercent);
        assertNull(au.claudeRemainingPercent);
    }
}

