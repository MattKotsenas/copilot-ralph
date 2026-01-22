// Tests for SDK events.

using Ralph.Cli.Sdk;

namespace Ralph.Tests;

[TestClass]
public sealed class SdkEventsTests
{
    [TestMethod]
    public void TextEvent_HasCorrectType()
    {
        var textEvent = new TextEvent { Text = "Hello" };

        Assert.AreEqual(EventType.Text, textEvent.Type);
        Assert.AreEqual("Hello", textEvent.Text);
        Assert.IsFalse(textEvent.Reasoning);
    }

    [TestMethod]
    public void TextEvent_WithReasoning_HasFlag()
    {
        var textEvent = new TextEvent { Text = "Thinking...", Reasoning = true };

        Assert.IsTrue(textEvent.Reasoning);
    }

    [TestMethod]
    public void ToolCallEvent_HasCorrectType()
    {
        var toolCall = new ToolCall { Name = "read_file" };
        var toolCallEvent = new ToolCallEvent { ToolCall = toolCall };

        Assert.AreEqual(EventType.ToolCall, toolCallEvent.Type);
        Assert.AreEqual("read_file", toolCallEvent.ToolCall.Name);
    }

    [TestMethod]
    public void ToolResultEvent_HasCorrectType()
    {
        var toolCall = new ToolCall { Name = "read_file" };
        var toolResultEvent = new ToolResultEvent { ToolCall = toolCall, Result = "content" };

        Assert.AreEqual(EventType.ToolResult, toolResultEvent.Type);
        Assert.AreEqual("content", toolResultEvent.Result);
    }

    [TestMethod]
    public void ToolResultEvent_WithError_HasError()
    {
        var toolCall = new ToolCall { Name = "read_file" };
        var error = new Exception("File not found");
        var toolResultEvent = new ToolResultEvent { ToolCall = toolCall, Error = error };

        Assert.AreEqual(error, toolResultEvent.Error);
    }

    [TestMethod]
    public void ErrorEvent_HasCorrectType()
    {
        var error = new Exception("Test error");
        var errorEvent = new ErrorEvent { Error = error };

        Assert.AreEqual(EventType.Error, errorEvent.Type);
        Assert.AreEqual(error, errorEvent.Error);
    }

    [TestMethod]
    public void AllEvents_HaveTimestamp()
    {
        var before = DateTime.UtcNow;

        var textEvent = new TextEvent { Text = "test" };
        var toolCallEvent = new ToolCallEvent { ToolCall = new ToolCall { Name = "test" } };
        var toolResultEvent = new ToolResultEvent { ToolCall = new ToolCall { Name = "test" } };
        var errorEvent = new ErrorEvent { Error = new Exception() };

        var after = DateTime.UtcNow;

        Assert.IsTrue(textEvent.Timestamp >= before && textEvent.Timestamp <= after);
        Assert.IsTrue(toolCallEvent.Timestamp >= before && toolCallEvent.Timestamp <= after);
        Assert.IsTrue(toolResultEvent.Timestamp >= before && toolResultEvent.Timestamp <= after);
        Assert.IsTrue(errorEvent.Timestamp >= before && errorEvent.Timestamp <= after);
    }
}

[TestClass]
public sealed class ToolCallTests
{
    [TestMethod]
    public void ToolCall_HasAllProperties()
    {
        var parameters = new Dictionary<string, object?> { ["path"] = "file.txt" };
        var toolCall = new ToolCall
        {
            Id = "123",
            Name = "read_file",
            Parameters = parameters
        };

        Assert.AreEqual("123", toolCall.Id);
        Assert.AreEqual("read_file", toolCall.Name);
        Assert.AreEqual(parameters, toolCall.Parameters);
    }

    [TestMethod]
    public void ToolCall_DefaultParameters_IsEmptyDictionary()
    {
        var toolCall = new ToolCall { Name = "test" };

        Assert.IsNotNull(toolCall.Parameters);
        Assert.AreEqual(0, toolCall.Parameters.Count);
    }
}
