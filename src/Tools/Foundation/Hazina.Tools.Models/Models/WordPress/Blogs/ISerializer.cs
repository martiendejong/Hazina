namespace DevGPT.GenerationTools.Models.WordPress.Blogs
{
    public interface ISerializer
    {
        void Save(string file);
        string Serialize();
    }
}

