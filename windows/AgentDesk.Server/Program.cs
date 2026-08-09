using System.Globalization;
using System.Text.Json;
using AgentDesk.Core.Models;
using AgentDesk.Core.Protocol;
using AgentDesk.Core.State;
using AgentDesk.Core.Translators;
using AgentDesk.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(8765);
    options.Limits.MaxRequestBodySize = 64 * 1024;
});

builder.Services.AddSingleton<StateStore>();
builder.Services.AddSingleton<ApprovalBroker>();
builder.Services.AddSingleton<CodexTranslator>();
builder.Services.AddSingleton<SseBroadcaster>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    devices = 0,
    serial_port = (string?)null,
    usb_enabled = false
}));

app.MapGet("/api/v1/dashboard", async (StateStore store) =>
{
    var snapshot = await store.GetDashboardSnapshotAsync();
    return Results.Ok(snapshot);
});

app.MapGet("/api/v1/events", async (HttpContext context, SseBroadcaster broadcaster, StateStore store, CancellationToken cancellationToken) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    var subscriber = broadcaster.Subscribe();
    try
    {
        var snapshot = await store.GetDashboardSnapshotAsync();
        var initialJson = JsonSerializer.Serialize(snapshot, ProtocolSerializerOptions.Default);
        await context.Response.WriteAsync($"data: {initialJson}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var (data, isKeepAlive) = await subscriber.ReadNextAsync(TimeSpan.FromSeconds(15), cancellationToken);
            if (isKeepAlive)
            {
                await context.Response.WriteAsync(": keepalive\n\n", cancellationToken);
            }
            else if (data != null)
            {
                await context.Response.WriteAsync($"data: {data}\n\n", cancellationToken);
            }
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
    }
    finally
    {
        broadcaster.Unsubscribe(subscriber);
    }
});

app.MapPost("/api/v1/actions", async (HttpContext context, StateStore store, ApprovalBroker broker, SseBroadcaster broadcaster) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();

    ActionPayload actionPayload;
    try
    {
        actionPayload = ActionParser.Parse(body);
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync($"Invalid action payload: {ex.Message}");
        return;
    }

    var action = actionPayload.Action;
    var targetId = actionPayload.TargetId;
    bool accepted = false;

    if (action == DeviceAction.Usage || action == DeviceAction.UsageNext)
    {
        accepted = false;
    }
    else if (action == DeviceAction.Next)
    {
        accepted = (await store.NextSessionAsync()) != null;
    }
    else if (action == DeviceAction.NextProject)
    {
        accepted = (await store.NextProjectAsync()) != null;
    }
    else if (action == DeviceAction.PreviousProject)
    {
        accepted = (await store.PreviousProjectAsync()) != null;
    }
    else if (action == DeviceAction.SelectProject)
    {
        accepted = targetId != null && (await store.SelectProjectAsync(targetId)) != null;
    }
    else if (action == DeviceAction.Clear)
    {
        accepted = (await store.ClearCurrentAsync()) != null;
    }
    else if (action == DeviceAction.Approve || action == DeviceAction.Reject)
    {
        accepted = broker.Resolve(targetId, action);
    }

    var snapshot = await store.GetDashboardSnapshotAsync();
    await broadcaster.BroadcastAsync(snapshot);

    var resultEnvelope = new ProtocolEnvelope<ActionResultPayload>
    {
        Type = "action_result",
        Id = $"msg_{Guid.NewGuid():n}",
        Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        Payload = new ActionResultPayload
        {
            Accepted = accepted,
            Action = action
        }
    };

    context.Response.ContentType = "application/json";
    await JsonSerializer.SerializeAsync(context.Response.Body, resultEnvelope, ProtocolSerializerOptions.Default);
});

