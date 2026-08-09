using System.Text.Json;
using Xunit;

namespace AgentDesk.Hook.Tests;

public class ProjectHookConfigTests
{
    [Fact]
    public void ProjectHooksJson_IsValidAndRegistersAllSevenHooks()
    {
        string projectRoot = GetProjectRoot();
        string hooksJsonPath = Path.Combine(projectRoot, ".codex", "hooks.json");

        Assert.True(File.Exists(hooksJsonPath), $"Expected .codex/hooks.json to exist at {hooksJsonPath}");

        string jsonContent = File.ReadAllText(hooksJsonPath);
        using var doc = JsonDocument.Parse(jsonContent);

        JsonElement root = doc.RootElement;
        Assert.True(root.TryGetProperty("hooks", out JsonElement hooksElement), "Root element must contain 'hooks' property");

        string expectedCmdWindows = @"powershell -WindowStyle Hidden -NoProfile -Command '$root = git rev-parse --show-toplevel; & ""$root\windows\artifacts\AgentDesk.Hook-win-x64\AgentDesk.Hook.exe""'";
        string expectedCmdPosix = "./windows/artifacts/AgentDesk.Hook-win-x64/AgentDesk.Hook";

        string[] requiredHooks = ["SessionStart", "SessionEnd", "UserPromptSubmit", "PreToolUse", "PermissionRequest", "PostToolUse", "Stop"];
        foreach (string hookName in requiredHooks)
        {
            Assert.True(hooksElement.TryGetProperty(hookName, out JsonElement eventArray), $"Hooks configuration must contain event registration for '{hookName}'");
            Assert.Equal(JsonValueKind.Array, eventArray.ValueKind);
            Assert.True(eventArray.GetArrayLength() > 0, $"Event array for '{hookName}' must not be empty");

            JsonElement firstGroup = eventArray[0];
            Assert.True(firstGroup.TryGetProperty("hooks", out JsonElement handlersArray), $"Event group for '{hookName}' must contain 'hooks' array");
            Assert.True(handlersArray.GetArrayLength() > 0, $"Handler array for '{hookName}' must not be empty");

            JsonElement handler = handlersArray[0];
            Assert.True(handler.TryGetProperty("type", out JsonElement typeElem), $"Handler for '{hookName}' must have 'type'");
            Assert.Equal("command", typeElem.GetString());

            Assert.True(handler.TryGetProperty("timeout", out JsonElement timeoutElem), $"Handler for '{hookName}' must specify 'timeout'");
            int timeout = timeoutElem.GetInt32();

            if (hookName == "SessionEnd")
            {
                Assert.Equal(3, timeout);
            }
            else if (hookName == "PermissionRequest")
            {
                Assert.True(timeout >= 300, $"PermissionRequest timeout should be long blocking, got {timeout}");
                Assert.True(firstGroup.TryGetProperty("matcher", out JsonElement matcherElem), "PermissionRequest group must specify matcher");
                Assert.Equal("*", matcherElem.GetString());
                Assert.True(handler.TryGetProperty("statusMessage", out JsonElement statusMsgElem), "PermissionRequest handler must have statusMessage");
                Assert.False(string.IsNullOrWhiteSpace(statusMsgElem.GetString()), "PermissionRequest statusMessage must not be empty");
            }
            else
            {
                Assert.Equal(10, timeout);
                if (hookName is "PreToolUse" or "PostToolUse")
                {
                    Assert.True(firstGroup.TryGetProperty("matcher", out JsonElement matcherElem), $"{hookName} group must specify matcher");
                    Assert.Equal("*", matcherElem.GetString());
                }
            }

            Assert.True(handler.TryGetProperty("commandWindows", out JsonElement cmdWinElem), $"Handler for '{hookName}' must specify 'commandWindows'");
            string cmdWin = cmdWinElem.GetString() ?? "";

            // Assert Windows command structure: single-quoted -Command body, no literal backslash-quote, exact pattern
            Assert.Contains("-Command '$root", cmdWin);
            Assert.DoesNotContain("\\\"", cmdWin);
            Assert.Equal(expectedCmdWindows, cmdWin);

            Assert.True(handler.TryGetProperty("command", out JsonElement cmdElem), $"Handler for '{hookName}' must specify 'command'");
            string cmd = cmdElem.GetString() ?? "";

            // Assert POSIX command structure: starts with ./ and matches relative path
            Assert.StartsWith("./", cmd);
            Assert.Equal(expectedCmdPosix, cmd);
        }
    }

    private static string GetProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, ".codex", "hooks.json");
            if (File.Exists(candidate))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not find .codex/hooks.json traversing up from " + AppContext.BaseDirectory);
    }
}
