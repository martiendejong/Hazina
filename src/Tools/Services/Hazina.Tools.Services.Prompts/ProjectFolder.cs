using System.IO;
using System.Text.Json;

namespace HazinaStore
{
    public class ProjectFolder
    {
        private readonly string _projectsFolder;
        public ProjectFolder(string projectsFolder)
        {
            _projectsFolder = projectsFolder;
        }

        public T GetProjectFile<T>(string filename) where T : new()
        {
            var path = Path.Combine(_projectsFolder, filename);
            if (!File.Exists(path)) return new T();
            var json = File.ReadAllText(path);
            try { var obj = JsonSerializer.Deserialize<T>(json); return obj == null ? new T() : obj; }
            catch { return new T(); }
        }

        public void StoreProjectFile<T>(string filename, T content)
        {
            var path = Path.Combine(_projectsFolder, filename);
            var json = JsonSerializer.Serialize(content);
            File.WriteAllText(path, json);
        }
    }
}

