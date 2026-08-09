using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AgentDesk.Server.Tests;

public class ServerIntegrationTests
{
    [Fact]
    public async Task GetHealth_ReturnsOkPayload()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal(0, root.GetProperty("devices").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("serial_port").ValueKind);
        Assert.False(root.GetProperty("usb_enabled").GetBoolean());
    }

    [Fact]
    public async Task GetDashboard_Initial_ReturnsEmptyDashboard()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("1.0", root.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("current").ValueKind);
        Assert.Equal(0, root.GetProperty("projects").GetArrayLength());
    }

    [Fact]
    public async Task PostActions_InvalidAction_Returns400Text()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var invalidJson = """{"version": "1.0", "type": "action", "payload": {"action": "invalid"}}""";

        var response = await client.PostAsync("/api/v1/actions", new StringContent(invalidJson, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unsupported action", text);
    }

    [Fact]
    public async Task PostHook_UpdatesDashboardAndActionResult()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var hookPayload = """
        {
          "hook_event_name": "UserPromptSubmit",
          "session_id": "integration_s1",
          "cwd": "C:\\projects\\TestApp",
          "prompt": "Test integration prompt"
        }
        """;

        var hookRes = await client.PostAsync("/api/v1/hooks/codex", new StringContent(hookPayload, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, hookRes.StatusCode);

        var dashRes = await client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, dashRes.StatusCode);
        using var doc = JsonDocument.Parse(await dashRes.Content.ReadAsStringAsync());
        var current = doc.RootElement.GetProperty("current");
        Assert.Equal("1.0", current.GetProperty("version").GetString());
        Assert.Equal("state", current.GetProperty("type").GetString());
        var currentPayload = current.GetProperty("payload");
        Assert.Equal("TestApp", currentPayload.GetProperty("project").GetString());
        Assert.Equal("working", currentPayload.GetProperty("status").GetString());

        var actionPayload = """
        {
          "version": "1.0",
          "type": "action",
          "id": "act_01",
          "timestamp": null,
          "payload": {
            "action": "clear",
            "target_id": null
          }
        }
        """;

        var actionRes = await client.PostAsync("/api/v1/actions", new StringContent(actionPayload, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, actionRes.StatusCode);
        using var actionDoc = JsonDocument.Parse(await actionRes.Content.ReadAsStringAsync());
        var payload = actionDoc.RootElement.GetProperty("payload");
        Assert.True(payload.GetProperty("accepted").GetBoolean());
        Assert.Equal("clear", payload.GetProperty("action").GetString());
    }

    [Fact]
    public async Task PermissionRequest_ApprovalFlow_Allow()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var hookPayload = """
        {
          "hook_event_name": "PermissionRequest",
          "session_id": "approval_s1",
          "cwd": "C:\\projects\\App",
          "tool_name": "exec_command",
          "tool_input": { "command": "git push" }
        }
        """;

        var hookTask = client.PostAsync("/api/v1/hooks/codex", new StringContent(hookPayload, System.Text.Encoding.UTF8, "application/json"));

        string? targetId = null;
        var timeout = TimeSpan.FromSeconds(5);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var dashRes = await client.GetAsync("/api/v1/dashboard");
            if (dashRes.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await dashRes.Content.ReadAsStringAsync());
                var current = doc.RootElement.GetProperty("current");
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
                {
                    if (payload.TryGetProperty("target_id", out var tid) && tid.ValueKind == JsonValueKind.String)
                    {
                        targetId = tid.GetString();
                        if (!string.IsNullOrEmpty(targetId)) break;
                    }
                }
            }
            await Task.Delay(20);
        }

        Assert.NotNull(targetId);

        var approveAction = $$"""
        {
          "version": "1.0",
          "type": "action",
          "id": "act_approve",
          "timestamp": null,
          "payload": {
            "action": "approve",
            "target_id": "{{targetId}}"
          }
        }
        """;

        var actionRes = await client.PostAsync("/api/v1/actions", new StringContent(approveAction, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, actionRes.StatusCode);
        using var actionDoc = JsonDocument.Parse(await actionRes.Content.ReadAsStringAsync());
        var actionPayload = actionDoc.RootElement.GetProperty("payload");
        Assert.True(actionPayload.GetProperty("accepted").GetBoolean());

        var hookRes = await hookTask;
        Assert.Equal(HttpStatusCode.OK, hookRes.StatusCode);
        using var hookDoc = JsonDocument.Parse(await hookRes.Content.ReadAsStringAsync());
        var hookSpecificOutput = hookDoc.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PermissionRequest", hookSpecificOutput.GetProperty("hookEventName").GetString());
        var decision = hookSpecificOutput.GetProperty("decision");
        Assert.Equal("allow", decision.GetProperty("behavior").GetString());
    }

    [Fact]
    public async Task PermissionRequest_ApprovalFlow_Deny()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var hookPayload = """
        {
          "hook_event_name": "PermissionRequest",
          "session_id": "approval_s2",
          "cwd": "C:\\projects\\App",
          "tool_name": "exec_command",
          "tool_input": { "command": "rm -rf /" }
        }
        """;

        var hookTask = client.PostAsync("/api/v1/hooks/codex", new StringContent(hookPayload, System.Text.Encoding.UTF8, "application/json"));

        string? targetId = null;
        var timeout = TimeSpan.FromSeconds(5);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var dashRes = await client.GetAsync("/api/v1/dashboard");
            if (dashRes.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await dashRes.Content.ReadAsStringAsync());
                var current = doc.RootElement.GetProperty("current");
                if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty("payload", out var payload) && payload.ValueKind == JsonValueKind.Object)
                {
                    if (payload.TryGetProperty("target_id", out var tid) && tid.ValueKind == JsonValueKind.String)
                    {
                        targetId = tid.GetString();
                        if (!string.IsNullOrEmpty(targetId)) break;
                    }
                }
            }
            await Task.Delay(20);
        }

        Assert.NotNull(targetId);

        var rejectAction = $$"""
        {
          "version": "1.0",
          "type": "action",
          "id": "act_reject",
          "timestamp": null,
          "payload": {
            "action": "reject",
            "target_id": "{{targetId}}"
          }
        }
        """;

        var actionRes = await client.PostAsync("/api/v1/actions", new StringContent(rejectAction, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, actionRes.StatusCode);
        using var actionDoc = JsonDocument.Parse(await actionRes.Content.ReadAsStringAsync());
        var actionPayload = actionDoc.RootElement.GetProperty("payload");
        Assert.True(actionPayload.GetProperty("accepted").GetBoolean());

        var hookRes = await hookTask;
        Assert.Equal(HttpStatusCode.OK, hookRes.StatusCode);
        using var hookDoc = JsonDocument.Parse(await hookRes.Content.ReadAsStringAsync());
        var hookSpecificOutput = hookDoc.RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PermissionRequest", hookSpecificOutput.GetProperty("hookEventName").GetString());
        var decision = hookSpecificOutput.GetProperty("decision");
        Assert.Equal("deny", decision.GetProperty("behavior").GetString());
    }

    [Fact]
    public async Task PostUnknownHook_Returns404()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var response = await client.PostAsync("/api/v1/hooks/unknown_agent", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("Unknown agent hook path", text);
    }

    [Theory]
    [InlineData("http://0.0.0.0:0")]
    [InlineData("http://192.168.1.2:8765")]
    [InlineData("https://127.0.0.1:0")]
    [InlineData("ftp://127.0.0.1:8765")]
    [InlineData("not_a_url")]
    public void Build_UrlOverrideValidation_RejectsInvalidUrls(string urlOverride)
    {
        Assert.Throws<ArgumentException>(() => AgentDeskServer.Build(Array.Empty<string>(), urlOverride));
    }
}
