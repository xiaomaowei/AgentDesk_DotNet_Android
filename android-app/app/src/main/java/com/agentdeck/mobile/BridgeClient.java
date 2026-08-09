package com.agentdeck.mobile;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;

final class BridgeClient {
    static final String BASE_URL = "http://127.0.0.1:8765";

    interface EventListener {
        void onDashboard(String json);
        void onConnection(boolean connected, String detail);
    }

    private BridgeClient() {}

    static void runEventLoop(AtomicBoolean running, EventListener listener) {
        while (running.get()) {
            HttpURLConnection connection = null;
            try {
                connection = (HttpURLConnection) new URL(BASE_URL + "/api/v1/events").openConnection();
                connection.setRequestMethod("GET");
                connection.setConnectTimeout(3000);
                connection.setReadTimeout(20000);
                connection.setRequestProperty("Accept", "text/event-stream");
                if (connection.getResponseCode() != HttpURLConnection.HTTP_OK) {
                    throw new IOException("Bridge HTTP " + connection.getResponseCode());
                }
                listener.onConnection(true, "本機 Bridge 已連線");
                try (BufferedReader reader = new BufferedReader(new InputStreamReader(
                        connection.getInputStream(), StandardCharsets.UTF_8))) {
                    String line;
                    while (running.get() && (line = reader.readLine()) != null) {
                        if (line.startsWith("data: ")) {
                            listener.onDashboard(line.substring(6));
                        }
                    }
                }
            } catch (IOException exception) {
                listener.onConnection(false, exception.getMessage());
            } finally {
                if (connection != null) connection.disconnect();
            }
            if (running.get()) {
                try {
                    Thread.sleep(1500);
                } catch (InterruptedException exception) {
                    Thread.currentThread().interrupt();
                    return;
                }
            }
        }
    }

    static boolean parseActionResult(String responseJson) {
        if (responseJson == null || responseJson.isBlank()) {
            return false;
        }
        try {
            JSONObject envelope = new JSONObject(responseJson);
            JSONObject payload = envelope.optJSONObject("payload");
            if (payload == null) {
                return false;
            }
            return payload.optBoolean("accepted", false);
        } catch (Exception e) {
            return false;
        }
    }

    static boolean postAction(String action, String targetId) {
        HttpURLConnection connection = null;
        try {
            JSONObject payload = new JSONObject();
            payload.put("action", action);
            payload.put("target_id", targetId == null ? JSONObject.NULL : targetId);
            JSONObject envelope = new JSONObject();
            envelope.put("version", "1.0");
            envelope.put("type", "action");
            envelope.put("id", "android_" + UUID.randomUUID().toString().replace("-", ""));
            envelope.put("timestamp", JSONObject.NULL);
            envelope.put("payload", payload);

            byte[] body = envelope.toString().getBytes(StandardCharsets.UTF_8);
            connection = (HttpURLConnection) new URL(BASE_URL + "/api/v1/actions").openConnection();
            connection.setRequestMethod("POST");
            connection.setConnectTimeout(3000);
            connection.setReadTimeout(5000);
            connection.setDoOutput(true);
            connection.setRequestProperty("Content-Type", "application/json");
            connection.setFixedLengthStreamingMode(body.length);
            try (OutputStream output = connection.getOutputStream()) {
                output.write(body);
            }
            if (connection.getResponseCode() != HttpURLConnection.HTTP_OK) {
                return false;
            }
            try (BufferedReader reader = new BufferedReader(new InputStreamReader(
                    connection.getInputStream(), StandardCharsets.UTF_8))) {
                StringBuilder builder = new StringBuilder();
                String line;
                while ((line = reader.readLine()) != null) {
                    builder.append(line);
                }
                return parseActionResult(builder.toString());
            }
        } catch (Exception ignored) {
            return false;
        } finally {
            if (connection != null) connection.disconnect();
        }
    }
}
