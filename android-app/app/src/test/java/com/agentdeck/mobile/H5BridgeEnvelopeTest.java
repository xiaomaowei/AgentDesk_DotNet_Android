package com.agentdeck.mobile;

import org.json.JSONObject;
import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

/**
 * Unit tests for dashboard envelope construction.
 *
 * Tests {@link H5MessageValidator#buildDashboardEnvelope} which is the
 * pure (no Android deps) implementation used by {@link H5Bridge}.
 *
 * Covers:
 * <ul>
 *   <li>Envelope construction with valid dashboard JSON</li>
 *   <li>Connected flag propagated correctly</li>
 *   <li>Raw dashboard JSON embedded as a JSON object (not a string)</li>
 *   <li>Special characters in dashboard values correctly escaped</li>
 *   <li>Null / blank / malformed inputs return null</li>
 * </ul>
 */
public final class H5BridgeEnvelopeTest {

    @Test
    public void buildsEnvelopeWithCorrectType() throws Exception {
        String dashboard = minimalDashboard();
        String envelope = H5MessageValidator.buildDashboardEnvelope(dashboard, true);
        assertNotNull(envelope);
        JSONObject obj = new JSONObject(envelope);
        assertEquals("dashboard", obj.getString("type"));
    }

    @Test
    public void propagatesConnectedTrue() throws Exception {
        String envelope = H5MessageValidator.buildDashboardEnvelope(minimalDashboard(), true);
        assertNotNull(envelope);
        JSONObject obj = new JSONObject(envelope);
        assertTrue(obj.getBoolean("connected"));
    }

    @Test
    public void propagatesConnectedFalse() throws Exception {
        String envelope = H5MessageValidator.buildDashboardEnvelope(minimalDashboard(), false);
        assertNotNull(envelope);
        JSONObject obj = new JSONObject(envelope);
        assertEquals(false, obj.getBoolean("connected"));
    }

    @Test
    public void dashboardIsJsonObjectNotString() throws Exception {
        String dashboard = minimalDashboard();
        String envelope = H5MessageValidator.buildDashboardEnvelope(dashboard, true);
        assertNotNull(envelope);
        JSONObject obj = new JSONObject(envelope);
        // "dashboard" value must be an object, not a string
        Object dashValue = obj.get("dashboard");
        assertTrue("dashboard value must be a JSONObject, got: " + dashValue.getClass().getSimpleName(),
                dashValue instanceof JSONObject);
    }

    @Test
    public void dashboardObjectContainsDashboardFields() throws Exception {
        String dashboard = "{\"current\":null,\"projects\":[]}";
        String envelope = H5MessageValidator.buildDashboardEnvelope(dashboard, false);
        assertNotNull(envelope);
        JSONObject obj = new JSONObject(envelope);
        JSONObject dashObj = obj.getJSONObject("dashboard");
        assertTrue(dashObj.isNull("current"));
        assertNotNull(dashObj.getJSONArray("projects"));
    }

    @Test
    public void handlesSpecialCharactersInDashboardJson() throws Exception {
        // Ensure special chars are correctly handled via org.json
        String dashboard = "{\"current\":null,\"projects\":[],\"note\":\"<test>&\\\"quoted\\\"\"}";
        String envelope = H5MessageValidator.buildDashboardEnvelope(dashboard, false);
        assertNotNull(envelope);
        // Must parse back correctly
        JSONObject obj = new JSONObject(envelope);
        assertNotNull(obj.getJSONObject("dashboard"));
    }

    @Test
    public void returnsNullForNullDashboard() {
        assertNull(H5MessageValidator.buildDashboardEnvelope(null, true));
    }

    @Test
    public void returnsNullForBlankDashboard() {
        assertNull(H5MessageValidator.buildDashboardEnvelope("", true));
        assertNull(H5MessageValidator.buildDashboardEnvelope("   ", true));
    }

    @Test
    public void returnsNullForMalformedDashboardJson() {
        assertNull(H5MessageValidator.buildDashboardEnvelope("not json", true));
        assertNull(H5MessageValidator.buildDashboardEnvelope("[1,2,3]", true));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static String minimalDashboard() {
        return "{\"current\":null,\"projects\":[]}";
    }
}
