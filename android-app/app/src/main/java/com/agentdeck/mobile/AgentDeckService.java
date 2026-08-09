package com.agentdeck.mobile;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.os.Build;
import android.os.IBinder;

import org.json.JSONException;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;

public final class AgentDeckService extends Service implements BridgeClient.EventListener {
    static final String ACTION_STATE = "com.agentdeck.mobile.STATE";
    static final String ACTION_CONNECTION = "com.agentdeck.mobile.CONNECTION";
    static final String EXTRA_JSON = "json";
    static final String EXTRA_CONNECTED = "connected";
    static final String EXTRA_DETAIL = "detail";
    static final String PREFS = "agentdeck_state";
    static final String PREF_DASHBOARD = "dashboard";
    static final String PREF_CONNECTED = "connected";

    private static final String CONNECTION_CHANNEL = "agentdeck_connection";
    private static final String STATUS_CHANNEL = "agentdeck_status";
    private static final int FOREGROUND_ID = 1001;
    private static final int STATUS_ID = 1002;

    private final AtomicBoolean running = new AtomicBoolean(false);
    private ExecutorService executor;
    private String lastNotifiedEvent = "";

    @Override
    public void onCreate() {
        super.onCreate();
        createNotificationChannels();
        startForegroundServiceNotification(false);
        running.set(true);
        executor = Executors.newSingleThreadExecutor();
        executor.execute(() -> BridgeClient.runEventLoop(running, this));
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        return START_STICKY;
    }

    @Override
    public void onDestroy() {
        running.set(false);
        if (executor != null) executor.shutdownNow();
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onDashboard(String json) {
        getSharedPreferences(PREFS, MODE_PRIVATE).edit().putString(PREF_DASHBOARD, json).apply();
        Intent update = new Intent(ACTION_STATE).setPackage(getPackageName());
        update.putExtra(EXTRA_JSON, json);
        sendBroadcast(update);
        postImportantStatus(json);
    }

    @Override
    public void onConnection(boolean connected, String detail) {
        getSharedPreferences(PREFS, MODE_PRIVATE).edit()
                .putBoolean(PREF_CONNECTED, connected)
                .apply();
        NotificationManager manager = getSystemService(NotificationManager.class);
        manager.notify(FOREGROUND_ID, connectionNotification(connected));
        Intent update = new Intent(ACTION_CONNECTION).setPackage(getPackageName());
        update.putExtra(EXTRA_CONNECTED, connected);
        update.putExtra(EXTRA_DETAIL, detail);
        sendBroadcast(update);
    }

    private void startForegroundServiceNotification(boolean connected) {
        Notification notification = connectionNotification(connected);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                    FOREGROUND_ID,
                    notification,
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC
            );
        } else {
            startForeground(FOREGROUND_ID, notification);
        }
    }

    private Notification connectionNotification(boolean connected) {
        return new Notification.Builder(this, CONNECTION_CHANNEL)
                .setSmallIcon(com.agentdeck.mobile.R.drawable.ic_agentdeck)
                .setContentTitle("AgentDesk")
                .setContentText(connected ? "本機 Bridge 已連線" : "正在等待本機 Bridge")
                .setContentIntent(openAppIntent())
                .setOngoing(true)
                .setCategory(Notification.CATEGORY_SERVICE)
                .build();
    }

    private void postImportantStatus(String json) {
        try {
            DashboardState.AgentState state = DashboardState.fromJson(json).current;
            if (state == null || state.eventId.equals(lastNotifiedEvent)) return;
            boolean important = state.requiresAction
                    || "completed".equals(state.status)
                    || "error".equals(state.status);
            if (!important) return;
            lastNotifiedEvent = state.eventId;
            Notification notification = new Notification.Builder(this, STATUS_CHANNEL)
                    .setSmallIcon(com.agentdeck.mobile.R.drawable.ic_agentdeck)
                    .setContentTitle(state.project + " · " + state.statusLabel())
                    .setContentText(state.message)
                    .setContentIntent(openAppIntent())
                    .setAutoCancel(true)
                    .setVibrate(null)
                    .setCategory(state.requiresAction
                            ? Notification.CATEGORY_REMINDER : Notification.CATEGORY_STATUS)
                    .build();
            getSystemService(NotificationManager.class).notify(STATUS_ID, notification);
        } catch (JSONException ignored) {
            // The Bridge contract is validated independently; retain the last good dashboard.
        }
    }

    private PendingIntent openAppIntent() {
        Intent intent = new Intent(this, MainActivity.class)
                .addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_CLEAR_TOP);
        return PendingIntent.getActivity(
                this,
                0,
                intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE
        );
    }

    private void createNotificationChannels() {
        NotificationManager manager = getSystemService(NotificationManager.class);
        NotificationChannel connection = new NotificationChannel(
                CONNECTION_CHANNEL,
                getString(com.agentdeck.mobile.R.string.service_channel),
                NotificationManager.IMPORTANCE_LOW
        );
        connection.enableVibration(false);
        connection.setVibrationPattern(null);

        NotificationChannel status = new NotificationChannel(
                STATUS_CHANNEL,
                getString(com.agentdeck.mobile.R.string.status_channel),
                NotificationManager.IMPORTANCE_HIGH
        );
        status.enableVibration(false);
        status.setVibrationPattern(null);

        manager.createNotificationChannel(connection);
        manager.createNotificationChannel(status);
    }
}
