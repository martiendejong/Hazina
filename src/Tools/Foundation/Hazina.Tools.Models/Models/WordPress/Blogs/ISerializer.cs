namespace Hazina.Tools.Models.WordPress.Blogs
{
    public interface ISerializer
    {
        void Save(string file);
        string Serialize();
    }
}