app.MapPost("/api/v1/hooks/{agent}", async (string agent, HttpContext context, CodexTranslator translator, StateStore store, ApprovalBroker broker, SseBroadcaster broadcaster, IConfiguration config) =>
{
    if (!string.Equals(agent, "codex", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 404;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("Unknown agent hook path");
        return;
    }

    JsonDocument doc;
    try
    {
        doc = await JsonDocument.ParseAsync(context.Request.Body);
    }
    catch (Exception)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("Invalid JSON");
        return;
    }

    using (doc)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("hook payload must be an object");
            return;
        }

        var update = translator.Translate(doc.RootElement);
        if (update.RemoveSession)
        {
            await store.RemoveAsync(update.State);
            var snapshot = await store.GetDashboardSnapshotAsync();
            await broadcaster.BroadcastAsync(snapshot);

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{}");
            return;
        }
        else if (update.WaitsForAction && !string.IsNullOrEmpty(update.State.TargetId))
        {
            var targetId = update.State.TargetId;
            var pendingTask = broker.Register(targetId);

            await store.UpsertAsync(update.State);
            var snapshot = await store.GetDashboardSnapshotAsync();
            await broadcaster.BroadcastAsync(snapshot);

            var timeoutSeconds = 300.0;

            bool TryParseTimeout(string? val, out double result)
            {
                if (!string.IsNullOrEmpty(val) &&
                    double.TryParse(val, CultureInfo.InvariantCulture, out var parsed) &&
                    parsed > 0 &&
                    !double.IsNaN(parsed) &&
                    !double.IsInfinity(parsed))
                {
                    result = parsed;
                    return true;
                }
                result = 0;
                return false;
            }

            var envTimeout = Environment.GetEnvironmentVariable("AGENTDECK_APPROVAL_TIMEOUT_SECONDS");
            if (TryParseTimeout(envTimeout, out var parsedEnv))
            {
                timeoutSeconds = parsedEnv;
            }
            else if (TryParseTimeout(config["ApprovalTimeoutSeconds"], out var parsedCfg))
            {
                timeoutSeconds = parsedCfg;
            }

            string? chosenAction = null;
            try
            {
                chosenAction = await pendingTask.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds));
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                broker.Discard(targetId);
            }

            if (chosenAction == DeviceAction.Approve)
            {
                update.State.RequiresAction = false;
                update.State.Actions = new();
                update.State.TargetId = null;
                update.State.Status = AgentStatus.Working;
                update.State.Message = "Approved from AgentDeck";
                await store.UpsertAsync(update.State);
                await broadcaster.BroadcastAsync(await store.GetDashboardSnapshotAsync());

                context.Response.ContentType = "application/json";
                var allowObj = new
                {
                    hookSpecificOutput = new
                    {
                        hookEventName = "PermissionRequest",
                        decision = new { behavior = "allow" }
                    }
                };
                await JsonSerializer.SerializeAsync(context.Response.Body, allowObj);
                return;
            }
            else if (chosenAction == DeviceAction.Reject)
            {
                update.State.RequiresAction = false;
                update.State.Actions = new();
                update.State.TargetId = null;
                update.State.Status = AgentStatus.Error;
                update.State.Message = "Rejected from AgentDeck";
                await store.UpsertAsync(update.State);
                await broadcaster.BroadcastAsync(await store.GetDashboardSnapshotAsync());

                context.Response.ContentType = "application/json";
                var denyObj = new
                {
                    hookSpecificOutput = new
                    {
                        hookEventName = "PermissionRequest",
                        decision = new { behavior = "deny", message = "Rejected from AgentDeck" }
                    }
                };
                await JsonSerializer.SerializeAsync(context.Response.Body, denyObj);
                return;
            }
            else
            {
                update.State.RequiresAction = false;
                update.State.Actions = new();
                update.State.TargetId = null;
                update.State.Message = "Approval timed out; use Codex";
                await store.UpsertAsync(update.State);
                await broadcaster.BroadcastAsync(await store.GetDashboardSnapshotAsync());

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{}");
                return;
            }
        }
        else
        {
            await store.UpsertAsync(update.State);
            var snapshot = await store.GetDashboardSnapshotAsync();
            await broadcaster.BroadcastAsync(snapshot);

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{}");
            return;
        }
    }
});

app.MapFallback((HttpContext context) =>
{
    context.Response.StatusCode = 404;
    context.Response.ContentType = "text/plain";
    return context.Response.WriteAsync("Not Found");
});

app.Run();

public partial class Program { }
