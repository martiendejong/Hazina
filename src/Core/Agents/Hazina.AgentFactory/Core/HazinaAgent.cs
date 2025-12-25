public class HazinaAgent
{
    public string Name { get; set; }
    public DocumentGenerator Generator { get; set; }
    public IToolsContext Tools { get; set; }
    public bool IsCoder { get; set; } = false;

    public HazinaAgent(string name, DocumentGenerator generator, IToolsContext tools, bool isCoder = false)
    {
        Name = name;
        Generator = generator;
        Tools = tools;
        IsCoder = isCoder;
    }
}
