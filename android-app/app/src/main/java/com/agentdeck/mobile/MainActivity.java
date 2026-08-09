package com.agentdeck.mobile;

import android.Manifest;
import android.app.Activity;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowManager;

/**
 * Main Activity: hosts the H5 WebView dashboard surface.
 *
 * <p>The native {@link AgentDeckView} and {@link DashboardState} are preserved as-is.
 * This Activity now delegates the display to {@link H5Bridge}, which manages
 * the WebView and the WebMessageChannel communication with the React H5 UI.
 *
 * <p>All backend services remain unchanged:
 * <ul>
 *   <li>{@link AgentDeckService} runs as a foreground service and owns SSE + cache.</li>
 *   <li>Broadcasts ({@link AgentDeckService#ACTION_STATE},
 *       {@link AgentDeckService#ACTION_CONNECTION}) are received here and
 *       forwarded to H5 via {@link H5Bridge#pushDashboard}.</li>
 *   <li>Cached dashboard and connection flag are loaded from SharedPreferences
 *       on Activity startup and pushed to H5 once the port is ready.</li>
 * </ul>
 */
public final class MainActivity extends Activity {

    private H5Bridge h5Bridge;

    // Last known state (raw JSON + connected flag) – buffered while port is starting
    private String lastDashboardJson = null;
    private boolean lastConnected = false;

    private final BroadcastReceiver receiver = new BroadcastReceiver() {
        @Override
        public void onReceive(Context context, Intent intent) {
            if (AgentDeckService.ACTION_STATE.equals(intent.getAction())) {
                String json = intent.getStringExtra(AgentDeckService.EXTRA_JSON);
                lastDashboardJson = json;
                h5Bridge.pushDashboard(json, lastConnected);
            } else if (AgentDeckService.ACTION_CONNECTION.equals(intent.getAction())) {
                boolean connected = intent.getBooleanExtra(AgentDeckService.EXTRA_CONNECTED, false);
                lastConnected = connected;
                h5Bridge.pushDashboard(lastDashboardJson, connected);
            }
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        configureFullScreenAndInsets();

        h5Bridge = new H5Bridge(this);
        setContentView(h5Bridge.getWebView());

        setupWindowInsetsForWebView();
        registerAgentDeckReceiver();
        loadCachedState();
        h5Bridge.loadApp();

        startForegroundService(new Intent(this, AgentDeckService.class));
        requestNotificationPermission();
    }

    @Override
    protected void onDestroy() {
        unregisterReceiver(receiver);
        h5Bridge.destroy();
        super.onDestroy();
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void setupWindowInsetsForWebView() {
        h5Bridge.getWebView().setOnApplyWindowInsetsListener((v, insets) -> {
            int left = 0, top = 0, right = 0, bottom = 0;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                android.graphics.Insets sysInsets = insets.getInsets(
                        WindowInsets.Type.systemBars() | WindowInsets.Type.displayCutout());
                left = sysInsets.left;
                top = sysInsets.top;
                right = sysInsets.right;
                bottom = sysInsets.bottom;
            }
            v.setPadding(left, top, right, bottom);
            return insets;
        });
    }

    @android.annotation.SuppressLint("UnspecifiedRegisterReceiverFlag")
    private void registerAgentDeckReceiver() {
        IntentFilter filter = new IntentFilter();
        filter.addAction(AgentDeckService.ACTION_STATE);
        filter.addAction(AgentDeckService.ACTION_CONNECTION);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            registerReceiver(receiver, filter, Context.RECEIVER_NOT_EXPORTED);
        } else {
            registerReceiver(receiver, filter);
        }
    }

    private void loadCachedState() {
        android.content.SharedPreferences prefs =
                getSharedPreferences(AgentDeckService.PREFS, MODE_PRIVATE);
        lastConnected = prefs.getBoolean(AgentDeckService.PREF_CONNECTED, false);
        lastDashboardJson = prefs.getString(AgentDeckService.PREF_DASHBOARD, null);
        // pushDashboard buffers if the port is not yet ready (it is not yet).
        h5Bridge.pushDashboard(lastDashboardJson, lastConnected);
    }

    private void requestNotificationPermission() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU
                && checkSelfPermission(Manifest.permission.POST_NOTIFICATIONS)
                != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(new String[]{Manifest.permission.POST_NOTIFICATIONS}, 42);
        }
    }

    @SuppressWarnings("deprecation")
    private void configureFullScreenAndInsets() {
        Window window = getWindow();
        int darkBg = Color.rgb(15, 23, 42);
        window.setStatusBarColor(darkBg);
        window.setNavigationBarColor(darkBg);
        window.addFlags(WindowManager.LayoutParams.FLAG_DRAWS_SYSTEM_BAR_BACKGROUNDS);
        window.getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                        | View.SYSTEM_UI_FLAG_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
        );
    }
}
