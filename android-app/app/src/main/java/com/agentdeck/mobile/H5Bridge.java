package com.agentdeck.mobile;

import android.annotation.SuppressLint;
import android.content.Context;
import android.net.Uri;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.webkit.GeolocationPermissions;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

import androidx.webkit.WebMessageCompat;
import androidx.webkit.WebMessagePortCompat;
import androidx.webkit.WebViewAssetLoader;
import androidx.webkit.WebViewCompat;
import androidx.webkit.WebViewFeature;

import java.io.ByteArrayInputStream;
import java.nio.charset.StandardCharsets;
import java.util.Collections;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Manages the H5 WebView that replaces the native AgentDeckView as the primary
 * display surface.
 *
 * <p>Security posture:
 * <ul>
 *   <li>Served via {@link WebViewAssetLoader} at
 *       {@code https://appassets.androidplatform.net/assets/index.html}</li>
 *   <li>JavaScript enabled only for React.</li>
 *   <li>File access, content access, universal/file URL cross-origin access,
 *       geolocation, and multiple windows all disabled.</li>
 *   <li>Mixed content blocked.</li>
 *   <li>Navigation restricted to the exact app-asset origin/path.</li>
 *   <li>Safe browsing enabled where available.</li>
 *   <li>No {@code addJavascriptInterface}.</li>
 *   <li>Narrow {@code AgentDeckBootstrap} listener used strictly for H5 ready handshake.</li>
 *   <li>Message channel used for all native↔H5 dashboard & action communication.</li>
 * </ul>
 *
 * <p>Message protocol:
 * <ul>
 *   <li>H5 → Host bootstrap: {@code {"type":"ready"}} via {@code AgentDeckBootstrap.postMessage}</li>
 *   <li>Host → H5 init: {@code {"type":"agentdeck:init"}} with port[1] transferred via {@code postWebMessage}</li>
 *   <li>Host → H5 data: {@code {"type":"dashboard","dashboard":<raw JSON object>,"connected":<bool>}}</li>
 *   <li>H5 → Host: {@code {"type":"action","action":"approve"|"reject"|"select_project","target_id":string}}</li>
 * </ul>
 */
@SuppressLint("SetJavaScriptEnabled")
public final class H5Bridge {

    private static final String TAG = "H5Bridge";
    static final String APP_ASSET_BASE_URL =
            "https://appassets.androidplatform.net/assets/index.html";
    private static final String APP_ASSET_ORIGIN =
            "https://appassets.androidplatform.net";

    private final WebView webView;
    private final WebViewAssetLoader assetLoader;
    private final ExecutorService actionExecutor = Executors.newSingleThreadExecutor();
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    private WebMessagePortCompat hostPort;  // the port the host keeps
    private boolean portReady = false;

    // Last known state, buffered until port is ready or reloaded
    private String pendingDashboardJson = null;
    private boolean pendingConnected = false;

    public H5Bridge(Context context) {
        webView = new WebView(context);
        assetLoader = new WebViewAssetLoader.Builder()
                .setDomain("appassets.androidplatform.net")
                .addPathHandler("/assets/", new WebViewAssetLoader.AssetsPathHandler(context))
                .build();

        configureWebView();
    }

    /** Returns the underlying {@link WebView} to embed in the Activity layout. */
    public WebView getWebView() {
        return webView;
    }

    /** Loads the H5 app. Call after the WebView has been attached to a window. */
    public void loadApp() {
        webView.loadUrl(APP_ASSET_BASE_URL);
    }

    /**
     * Pushes the current dashboard state to H5. If the message channel is not
     * yet ready, the state is buffered and sent once the channel is established.
     *
     * @param dashboardJson raw JSON string from the bridge (the exact string
     *                      stored in SharedPreferences / received from SSE)
     * @param connected     current connection flag
     */
    public void pushDashboard(String dashboardJson, boolean connected) {
        pendingDashboardJson = dashboardJson;
        pendingConnected = connected;

        if (!portReady) {
            return;
        }
        sendDashboardToPort(dashboardJson, connected);
    }

    /**
     * Called on Activity destroy to release the WebView and close ports.
     */
    public void destroy() {
        actionExecutor.shutdownNow();
        closePort();
        webView.stopLoading();
        webView.destroy();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    @SuppressLint("SetJavaScriptEnabled")
    private void configureWebView() {
        WebSettings s = webView.getSettings();

        // Required for React
        s.setJavaScriptEnabled(true);

        // Harden: disable all file/content access
        s.setAllowFileAccess(false);
        s.setAllowContentAccess(false);

        // Disable deprecated cross-origin file access flags
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.JELLY_BEAN) {
            s.setAllowFileAccessFromFileURLs(false);
            s.setAllowUniversalAccessFromFileURLs(false);
        }

        // Block mixed content
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            s.setMixedContentMode(WebSettings.MIXED_CONTENT_NEVER_ALLOW);
        }

        // Disable geolocation, popups, zoom
        s.setGeolocationEnabled(false);
        s.setSupportMultipleWindows(false);
        s.setSupportZoom(false);
        s.setBuiltInZoomControls(false);
        s.setDisplayZoomControls(false);

        // Safe browsing
        if (WebViewFeature.isFeatureSupported(WebViewFeature.SAFE_BROWSING_ENABLE)) {
            WebSettingsCompat.setSafeBrowsingEnabled(webView, true);
        }

        // Register WebMessageListener for exact-origin H5 bootstrap handshake
        if (WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_LISTENER)) {
            WebViewCompat.addWebMessageListener(
                    webView,
                    "AgentDeckBootstrap",
                    Collections.singleton(APP_ASSET_ORIGIN),
                    (view, message, sourceOrigin, isMainFrame, replyProxy) -> {
                        if (!isMainFrame) return;
                        if (!APP_ASSET_ORIGIN.equals(sourceOrigin.toString())) return;
                        if (message.getType() != WebMessageCompat.TYPE_STRING) return;
                        String data = message.getData();
                        if (!H5MessageValidator.isReadyMessage(data)) return;

                        onH5Ready();
                    }
            );
        } else {
            Log.e(TAG, "WEB_MESSAGE_LISTENER feature not supported on this device");
        }

        // Install the hardened WebViewClient
        webView.setWebViewClient(new SecureWebViewClient());

        // Install the chrome client that blocks new windows and geolocation
        webView.setWebChromeClient(new HardenedChromeClient());
    }

    /**
     * Called once H5 sends a validated ready bootstrap message. Closes any previous
     * message port state, sets up a fresh message channel, and transfers port[1] to H5.
     */
    private void onH5Ready() {
        if (!WebViewFeature.isFeatureSupported(WebViewFeature.CREATE_WEB_MESSAGE_CHANNEL)
                || !WebViewFeature.isFeatureSupported(WebViewFeature.POST_WEB_MESSAGE)
                || !WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_PORT_SET_MESSAGE_CALLBACK)
                || !WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_PORT_POST_MESSAGE)) {
            Log.e(TAG, "WebMessageChannel or WebMessagePort features not supported on this device");
            return;
        }

        // Clean up previous port state on reload/ready
        closePort();

        WebMessagePortCompat[] channel = WebViewCompat.createWebMessageChannel(webView);
        hostPort = channel[0];
        WebMessagePortCompat h5Port = channel[1];

        // Set up listener on the host port
        hostPort.setWebMessageCallback(new WebMessagePortCompat.WebMessageCallbackCompat() {
            @Override
            public void onMessage(WebMessagePortCompat port, WebMessageCompat message) {
                handleH5Message(message);
            }
        });

        // Transfer port[1] to H5 with the init message
        String initPayload = "{\"type\":\"agentdeck:init\"}";
        WebViewCompat.postWebMessage(
                webView,
                new WebMessageCompat(initPayload, new WebMessagePortCompat[]{h5Port}),
                Uri.parse(APP_ASSET_ORIGIN)
        );

        portReady = true;

        // Flush any buffered state across reloads/startup
        if (pendingDashboardJson != null) {
            sendDashboardToPort(pendingDashboardJson, pendingConnected);
        }
    }

    private void handleH5Message(WebMessageCompat message) {
        String raw = message.getData();
        H5MessageValidator.ValidatedAction validated = H5MessageValidator.validate(raw);
        if (validated == null) {
            Log.w(TAG, "Rejected invalid H5 message (size=" +
                    (raw != null ? raw.length() : "null") + ")");
            return;
        }

        final String action = validated.action;
        final String targetId = validated.targetId;

        // Execute the HTTP action off the UI thread
        actionExecutor.execute(() -> {
            boolean accepted = BridgeClient.postAction(action, targetId);
            mainHandler.post(() -> {
                if (!accepted) {
                    showToast("操作處理失敗 (Bridge 拒絕或連線異常)");
                }
                // Re-send last dashboard to clear H5 pending state regardless
                if (pendingDashboardJson != null) {
                    sendDashboardToPort(pendingDashboardJson, pendingConnected);
                }
            });
        });
    }

    private void sendDashboardToPort(String dashboardJson, boolean connected) {
        if (!portReady || hostPort == null) return;
        if (dashboardJson == null || dashboardJson.isBlank()) return;

        String envelope = buildDashboardEnvelope(dashboardJson, connected);
        if (envelope == null) return;

        try {
            if (WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_PORT_POST_MESSAGE)) {
                hostPort.postMessage(new WebMessageCompat(envelope));
            }
        } catch (Exception e) {
            Log.e(TAG, "Failed to post dashboard message to H5 port", e);
        }
    }

    /**
     * Constructs the dashboard envelope JSON sent to H5.
     * Delegates to {@link H5MessageValidator#buildDashboardEnvelope} which has
     * no Android framework dependencies and is fully unit-testable.
     *
     * @param dashboardJson raw dashboard JSON string (value of the "dashboard" field)
     * @param connected     connection flag
     * @return envelope JSON string, or null if construction fails
     */
    static String buildDashboardEnvelope(String dashboardJson, boolean connected) {
        return H5MessageValidator.buildDashboardEnvelope(dashboardJson, connected);
    }

    private void closePort() {
        if (hostPort != null) {
            try {
                if (WebViewFeature.isFeatureSupported(WebViewFeature.WEB_MESSAGE_PORT_CLOSE)) {
                    hostPort.close();
                }
            } catch (Exception ignored) {
            }
            hostPort = null;
        }
        portReady = false;
    }

    private void showToast(String message) {
        Toast.makeText(webView.getContext(), message, Toast.LENGTH_SHORT).show();
    }

    // -----------------------------------------------------------------------
    // WebViewClient: intercepts all navigation and resource requests
    // -----------------------------------------------------------------------

    private class SecureWebViewClient extends WebViewClient {

        @Override
        public WebResourceResponse shouldInterceptRequest(
                WebView view, WebResourceRequest request) {
            // Only serve app-asset requests; block everything else
            if (H5MessageValidator.isAppAssetUrl(request.getUrl().toString())) {
                return assetLoader.shouldInterceptRequest(request.getUrl());
            }
            // Safe 404 response for external requests to avoid runtime null-constructor hazards
            return new WebResourceResponse(
                    "text/plain",
                    StandardCharsets.UTF_8.name(),
                    404,
                    "Blocked external load",
                    Collections.emptyMap(),
                    new ByteArrayInputStream(new byte[0])
            );
        }

        @Override
        public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request) {
            // Block all navigation outside the app-asset origin
            String url = request.getUrl().toString();
            if (H5MessageValidator.isAppAssetUrl(url)) {
                return false; // allow
            }
            return true; // block
        }

        @Override
        public void onPageFinished(WebView view, String url) {
            super.onPageFinished(view, url);
        }
    }

    // -----------------------------------------------------------------------
    // WebChromeClient: block popups, geolocation, file chooser
    // -----------------------------------------------------------------------

    private static class HardenedChromeClient extends android.webkit.WebChromeClient {

        @Override
        public void onGeolocationPermissionsShowPrompt(
                String origin, GeolocationPermissions.Callback callback) {
            // Always deny
            callback.invoke(origin, false, false);
        }

        @Override
        public boolean onCreateWindow(WebView view, boolean isDialog,
                boolean isUserGesture, android.os.Message resultMsg) {
            // Block new windows / popups
            return false;
        }

        @Override
        public void onShowCustomView(
                android.view.View view, android.webkit.WebChromeClient.CustomViewCallback callback) {
            // Never allow fullscreen video or custom view
            callback.onCustomViewHidden();
        }
    }

    // -----------------------------------------------------------------------
    // Compat shim: WebSettingsCompat.setSafeBrowsingEnabled via reflection
    // (avoids a direct import that would fail on API < 27 without X86 support)
    // -----------------------------------------------------------------------

    private static final class WebSettingsCompat {
        private WebSettingsCompat() {}

        static void setSafeBrowsingEnabled(WebView view, boolean enabled) {
            try {
                androidx.webkit.WebSettingsCompat.setSafeBrowsingEnabled(
                        view.getSettings(), enabled);
            } catch (Exception ignored) {
                // Silently fail: safe browsing unavailable on this device/API
            }
        }
    }
}
