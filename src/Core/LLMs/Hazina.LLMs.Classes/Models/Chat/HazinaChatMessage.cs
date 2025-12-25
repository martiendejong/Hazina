#nullable enable

using System;

public class HazinaChatMessage
{
    public Guid MessageId { get; set; }
    public HazinaMessageRole Role { get; set; }
    public string Text { get; set; }
    public string AgentName { get; set; }
    public string FunctionName { get; set; }
    public string FlowName { get; set; }
    public string Response { get; set; }

    public HazinaChatMessage()
    {
        MessageId = Guid.NewGuid();
        Role = HazinaMessageRole.User;
        Text = string.Empty;
        AgentName = string.Empty;
        FunctionName = string.Empty;
        FlowName = string.Empty;
        Response = string.Empty;
    }
    public HazinaChatMessage(HazinaMessageRole role, string text) : this()
    {
        Role = role;
        Text = text;
    }
    public HazinaChatMessage(HazinaMessageRole role, string text, string agentName, string functionName, string flowName, string response)
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