using Hazina.Tools.Models.WordPress.Blogs;
using Hazina.Tools.Data;
using System.Collections.Generic;
using Hazina.Tools.Models;
using HazinaStore.Models;
using System.Linq;

namespace Hazina.Tools.AI.Agents
{
    public class HazinaStoreAgent
    {
        public ProjectsRepository Projects;
        private readonly ProjectFileLocator _fileLocator;

        public HazinaStoreAgent(ProjectsRepository projects)
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

