using System.Text.Json;
using AgentDesk.Core.Models;
using AgentDesk.Core.Protocol;
using Xunit;

namespace AgentDesk.Core.Tests;

public class EnvelopeAndActionValidationTests
{
    [Theory]
    [InlineData("next")]
    [InlineData("next_project")]
    [InlineData("previous_project")]
    [InlineData("select_project")]
    [InlineData("usage")]
    [InlineData("usage_next")]
    [InlineData("clear")]
    [InlineData("approve")]
    [InlineData("reject")]
    public void Parse_ValidActions_ReturnsActionPayload(string actionName)
    {
        var json = $$"""
        {
          "version": "1.0",
          "type": "action",
          "id": "act_01",
          "timestamp": null,
          "payload": {
            "action": "{{actionName}}",
            "target_id": "target_123"
          }
        }
        """;

        var payload = ActionParser.Parse(json);
        Assert.Equal(actionName, payload.Action);
        Assert.Equal("target_123", payload.TargetId);
    }

    [Fact]
    public void Parse_NullTargetId_ReturnsNullTargetId()
    {
        var json = """
        {
          "version": "1.0",
          "type": "action",
          "id": "act_01",
          "timestamp": null,
          "payload": {
            "action": "next",
            "target_id": null
          }
        }
        """;

        var payload = ActionParser.Parse(json);
        Assert.Equal("next", payload.Action);
        Assert.Null(payload.TargetId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid json")]
    [InlineData("{}")]
    [InlineData("""{"version": "2.0", "type": "action", "payload": {"action": "next"}}""")]
    [InlineData("""{"version": "1.0", "type": "state", "payload": {"action": "next"}}""")]
    [InlineData("""{"version": "1.0", "type": "action", "payload": {}}""")]
    [InlineData("""{"version": "1.0", "type": "action", "payload": {"action": "invalid_action"}}""")]
    [InlineData("""{"version": "1.0", "type": "action", "payload": {"action": "next", "target_id": 123}}""")]
    public void Parse_InvalidJson_ThrowsArgumentException(string json)
    {
        Assert.Throws<ArgumentException>(() => ActionParser.Parse(json));
    }
}
