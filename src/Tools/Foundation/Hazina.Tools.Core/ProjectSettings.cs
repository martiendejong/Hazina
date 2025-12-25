using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hazina.Tools.Models;
using Hazina.Tools.Models.WordPress.Blogs;
using LegacyProject = Hazina.Tools.Models.Project;

namespace HazinaStore.Core
{
    /// <summary>
    /// Manages project-specific settings and configuration
    /// Handles custom prompts, action files, and WordPress settings
    /// </summary>
    public class ProjectSettings
    {
        private readonly string _projectPath;
        private readonly Project _project;

        /// <summary>
        /// Path to the project folder
        /// </summary>
        public string ProjectPath => _projectPath;

        /// <summary>
        /// Parent project
        /// </summary>
        public Project Project => _project;

        /// <summary>
        /// Initialize project settings
        /// </summary>
        /// <param name="projectPath">Path to project folder</param>
        /// <param name="project">Parent project</param>
        internal ProjectSettings(string projectPath, Project project)
        {
            _projectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            _project = project ?? throw new ArgumentNullException(nameof(project));
        }

        #region Custom Prompt

        /// <summary>
        /// Get the project-specific custom prompt
        /// </summary>
        /// <returns>Custom prompt text or null if not set</returns>
        public string GetCustomPrompt()
        {
            // Custom prompts are stored in the project metadata
            return _project.Metadata.CustomFields.ContainsKey("KlantSpecifiekePrompt")
                ? _project.Metadata.CustomFields["KlantSpecifiekePrompt"]
                : null;
        }

        /// <summary>
        /// Set the project-specific custom prompt
        /// </summary>
        /// <param name="prompt">Custom prompt text</param>
        public void SetCustomPrompt(string prompt)
        {
            var metadata = _project.Metadata;
            metadata.CustomFields["KlantSpecifiekePrompt"] = prompt ?? string.Empty;
            _project.UpdateMetadata(metadata);
        }

        #endregion

        #region Action Files (Prompt Overrides)

        /// <summary>
        /// Get all action files (*.actions)
        /// </summary>
        /// <returns>Dictionary of action name to content</returns>
        public Dictionary<string, string> GetActionFiles()
        {
            var actions = new Dictionary<string, string>();

            var actionFiles = Directory.GetFiles(_projectPath, "*.actions", SearchOption.TopDirectoryOnly);

            foreach (var actionFile in actionFiles)
            {
                var actionName = Path.GetFileNameWithoutExtension(actionFile);
                var content = File.ReadAllText(actionFile);
                actions[actionName] = content;
            }

            return actions;
        }

        /// <summary>
        /// Get a specific action file content
        /// </summary>
        /// <param name="actionName">Action name (without .actions extension)</param>
        /// <returns>Action content or null if not found</returns>
        public string GetActionFile(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return null;

            var actionFilePath = Path.Combine(_projectPath, $"{actionName}.actions");

            if (!File.Exists(actionFilePath))
                return null;

            return File.ReadAllText(actionFilePath);
        }

        /// <summary>
        /// Save an action file
        /// </summary>
        /// <param name="actionName">Action name (without .actions extension)</param>
        /// <param name="content">Action content</param>
        public void SaveActionFile(string actionName, string content)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                throw new ArgumentException("Action name cannot be empty", nameof(actionName));

            var actionFilePath = Path.Combine(_projectPath, $"{actionName}.actions");
            File.WriteAllText(actionFilePath, content ?? string.Empty);
        }

        /// <summary>
        /// Delete an action file
        /// </summary>
        /// <param name="actionName">Action name (without .actions extension)</param>
        public void DeleteActionFile(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return;

            var actionFilePath = Path.Combine(_projectPath, $"{actionName}.actions");

            if (File.Exists(actionFilePath))
                File.Delete(actionFilePath);
        }

        /// <summary>
        /// Check if an action file exists
        /// </summary>
        /// <param name="actionName">Action name (without .actions extension)</param>
        /// <returns>True if action file exists</returns>
        public bool ActionFileExists(string actionName)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return false;

