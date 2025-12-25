using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using HazinaStore.Models;

namespace Hazina.Tools.Data
{
    /// <summary>
    /// Service responsible for project-global configuration and settings.
    /// Handles blog categories, snel aanpassen, basis prompt, and user info.
    /// </summary>
    public class ProjectGlobalSettingsRepository
    {
        private readonly string _projectsFolder;

        public ProjectGlobalSettingsRepository(string projectsFolder)
        {
            _projectsFolder = projectsFolder ?? throw new ArgumentNullException(nameof(projectsFolder));
        }

        public BlogCategoriesClass LoadBlogCategories(Project project, ProjectFileLocator fileLocator)
        {
            var catsJsonFile = fileLocator.GetBlogCategoriesFile(project.Id);
            return BlogCategoriesClass.Load(catsJsonFile);
        }

        public List<KeyValuePair<string, string>> LoadSnelAanpassen()
        {
            var filePath = Path.Combine(_projectsFolder, ProjectFileLocator.SnelAanpassenFile);
            List<KeyValuePair<string, string>> data;
            if (File.Exists(filePath))
            {
                var text = File.ReadAllText(filePath);
                data = JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(text) ?? new List<KeyValuePair<string, string>>();
            }
            else
            {
                data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("Meer diepgang", "Geef de tekst meer diepgang."),
                    new KeyValuePair<string, string>("Eenvoudiger", "Maak de tekst meer eenvoudiger."),
                    new KeyValuePair<string, string>("Commercieler", "Maak de tekst meer commercieler."),
                    new KeyValuePair<string, string>("Minder commercieel", "Maak de tekst meer informeel."),
                    new KeyValuePair<string, string>("Langer maken", "Maak de tekst langer."),
                    new KeyValuePair<string, string>("Korter maken", "Maak de tekst korter."),
                    new KeyValuePair<string, string>("Formeler", "Maak de tekst meer formeel."),
                    new KeyValuePair<string, string>("Informeler", "Maak de tekst meer informeel."),
                    new KeyValuePair<string, string>("Zeg het anders", "Schrijf de tekst anders."),
                };
            }

            return data;
        }

        public void SaveSnelAanpassen(List<KeyValuePair<string, string>> snelAanpassen)
        {
            var data = JsonSerializer.Serialize(snelAanpassen);
            var filePath = Path.Combine(_projectsFolder, ProjectFileLocator.SnelAanpassenFile);
            File.WriteAllText(filePath, data);
        }

        public string LoadBasisPrompt()
        {
            var basisPromptFilePath = Path.Combine(_projectsFolder, ProjectFileLocator.BasisPromptFile);
            if (File.Exists(basisPromptFilePath))
                return File.ReadAllText(basisPromptFilePath);
            return "***Dit is de basis systeem prompt***";
        }

        public void SaveBasisPrompt(string prompt)
        {
            var basisPromptFilePath = Path.Combine(_projectsFolder, ProjectFileLocator.BasisPromptFile);
            File.WriteAllText(basisPromptFilePath, prompt);
        }

        public List<IHazinaStoreUserInfo> LoadUserInfos()
        {
            var usersFilePath = Path.Combine(_projectsFolder, ProjectFileLocator.UsersFile);
            if (File.Exists(usersFilePath))
            {
                var usersText = File.ReadAllText(usersFilePath);
                var info = JsonSerializer.Deserialize<List<HazinaStoreUserInfo>>(usersText);
                return info.Select(i => i as IHazinaStoreUserInfo).ToList();
            }
            else
            {
                return new List<IHazinaStoreUserInfo>();
            }
        }

        public void SaveUserInfos(List<IHazinaStoreUserInfo> infos)
        {
            var usersFilePath = Path.Combine(_projectsFolder, ProjectFileLocator.UsersFile);
            var json = JsonSerializer.Serialize(infos);
            File.WriteAllText(usersFilePath, json);
        }
    }
}

