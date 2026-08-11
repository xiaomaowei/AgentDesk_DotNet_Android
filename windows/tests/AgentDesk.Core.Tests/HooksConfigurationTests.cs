using System.Text.Json;
using Xunit;

namespace AgentDesk.Core.Tests;

public class HooksConfigurationTests
{
    private static readonly string[] LifecycleEvents =
    {
        "SessionStart",
        "SessionEnd",
        "UserPromptSubmit",
        "PreToolUse",
        "PermissionRequest",
        "PostToolUse",
        "Stop"
    };

    [Fact]
    public void WindowsLifecycleHandlers_ExecuteHookDirectlyWithOriginalSettings()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, ".codex", "hooks.json");
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var hooks = document.RootElement.GetProperty("hooks");

        Assert.Equal(LifecycleEvents.Length, hooks.EnumerateObject().Count());

        foreach (var lifecycleEvent in LifecycleEvents)
        {
            var handlerGroups = hooks.GetProperty(lifecycleEvent);
            var handlerGroup = Assert.Single(handlerGroups.EnumerateArray());
            var handler = Assert.Single(handlerGroup.GetProperty("hooks").EnumerateArray());
            var commandWindows = handler.GetProperty("commandWindows").GetString();

            Assert.NotNull(commandWindows);
            Assert.Equal(@".\windows\artifacts\AgentDesk.Hook-win-x64\AgentDesk.Hook.exe", commandWindows);
            Assert.DoesNotContain("./", commandWindows, StringComparison.Ordinal);
            Assert.DoesNotContain("powershell", commandWindows, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("-Command", commandWindows, StringComparison.Ordinal);
            Assert.DoesNotContain("ReadLine", commandWindows, StringComparison.Ordinal);
            Assert.DoesNotContain("$input", commandWindows, StringComparison.Ordinal);

            Assert.Equal(ExpectedTimeout(lifecycleEvent), handler.GetProperty("timeout").GetInt32());
            Assert.Equal(RequiresMatcher(lifecycleEvent), handlerGroup.TryGetProperty("matcher", out var matcher));
            if (RequiresMatcher(lifecycleEvent))
            {
                Assert.Equal("*", matcher.GetString());
            }

            if (lifecycleEvent == "PermissionRequest")
            {
                Assert.Equal("Waiting for AgentDesk approval", handler.GetProperty("statusMessage").GetString());
            }
            else
            {
                Assert.False(handler.TryGetProperty("statusMessage", out _));
            }
        }
    }

    private static int ExpectedTimeout(string lifecycleEvent) => lifecycleEvent switch
    {
        "SessionEnd" => 3,
        "PermissionRequest" => 315,
        _ => 10
    };

    private static bool RequiresMatcher(string lifecycleEvent) => lifecycleEvent is "PreToolUse" or "PermissionRequest" or "PostToolUse";
}
