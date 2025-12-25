public partial class SimpleOpenAIClientChatInteraction
{
    class ToolCallData
    {
        public string FunctionName = "";
        public string ToolCallId = "";
        public List<BinaryData> BinaryData = new List<BinaryData>();
    }
}
