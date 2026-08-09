package com.agentdeck.mobile;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class BridgeClientTest {

    @Test
    public void parsesAcceptedActionResultAsTrue() {
        String json = "{\n" +
                "  \"version\": \"1.0\",\n" +
                "  \"type\": \"action_result\",\n" +
                "  \"id\": \"res_123\",\n" +
                "  \"timestamp\": \"2026-08-07T12:00:00Z\",\n" +
                "  \"payload\": {\n" +
                "    \"action_id\": \"act_456\",\n" +
                "    \"accepted\": true,\n" +
                "    \"message\": \"Action queued\"\n" +
                "  }\n" +
                "}";
        assertTrue(BridgeClient.parseActionResult(json));
    }

    @Test
    public void parsesRejectedActionResultAsFalse() {
        String json = "{\n" +
                "  \"version\": \"1.0\",\n" +
                "  \"type\": \"action_result\",\n" +
                "  \"id\": \"res_124\",\n" +
                "  \"timestamp\": \"2026-08-07T12:00:00Z\",\n" +
                "  \"payload\": {\n" +
                "    \"action_id\": \"act_457\",\n" +
                "    \"accepted\": false,\n" +
                "    \"message\": \"Action denied by operator\"\n" +
                "  }\n" +
                "}";
        assertFalse(BridgeClient.parseActionResult(json));
    }

    @Test
    public void handlesMalformedAndMissingFieldsInActionResult() {
        assertFalse(BridgeClient.parseActionResult(null));
        assertFalse(BridgeClient.parseActionResult(""));
        assertFalse(BridgeClient.parseActionResult("not a json string"));
        assertFalse(BridgeClient.parseActionResult("{}"));
        assertFalse(BridgeClient.parseActionResult("{\"payload\": {}}"));
        assertFalse(BridgeClient.parseActionResult("{\"payload\": {\"accepted\": \"invalid\"}}"));
    }
}
