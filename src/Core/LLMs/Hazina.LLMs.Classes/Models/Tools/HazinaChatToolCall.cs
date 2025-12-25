
public class HazinaChatToolCall
{
    public string Id { get; }
    public string FunctionName { get; }
    public BinaryData FunctionArguments { get; }

    public HazinaChatToolCall(string id, string functionName, BinaryData functionArguments)
    {
        this.Id = id;
        this.FunctionName = functionName;
        this.FunctionArguments = functionArguments;
    }
}