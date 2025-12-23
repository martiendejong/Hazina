namespace DevGPT.GenerationTools.Data
{
    public class StoreSetup
    {
        public ILLMClient LLMClient { get; set; }
        public ITextEmbeddingStore TextEmbeddingStore { get; set; }
        public ITextStore TextStore { get; set; }
        public IDocumentPartStore DocumentPartStore { get; set; }
        public IDocumentStore Store { get; set; }
    }
}

