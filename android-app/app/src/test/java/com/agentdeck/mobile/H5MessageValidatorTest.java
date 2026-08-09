package com.agentdeck.mobile;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

/**
 * Unit tests for {@link H5MessageValidator}.
 *
 * Covers:
 * <ul>
 *   <li>Action allowlist (approve, reject, select_project)</li>
 *   <li>target_id required non-null, non-blank rules for all actions</li>
 *   <li>Malformed / oversized payload rejection</li>
 *   <li>Origin/URL allow policy</li>
 * </ul>
 */
public final class H5MessageValidatorTest {

    // -----------------------------------------------------------------------
    // Allowlist: approve
    // -----------------------------------------------------------------------

    @Test
    public void approvesApproveActionWithTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"approve\",\"target_id\":\"evt_123\"}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNotNull(result);
        assertEquals("approve", result.action);
        assertEquals("evt_123", result.targetId);
    }

    @Test
    public void rejectsApproveActionWithNullTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"approve\",\"target_id\":null}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNull("approve requires non-null target_id", result);
    }

    @Test
    public void rejectsApproveActionWithBlankTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"approve\",\"target_id\":\"   \"}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNull("approve requires non-blank target_id", result);
    }

    // -----------------------------------------------------------------------
    // Allowlist: reject
    // -----------------------------------------------------------------------

    @Test
    public void approvesRejectActionWithTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"reject\",\"target_id\":\"evt_456\"}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNotNull(result);
        assertEquals("reject", result.action);
        assertEquals("evt_456", result.targetId);
    }

    @Test
    public void rejectsRejectActionWithNullTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"reject\",\"target_id\":null}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNull("reject requires non-null target_id", result);
    }

    @Test
    public void rejectsRejectActionWithBlankTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"reject\",\"target_id\":\"\"}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNull("reject requires non-blank target_id", result);
    }

    // -----------------------------------------------------------------------
    // Allowlist: select_project
    // -----------------------------------------------------------------------

    @Test
    public void approvesSelectProjectWithNonNullTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"select_project\",\"target_id\":\"proj_abc\"}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNotNull(result);
        assertEquals("select_project", result.action);
        assertEquals("proj_abc", result.targetId);
    }

    @Test
    public void rejectsSelectProjectWithNullTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"select_project\",\"target_id\":null}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNull("select_project requires non-null target_id", result);
    }

    @Test
    public void rejectsSelectProjectWithBlankTargetId() {
        String msg = "{\"type\":\"action\",\"action\":\"select_project\",\"target_id\":\"  \"}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNull("select_project requires non-blank target_id", result);
    }

    @Test
    public void rejectsSelectProjectWithMissingTargetIdField() {
        String msg = "{\"type\":\"action\",\"action\":\"select_project\"}";
        H5MessageValidator.ValidatedAction result = H5MessageValidator.validate(msg);
        assertNull("target_id field must be present", result);
    }

    // -----------------------------------------------------------------------
    // Exact action allowlist – unknown actions rejected
    // -----------------------------------------------------------------------

    @Test
    public void rejectsUnknownAction() {
        String msg = "{\"type\":\"action\",\"action\":\"delete\",\"target_id\":\"t1\"}";
        assertNull(H5MessageValidator.validate(msg));
    }

    @Test
    public void rejectsEmptyAction() {
        String msg = "{\"type\":\"action\",\"action\":\"\",\"target_id\":\"t1\"}";
        assertNull(H5MessageValidator.validate(msg));
    }

    @Test
    public void rejectsActionWithWrongCase() {
        // Case-sensitive: "Approve" ≠ "approve"
        String msg = "{\"type\":\"action\",\"action\":\"Approve\",\"target_id\":\"t1\"}";
        assertNull(H5MessageValidator.validate(msg));
    }

    // -----------------------------------------------------------------------
    // type field validation
    // -----------------------------------------------------------------------

    @Test
    public void rejectsWrongType() {
        String msg = "{\"type\":\"dashboard\",\"action\":\"approve\",\"target_id\":\"t1\"}";
        assertNull(H5MessageValidator.validate(msg));
    }

    @Test
    public void rejectsMissingType() {
        String msg = "{\"action\":\"approve\",\"target_id\":\"t1\"}";
        assertNull(H5MessageValidator.validate(msg));
    }

    // -----------------------------------------------------------------------
    // Malformed payloads
    // -----------------------------------------------------------------------

    @Test
    public void rejectsNullInput() {
        assertNull(H5MessageValidator.validate(null));
    }

    @Test
    public void rejectsEmptyString() {
        assertNull(H5MessageValidator.validate(""));
    }

    @Test
    public void rejectsBlankString() {
        assertNull(H5MessageValidator.validate("   "));
    }

    @Test
    public void rejectsMalformedJson() {
        assertNull(H5MessageValidator.validate("not json"));
        assertNull(H5MessageValidator.validate("{broken"));
        assertNull(H5MessageValidator.validate("[\"array\"]"));
    }

    @Test
    public void rejectsMissingTargetIdField() {
        String msg = "{\"type\":\"action\",\"action\":\"approve\"}";
        assertNull(H5MessageValidator.validate(msg));
    }

    @Test
    public void rejectsTargetIdOfWrongType() {
        String msg = "{\"type\":\"action\",\"action\":\"approve\",\"target_id\":123}";
        assertNull(H5MessageValidator.validate(msg));
    }

    @Test
    public void rejectsTargetIdAsArray() {
        String msg = "{\"type\":\"action\",\"action\":\"approve\",\"target_id\":[]}";
        assertNull(H5MessageValidator.validate(msg));
    }

    // -----------------------------------------------------------------------
    // Oversized payload
    // -----------------------------------------------------------------------

    @Test
    public void rejectsOversizedPayload() {
        StringBuilder sb = new StringBuilder("{\"type\":\"action\",\"action\":\"approve\",\"target_id\":\"");
        for (int i = 0; i < H5MessageValidator.MAX_MESSAGE_BYTES + 10; i++) {
            sb.append('x');
        }
        sb.append("\"}");
        assertNull("Oversized payload must be rejected", H5MessageValidator.validate(sb.toString()));
    }

    @Test
    public void rejectsOversizedTargetId() {
        StringBuilder sb = new StringBuilder("{\"type\":\"action\",\"action\":\"select_project\",\"target_id\":\"");
        for (int i = 0; i < H5MessageValidator.MAX_TARGET_ID_LENGTH + 1; i++) {
            sb.append('a');
        }
        sb.append("\"}");
        assertNull("Target ID exceeding max length must be rejected",
                H5MessageValidator.validate(sb.toString()));
    }

    @Test
    public void acceptsTargetIdAtMaxLength() {
        StringBuilder tid = new StringBuilder();
        for (int i = 0; i < H5MessageValidator.MAX_TARGET_ID_LENGTH; i++) {
            tid.append('a');
        }
        String msg = "{\"type\":\"action\",\"action\":\"select_project\",\"target_id\":\"" + tid + "\"}";
        assertNotNull("Target ID at exact max length must be accepted",
                H5MessageValidator.validate(msg));
    }

    @Test
    public void acceptsValidReadyMessage() {
        assertTrue(H5MessageValidator.isReadyMessage("{\"type\":\"ready\"}"));
    }

    @Test
    public void rejectsInvalidReadyMessages() {
        assertFalse(H5MessageValidator.isReadyMessage(null));
        assertFalse(H5MessageValidator.isReadyMessage(""));
        assertFalse(H5MessageValidator.isReadyMessage("   "));
        assertFalse(H5MessageValidator.isReadyMessage("not json"));
        assertFalse(H5MessageValidator.isReadyMessage("{\"type\":\"action\"}"));
        assertFalse(H5MessageValidator.isReadyMessage("{\"type\":\"READY\"}"));
    }

    // -----------------------------------------------------------------------
    // Origin / URL allow policy
    // -----------------------------------------------------------------------

    @Test
    public void recognizesValidAppAssetOrigin() {
        assertTrue(H5MessageValidator.isAppAssetOrigin("https://appassets.androidplatform.net"));
    }

    @Test
    public void rejectsOtherOrigins() {
        assertFalse(H5MessageValidator.isAppAssetOrigin("https://example.com"));
        assertFalse(H5MessageValidator.isAppAssetOrigin("http://appassets.androidplatform.net"));
        assertFalse(H5MessageValidator.isAppAssetOrigin(""));
        assertFalse(H5MessageValidator.isAppAssetOrigin(null));
    }

    @Test
    public void recognizesValidAppAssetUrls() {
        assertTrue(H5MessageValidator.isAppAssetUrl(
                "https://appassets.androidplatform.net/assets/index.html"));
        assertTrue(H5MessageValidator.isAppAssetUrl(
                "https://appassets.androidplatform.net/assets/index-abc123.js"));
    }

    @Test
    public void rejectsExternalUrls() {
        assertFalse(H5MessageValidator.isAppAssetUrl("https://example.com/index.html"));
        assertFalse(H5MessageValidator.isAppAssetUrl("file:///android_asset/index.html"));
        assertFalse(H5MessageValidator.isAppAssetUrl("http://appassets.androidplatform.net/assets/index.html"));
        assertFalse(H5MessageValidator.isAppAssetUrl(null));
    }
}
