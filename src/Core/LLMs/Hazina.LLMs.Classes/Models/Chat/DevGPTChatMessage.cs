#nullable enable

using System;

public class DevGPTChatMessage
{
    public Guid MessageId { get; set; }
    public DevGPTMessageRole Role { get; set; }
    public string Text { get; set; }
    public string AgentName { get; set; }
    public string FunctionName { get; set; }
    public string FlowName { get; set; }
    public string Response { get; set; }

    public DevGPTChatMessage()
    {
        MessageId = Guid.NewGuid();
        Role = DevGPTMessageRole.User;
        Text = string.Empty;
        AgentName = string.Empty;
        FunctionName = string.Empty;
        FlowName = string.Empty;
        Response = string.Empty;
    }
    public DevGPTChatMessage(DevGPTMessageRole role, string text) : this()
    {
        Role = role;
        Text = text;
    }
    public DevGPTChatMessage(DevGPTMessageRole role, string text, string agentName, string functionName, string flowName, string response)
    {
        MessageId = Guid.NewGuid();
        Role = role;
        Text = text;
        AgentName = agentName;
        FunctionName = functionName;
        FlowName = flowName;
        Response = response;
    }
}