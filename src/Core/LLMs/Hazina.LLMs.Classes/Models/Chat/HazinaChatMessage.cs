#nullable enable

using System;

/// <summary>
/// Represents a chat message in the Hazina LLM system.
/// </summary>
public class HazinaChatMessage
{
    /// <summary>
    /// Gets or sets the unique identifier for this message.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the role of the message sender.
    /// </summary>
    public HazinaMessageRole Role { get; set; } = HazinaMessageRole.User;

    /// <summary>
    /// Gets or sets the text content of the message.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the agent.
    /// </summary>
    public string AgentName { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the function.
    /// </summary>
    public string FunctionName { get; set; } = "";

    /// <summary>
    /// Gets or sets the name of the flow.
    /// </summary>
    public string FlowName { get; set; } = "";

    /// <summary>
    /// Gets or sets the response content.
    /// </summary>
    public string Response { get; set; } = "";

    /// <summary>
    /// Initializes a new instance of the HazinaChatMessage class.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the HazinaChatMessage class with the specified role and text.
    /// </summary>
    /// <param name="role">The role of the message sender.</param>
    /// <param name="text">The text content of the message.</param>
    public HazinaChatMessage(HazinaMessageRole role, string text) : this()
    {
        Role = role;
        Text = text;
    }

    /// <summary>
    /// Initializes a new instance of the HazinaChatMessage class with full details.
    /// </summary>
    /// <param name="role">The role of the message sender.</param>
    /// <param name="text">The text content of the message.</param>
    /// <param name="agentName">The name of the agent.</param>
    /// <param name="functionName">The name of the function.</param>
    /// <param name="flowName">The name of the flow.</param>
    /// <param name="response">The response content.</param>
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