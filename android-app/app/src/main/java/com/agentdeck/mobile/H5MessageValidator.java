package com.agentdeck.mobile;

import org.json.JSONException;
import org.json.JSONObject;

import java.nio.charset.StandardCharsets;
import java.util.Collections;
import java.util.HashSet;
import java.util.Set;

/**
 * Validates and parses inbound H5 action messages and bootstrap ready messages.
 * All validation fails closed: any malformed, oversized, or out-of-allowlist
 * message returns {@code null} or {@code false}.
 *
 * <p>Contract (host ← H5):
 * <pre>
 * Actions:
 * {
 *   "type": "action",                              // required, exact
 *   "action": "approve" | "reject" | "select_project", // required, exact allowlist
 *   "target_id": string                            // required non-null, non-blank string
 * }
 *
 * Bootstrap ready:
 * {
 *   "type": "ready"                                // required, exact
 * }
 * </pre>
 */
public final class H5MessageValidator {

    /** Maximum byte length accepted for raw JSON messages from H5. */
    static final int MAX_MESSAGE_BYTES = 4096;

    /** Maximum character length accepted for a target_id string value. */
    static final int MAX_TARGET_ID_LENGTH = 512;

    private static final Set<String> ALLOWED_ACTIONS;

    static {
        Set<String> set = new HashSet<>();
        set.add("approve");
        set.add("reject");
        set.add("select_project");
        ALLOWED_ACTIONS = Collections.unmodifiableSet(set);
    }

    /** Parsed, validated result of an H5 action message. */
    public static final class ValidatedAction {
        public final String action;
        public final String targetId;

        ValidatedAction(String action, String targetId) {
            this.action = action;
            this.targetId = targetId;
        }
    }

    private H5MessageValidator() {}

    /**
     * Validates the raw JSON string received from the bootstrap WebMessageListener.
     * Requires non-null, non-blank string within size limit, type == "ready" exactly.
     *
     * @param raw the raw message string
     * @return true if valid ready message, false otherwise (fail-closed)
     */
    public static boolean isReadyMessage(String raw) {
        if (raw == null || raw.isBlank()) {
            return false;
        }
        if (raw.getBytes(StandardCharsets.UTF_8).length > MAX_MESSAGE_BYTES) {
            return false;
        }
        try {
            JSONObject obj = new JSONObject(raw);
            return "ready".equals(obj.optString("type", null));
        } catch (JSONException e) {
            return false;
        }
    }

    /**
     * Validates the raw JSON string received from the H5 message port.
     *
     * @param raw the raw message string, may be null
     * @return a {@link ValidatedAction} on success, or {@code null} on any
     *         validation failure (fail-closed)
     */
    public static ValidatedAction validate(String raw) {
        // 1. Null / empty
        if (raw == null || raw.isBlank()) {
            return null;
        }

        // 2. Size guard (bytes, UTF-8 worst case)
        if (raw.getBytes(StandardCharsets.UTF_8).length > MAX_MESSAGE_BYTES) {
            return null;
        }

        // 3. JSON parse
        JSONObject obj;
        try {
            obj = new JSONObject(raw);
        } catch (JSONException e) {
            return null;
        }

        // 4. type must be "action" exactly
        String type = obj.optString("type", null);
        if (!"action".equals(type)) {
            return null;
        }

        // 5. action must be in the allowlist
        String action = obj.optString("action", null);
        if (action == null || !ALLOWED_ACTIONS.contains(action)) {
            return null;
        }

        // 6. target_id: required, non-null, non-blank string for all actions
        if (!obj.has("target_id") || obj.isNull("target_id")) {
            return null;
        }
        Object rawTid = obj.opt("target_id");
        if (!(rawTid instanceof String)) {
            return null;
        }
        String targetId = (String) rawTid;
        if (targetId.isBlank() || targetId.length() > MAX_TARGET_ID_LENGTH) {
            return null;
        }

        return new ValidatedAction(action, targetId);
    }

    /**
     * Constructs the dashboard envelope JSON string sent to H5.
     * Uses {@code org.json} to ensure correct escaping; the raw dashboard
     * JSON object is embedded as a parsed JSON value (not re-escaped as a string).
     *
     * @param dashboardJson raw dashboard JSON string (value of the "dashboard" field)
     * @param connected     current connection flag
     * @return envelope JSON string, or {@code null} if construction fails
     */
    public static String buildDashboardEnvelope(String dashboardJson, boolean connected) {
        if (dashboardJson == null || dashboardJson.isBlank()) return null;
        try {
            JSONObject dashboard = new JSONObject(dashboardJson);
            JSONObject envelope = new JSONObject();
            envelope.put("type", "dashboard");
            envelope.put("dashboard", dashboard);
            envelope.put("connected", connected);
            return envelope.toString();
        } catch (JSONException e) {
            return null;
        }
    }

    /**
     * Returns true if {@code origin} is exactly the WebViewAssetLoader app-asset
     * origin. Used for navigation/origin policy checks.
     *
     * @param origin the origin string to check
     */
    public static boolean isAppAssetOrigin(String origin) {
        return "https://appassets.androidplatform.net".equals(origin);
    }

    /**
     * Returns true if {@code url} belongs to the app-asset origin.
     * External navigation is always rejected; only the exact app-asset prefix
     * is permitted.
     *
     * @param url the URL to check
     */
    public static boolean isAppAssetUrl(String url) {
        if (url == null) return false;
        return url.startsWith("https://appassets.androidplatform.net/assets/");
    }
}
