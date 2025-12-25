using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.Social;
using DevGPT.GenerationTools.Models.WordPress.Blogs;
using System;

namespace DevGPT.GenerationTools.Data
{
    /// <summary>
    /// Service responsible for WordPress and Social Media integration operations.
    /// Handles credential retrieval and social media comment file operations.
    /// </summary>
    public class ProjectIntegrationStore
    {
        private readonly ProjectsRepository _projectsRepository;
        private readonly ProjectFileLocator _fileLocator;

        public ProjectIntegrationStore(ProjectsRepository projectsRepository, ProjectFileLocator fileLocator)
        {
            _projectsRepository = projectsRepository ?? throw new ArgumentNullException(nameof(projectsRepository));
            _fileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
        }

        /// <summary>
        /// Retrieve WordPress credentials for a given project, ensuring all fields are present.
        /// Throws detailed exceptions on missing or invalid data.
        /// </summary>
        public WordPressCredentials GetWordPressCredentials(string projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
                throw new ArgumentException("'projectId' mag niet leeg zijn.", nameof(projectId));

            var project = _projectsRepository.Load(projectId);

            if (project.Wordpress == null)
                throw new ArgumentException("Wordpress gegevens zijn niet ingevuld.", nameof(projectId));

            var username = project.Wordpress.Username;
            var password = project.Wordpress.Password;
            var baseUrl = project.Wordpress.BaseUrl;

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException($"Wordpress 'baseUrl' ontbreekt in de projectinstellingen (Project: {projectId}).");
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException($"Wordpress gebruikersnaam ontbreekt in de projectinstellingen (Project: {projectId}).");
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException($"Wordpress wachtwoord ontbreekt in de projectinstellingen (Project: {projectId}).");

            return new WordPressCredentials(baseUrl, username, password);
        }

        public string GetExtractedSocialMediaCommentsFilePath(Project project, SocialMediaAddress address)
        {
            return _fileLocator.GetPath(project.Id, $"{address.Label}.json");
        }

        public string GetDownloadedSocialMediaCommentsFilePath(Project project, SocialMediaAddress address)
        {
            return _fileLocator.GetPath(project.Id, $"{address.Label}.xlsx");
        }

        public bool HasDownloadedSocialMediaComments(string projectId, SocialMediaAddress address)
        {
            var project = _projectsRepository.Load(projectId);
            return System.IO.File.Exists(GetDownloadedSocialMediaCommentsFilePath(project, address));
        }

        public bool HasExtractedSocialMediaComments(string projectId, SocialMediaAddress address)
        {
            var project = _projectsRepository.Load(projectId);
            return System.IO.File.Exists(GetExtractedSocialMediaCommentsFilePath(project, address));
        }

        public SocialMediaInfo LoadSocialMediaInfo(SocialMediaAddress address, Project project)
        {
            return SocialMediaInfo.Load(GetExtractedSocialMediaCommentsFilePath(project, address));
        }

        public void SaveSocialMediaInfo(SocialMediaAddress address, Project project, SocialMediaInfo soc)
        {
            soc.Save(GetExtractedSocialMediaCommentsFilePath(project, address));
        }
    }
}