            var actionFilePath = Path.Combine(_projectPath, $"{actionName}.actions");
            return File.Exists(actionFilePath);
        }

        #endregion

        #region WordPress Settings

        /// <summary>
        /// Get WordPress settings for this project
        /// </summary>
        /// <returns>WordPress settings or null if not configured</returns>
        public WordpressSettings GetWordpressSettings()
        {
            // WordPress settings are stored in the old Project model
            // We need to load the project JSON and extract them
            var projectFile = Path.Combine(_projectPath, $"{_project.Id}.json");

            if (!File.Exists(projectFile))
                return null;

            try
            {
                var legacyProject = LegacyProject.Load(projectFile);
                return legacyProject.Wordpress;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading WordPress settings: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save WordPress settings for this project
        /// </summary>
        /// <param name="settings">WordPress settings</param>
        public void SaveWordpressSettings(WordpressSettings settings)
        {
            // Update the old Project model with WordPress settings
            var projectFile = Path.Combine(_projectPath, $"{_project.Id}.json");

            if (!File.Exists(projectFile))
                throw new FileNotFoundException("Project file not found");

            var project = LegacyProject.Load(projectFile);
            project.Wordpress = settings;
            project.Save(projectFile);
        }

        #endregion

        #region Prompt Files (*.prompts.*.json)

        /// <summary>
        /// Get all prompt files in the project
        /// </summary>
        /// <returns>Dictionary of prompt file name to content</returns>
        public Dictionary<string, string> GetPromptFiles()
        {
            var prompts = new Dictionary<string, string>();

            var promptFiles = Directory.GetFiles(_projectPath, "*.prompts.*.json", SearchOption.TopDirectoryOnly);

            foreach (var promptFile in promptFiles)
            {
                var filename = Path.GetFileName(promptFile);
                var content = File.ReadAllText(promptFile);
                prompts[filename] = content;
            }

            return prompts;
        }

        /// <summary>
        /// Get a specific prompt file content
        /// </summary>
        /// <param name="filename">Prompt filename (e.g., "project.prompts.config.json")</param>
        /// <returns>Prompt content or null if not found</returns>
        public string GetPromptFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            var promptFilePath = Path.Combine(_projectPath, filename);

            if (!File.Exists(promptFilePath))
                return null;

            return File.ReadAllText(promptFilePath);
        }

        /// <summary>
        /// Save a prompt file
        /// </summary>
        /// <param name="filename">Prompt filename</param>
        /// <param name="content">Prompt content (JSON)</param>
        public void SavePromptFile(string filename, string content)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename cannot be empty", nameof(filename));

            var promptFilePath = Path.Combine(_projectPath, filename);
            File.WriteAllText(promptFilePath, content ?? "{}");
        }

        /// <summary>
        /// Delete a prompt file
        /// </summary>
        /// <param name="filename">Prompt filename</param>
        public void DeletePromptFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return;

            var promptFilePath = Path.Combine(_projectPath, filename);

            if (File.Exists(promptFilePath))
                File.Delete(promptFilePath);
        }

        #endregion

        #region Content Planning Files

        /// <summary>
        /// Get content haakjes data
        /// </summary>
        /// <returns>Content haakjes JSON or null if not found</returns>
        public string GetContentHooks()
        {
            return GetProjectFile("contenthooks.json");
        }

        /// <summary>
        /// Save content haakjes data
        /// </summary>
        /// <param name="content">JSON content</param>
        public void SaveContentHooks(string content)
        {
            SaveProjectFile("contenthooks.json", content);
        }

        /// <summary>
        /// Get blog categories data
        /// </summary>
        /// <returns>Blog categories JSON or null if not found</returns>
        public string GetBlogCategories()
        {
            return GetProjectFile("BlogCategories.json");
        }

        /// <summary>
        /// Save blog categories data
        /// </summary>
        /// <param name="content">JSON content</param>
        public void SaveBlogCategories(string content)
        {
            SaveProjectFile("BlogCategories.json", content);
        }

        /// <summary>
        /// Get content planning data
        /// </summary>
        /// <returns>Content planning JSON or null if not found</returns>
        public string GetContentPlanning()
        {
            return GetProjectFile("ContentPlanning.json");
        }

        /// <summary>
        /// Save content planning data
        /// </summary>
        /// <param name="content">JSON content</param>
        public void SaveContentPlanning(string content)
        {
            SaveProjectFile("ContentPlanning.json", content);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get a project file content
        /// </summary>
        /// <param name="filename">Filename</param>
        /// <returns>File content or null if not found</returns>
        private string GetProjectFile(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            var filePath = Path.Combine(_projectPath, filename);

            if (!File.Exists(filePath))
                return null;

            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Save a project file
        /// </summary>
        /// <param name="filename">Filename</param>
        /// <param name="content">File content</param>
        private void SaveProjectFile(string filename, string content)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename cannot be empty", nameof(filename));

            var filePath = Path.Combine(_projectPath, filename);
            File.WriteAllText(filePath, content ?? string.Empty);
        }

        #endregion
    }
}
