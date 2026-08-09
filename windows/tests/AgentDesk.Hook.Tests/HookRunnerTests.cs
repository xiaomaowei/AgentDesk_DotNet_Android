using System.Net;
using System.Text;
using AgentDesk.Hook;
using Xunit;

namespace AgentDesk.Hook.Tests;

public class HookRunnerTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"just a string\"")]
    public async Task InvalidInput_FailsOpenWithEmptyJsonObject(string inputStr)
    {
        using var stdin = new MemoryStream(Encoding.UTF8.GetBytes(inputStr));
        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();

        var env = new Dictionary<string, string?>();
        bool requestSent = false;

        using var handler = new MockHttpMessageHandler((req, ct) =>
        {
            requestSent = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = new HttpClient(handler);

        int exitCode = await HookRunner.RunAsync(stdin, stdoutWriter, stderrWriter, env, client);

        Assert.Equal(0, exitCode);
        Assert.Equal("{}", stdoutWriter.ToString());
        Assert.False(requestSent);
    }

    [Fact]
    public async Task NonLoopbackBridgeUrl_RejectedAndNoRequestSent()
    {
        string inputJson = "{\"hook_event_name\":\"PreToolUse\"}";
        using var stdin = new MemoryStream(Encoding.UTF8.GetBytes(inputJson));
        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();

        var env = new Dictionary<string, string?>
        {
            ["AGENTDECK_BRIDGE_URL"] = "http://192.168.1.100:8765"
        };

        bool requestSent = false;
        using var handler = new MockHttpMessageHandler((req, ct) =>
        {
            requestSent = true;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var client = new HttpClient(handler);

        int exitCode = await HookRunner.RunAsync(stdin, stdoutWriter, stderrWriter, env, client);

        Assert.Equal(0, exitCode);
        Assert.Equal("{}", stdoutWriter.ToString());
        Assert.False(requestSent);
        Assert.Contains("Invalid or forbidden bridge URL", stderrWriter.ToString());
    }

    [Fact]
    public async Task ServerUnavailable_FailsOpenWithEmptyJsonObject()
    {
        string inputJson = "{\"hook_event_name\":\"PreToolUse\"}";
        using var stdin = new MemoryStream(Encoding.UTF8.GetBytes(inputJson));
        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();

        var env = new Dictionary<string, string?>();

        using var handler = new MockHttpMessageHandler((req, ct) =>
        {
            throw new HttpRequestException("Connection refused");
        });
        using var client = new HttpClient(handler);

        int exitCode = await HookRunner.RunAsync(stdin, stdoutWriter, stderrWriter, env, client);

        Assert.Equal(0, exitCode);
        Assert.Equal("{}", stdoutWriter.ToString());
        Assert.Contains("HTTP forward error", stderrWriter.ToString());
    }

    [Fact]
    public async Task ServerReturnsNon2xx_FailsOpenWithEmptyJsonObject()
    {
        string inputJson = "{\"hook_event_name\":\"PreToolUse\"}";
        using var stdin = new MemoryStream(Encoding.UTF8.GetBytes(inputJson));
        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();

        var env = new Dictionary<string, string?>();

        using var handler = new MockHttpMessageHandler((req, ct) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });
        using var client = new HttpClient(handler);

        int exitCode = await HookRunner.RunAsync(stdin, stdoutWriter, stderrWriter, env, client);

        Assert.Equal(0, exitCode);
        Assert.Equal("{}", stdoutWriter.ToString());
        Assert.Contains("Server returned non-2xx status code: 500", stderrWriter.ToString());
    }

    [Fact]
    public async Task ServerReturnsPermissionJsonResponse_PassesStdoutBodyUnchanged()
    {
        string inputJson = "{\"hook_event_name\":\"PermissionRequest\"}";
        using var stdin = new MemoryStream(Encoding.UTF8.GetBytes(inputJson));
        using var stdoutWriter = new StringWriter();
        using var stderrWriter = new StringWriter();

        var env = new Dictionary<string, string?>();
        string expectedServerResponseBody = "{\"hookSpecificOutput\":{\"hookEventName\":\"PermissionRequest\",\"decision\":{\"behavior\":\"allow\"}}}";

        using var handler = new MockHttpMessageHandler((req, ct) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expectedServerResponseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);
        });
        using var client = new HttpClient(handler);

        int exitCode = await HookRunner.RunAsync(stdin, stdoutWriter, stderrWriter, env, client);

        Assert.Equal(0, exitCode);
        Assert.Equal(expectedServerResponseBody, stdoutWriter.ToString());
        Assert.Empty(stderrWriter.ToString());
    }
}
