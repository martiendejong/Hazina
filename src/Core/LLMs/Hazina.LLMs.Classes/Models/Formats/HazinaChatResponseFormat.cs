public class HazinaChatResponseFormat
{
    public string Format;
    protected HazinaChatResponseFormat(string format) => Format = format;
    public static readonly HazinaChatResponseFormat Text = new HazinaChatResponseFormat("text");
    public static readonly HazinaChatResponseFormat Json = new HazinaChatResponseFormat("json");

    // Add static method to mimic 'CreateTextFormat' as used in Crosslink
    public static HazinaChatResponseFormat CreateTextFormat()
    {
        return Text;
    }
}
