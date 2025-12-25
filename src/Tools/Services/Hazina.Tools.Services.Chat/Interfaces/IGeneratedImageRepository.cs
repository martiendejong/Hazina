using DevGPT.GenerationTools.Models;
using System.Threading.Tasks;

namespace DevGPT.GenerationTools.Services.Chat
{
    public interface IGeneratedImageRepository
    {
        string GetFolder(string projectId, string? userId);
        string GetMetadataFile(string projectId, string? userId);
        Task<string> SaveImageAsync(string projectId, string? userId, string fileName, byte[] data);
        void Add(GeneratedImageInfo info, string projectId, string? userId);
        SerializableList<GeneratedImageInfo> GetAll(string projectId, string? userId);
    }
}
