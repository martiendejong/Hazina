
public class DevGPTChatToolCall
{
    public string Id { get; }
    public string FunctionName { get; }
    public BinaryData FunctionArguments { get; }

    public DevGPTChatToolCall(string id, string functionName, BinaryData functionArguments)
    {
        this.Id = id;
        this.FunctionName = functionName;
        this.FunctionArguments = functionArguments;
    }
}