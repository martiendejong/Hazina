using DevGPT.GenerationTools.Models.WordPress.Blogs;
using DevGPT.GenerationTools.Data;
using System.Collections.Generic;
using DevGPT.GenerationTools.Models;
using DevGPTStore.Models;
using System.Linq;

namespace DevGPT.GenerationTools.AI.Agents
{
    public class DevGPTStoreAgent
    {
        public ProjectsRepository Projects;
        private readonly ProjectFileLocator _fileLocator;

        public DevGPTStoreAgent(ProjectsRepository projects)
        {
            Projects = projects;
            _fileLocator = new ProjectFileLocator(projects.ProjectsFolder);
        }

        protected void Store(string id, GeneratedDocument document, string file)
        {
            var filePath = _fileLocator.GetPath(id, file);
            document.Save(filePath);
        }

        protected void Store<T>(string id, GeneratedObject<T> document, string file) where T : Serializer<T>
        {
            var filePath = _fileLocator.GetPath(id, file);
            document.Save(filePath);
        }
    }
}

