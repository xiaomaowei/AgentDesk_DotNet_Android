package com.agentdeck.mobile;

import org.json.JSONException;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertTrue;

public final class ActivityStageHelperTest {

    private DashboardState.AgentState createAgentState(String status, String message, boolean requiresAction) throws JSONException {
        String json = String.format("{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_test\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"test_agent\",\n" +
                "      \"project\": \"test_project\",\n" +
                "      \"status\": \"%s\",\n" +
                "      \"message\": \"%s\",\n" +
                "      \"requires_action\": %b\n" +
                "    }\n" +
                "  }\n" +
                "}", status, message == null ? "" : message, requiresAction);
        return DashboardState.fromJson(json).current;
    }

    private DashboardState.AgentState createAgentStateWithSteps(String status, String stepsJson, Integer currentStep) throws JSONException {
        String currStepStr = currentStep == null ? "null" : currentStep.toString();
        String json = String.format("{\n" +
                "  \"current\": {\n" +
                "    \"id\": \"evt_test\",\n" +
                "    \"payload\": {\n" +
                "      \"agent\": \"test_agent\",\n" +
                "      \"project\": \"test_project\",\n" +
                "      \"status\": \"%s\",\n" +
                "      \"steps\": %s,\n" +
                "      \"current_step\": %s\n" +
                "    }\n" +
                "  }\n" +
                "}", status, stepsJson, currStepStr);
        return DashboardState.fromJson(json).current;
    }

    @Test
    public void nullStateFallback() {
        assertEquals(1, ActivityStageHelper.totalSteps(null));
        assertEquals(1, ActivityStageHelper.currentStage(null));
        assertEquals("等待進度", ActivityStageHelper.progressTitle(null));
    }

    @Test
    public void noStepsFallbackWithEventSummaryMessage() throws JSONException {
        DashboardState.AgentState state = createAgentState("working", "Executing build step", false);
        assertEquals(1, ActivityStageHelper.totalSteps(state));
        assertEquals(1, ActivityStageHelper.currentStage(state));
        assertEquals("Executing build step", ActivityStageHelper.progressTitle(state));
    }

    @Test
    public void noStepsFallbackWithBlankMessageUsesStatusLabel() throws JSONException {
        DashboardState.AgentState workingState = createAgentState("working", "", false);
        assertEquals(1, ActivityStageHelper.totalSteps(workingState));
        assertEquals(1, ActivityStageHelper.currentStage(workingState));
        assertEquals("執行中", ActivityStageHelper.progressTitle(workingState));

        DashboardState.AgentState completedState = createAgentState("completed", "   ", false);
        assertEquals(1, ActivityStageHelper.totalSteps(completedState));
        assertEquals(1, ActivityStageHelper.currentStage(completedState));
        assertEquals("已完成", ActivityStageHelper.progressTitle(completedState));

        DashboardState.AgentState errorState = createAgentState("error", "", false);
        assertEquals(1, ActivityStageHelper.totalSteps(errorState));
        assertEquals(1, ActivityStageHelper.currentStage(errorState));
        assertEquals("錯誤", ActivityStageHelper.progressTitle(errorState));

        DashboardState.AgentState waitingState = createAgentState("waiting", "", false);
        assertEquals(1, ActivityStageHelper.totalSteps(waitingState));
        assertEquals(1, ActivityStageHelper.currentStage(waitingState));
        assertEquals("等待中", ActivityStageHelper.progressTitle(waitingState));

        DashboardState.AgentState actionState = createAgentState("working", "", true);
        assertEquals(1, ActivityStageHelper.totalSteps(actionState));
        assertEquals(1, ActivityStageHelper.currentStage(actionState));
        assertEquals("需要確認", ActivityStageHelper.progressTitle(actionState));
    }

    @Test
    public void dynamicStepsTwoSteps() throws JSONException {
        DashboardState.AgentState state = createAgentStateWithSteps("working", "[\"Init\", \"Deploy\"]", 2);
        assertEquals(2, ActivityStageHelper.totalSteps(state));
        assertEquals(2, ActivityStageHelper.currentStage(state));
        assertEquals("Deploy", ActivityStageHelper.progressTitle(state));
    }

    @Test
    public void dynamicStepsFourSteps() throws JSONException {
        DashboardState.AgentState state = createAgentStateWithSteps("working", "[\"Step1\", \"Step2\", \"Step3\", \"Step4\"]", 3);
        assertEquals(4, ActivityStageHelper.totalSteps(state));
        assertEquals(3, ActivityStageHelper.currentStage(state));
        assertEquals("Step3", ActivityStageHelper.progressTitle(state));
    }

    @Test
    public void dynamicStepsArbitraryCountAndBoundaries() throws JSONException {
        // 5 steps, current_step high out-of-bounds -> clamped to 5
        DashboardState.AgentState highState = createAgentStateWithSteps("working", "[\"S1\", \"S2\", \"S3\", \"S4\", \"S5\"]", 99);
        assertEquals(5, ActivityStageHelper.totalSteps(highState));
        assertEquals(5, ActivityStageHelper.currentStage(highState));
        assertEquals("S5", ActivityStageHelper.progressTitle(highState));

        // 5 steps, current_step low out-of-bounds -> clamped to 1
        DashboardState.AgentState lowState = createAgentStateWithSteps("working", "[\"S1\", \"S2\", \"S3\", \"S4\", \"S5\"]", -5);
        assertEquals(5, ActivityStageHelper.totalSteps(lowState));
        assertEquals(1, ActivityStageHelper.currentStage(lowState));
        assertEquals("S1", ActivityStageHelper.progressTitle(lowState));

        // 3 steps, current_step null, status completed -> returns step 3
        DashboardState.AgentState completedState = createAgentStateWithSteps("completed", "[\"A\", \"B\", \"C\"]", null);
        assertEquals(3, ActivityStageHelper.totalSteps(completedState));
        assertEquals(3, ActivityStageHelper.currentStage(completedState));
        assertEquals("C", ActivityStageHelper.progressTitle(completedState));
    }

    @Test
    public void truncateTextLeavesShortTextIntact() {
        String shortText = "檢查進度";
        String result = ActivityStageHelper.truncateText(shortText, 200f, text -> text.length() * 10f);
        assertEquals("檢查進度", result);
    }

    @Test
    public void truncateTextEllipsizesLongTextSafely() {
        String longText = "檢查 AgentDeck Android 現況、相關版面與震動邏輯";
        String result = ActivityStageHelper.truncateText(longText, 100f, text -> text.length() * 10f);
        assertEquals("檢查 AgentD…", result);
        assertTrue(result.endsWith("…"));
        assertTrue(result.length() * 10f <= 100f);
    }
}




