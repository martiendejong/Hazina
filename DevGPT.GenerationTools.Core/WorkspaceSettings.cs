using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DevGPTStore.Core
{
    /// <summary>
    /// Manages workspace-wide settings and configuration
    /// Handles prompts, conversation starters, and other global settings
    /// </summary>
    public class WorkspaceSettings
    {
        private readonly string _workspacePath;

        private const string BasisPromptFile = "basisprompt.txt";
        private const string RolePromptsFile = "roleprompts.json";
        private const string SnelAanpassenFile = "snelaanpassen.txt";
        private const string ConversationStartersFile = "ConversationStarters.json";
        private const string UsersFile = "users.json";

        /// <summary>
        /// Path to the workspace folder
        /// </summary>
        public string WorkspacePath => _workspacePath;

        /// <summary>
        /// Initialize workspace settings
        /// </summary>
        /// <param name="workspacePath">Path to workspace folder</param>
        internal WorkspaceSettings(string workspacePath)
        {
            _workspacePath = workspacePath ?? throw new ArgumentNullException(nameof(workspacePath));
        }

        #region Basis Prompt

        /// <summary>
        /// Get the basis (system) prompt
        /// </summary>
        /// <returns>Basis prompt text</returns>
        public string GetBasisPrompt()
        {
            var filePath = Path.Combine(_workspacePath, BasisPromptFile);

            if (!File.Exists(filePath))
                return "***Dit is de basis systeem prompt***";

            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Set the basis (system) prompt
        /// </summary>
        /// <param name="prompt">Prompt text</param>
        public void SetBasisPrompt(string prompt)
        {
            var filePath = Path.Combine(_workspacePath, BasisPromptFile);
            File.WriteAllText(filePath, prompt ?? string.Empty);
        }

        #endregion

        #region Role Prompts

        /// <summary>
        /// Get all role prompts
        /// </summary>
        /// <returns>Dictionary of role prompts</returns>
        public Dictionary<string, string> GetRolePrompts()
        {
            var filePath = Path.Combine(_workspacePath, RolePromptsFile);

            if (!File.Exists(filePath))
                return new Dictionary<string, string>();

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading role prompts: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Save role prompts
        /// </summary>
        /// <param name="rolePrompts">Dictionary of role prompts</param>
        public void SaveRolePrompts(Dictionary<string, string> rolePrompts)
        {
            var filePath = Path.Combine(_workspacePath, RolePromptsFile);
            var json = JsonSerializer.Serialize(rolePrompts, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Get a specific role prompt
        /// </summary>
        /// <param name="roleName">Role name</param>
        /// <returns>Prompt text or null if not found</returns>
        public string GetRolePrompt(string roleName)
        {
            var prompts = GetRolePrompts();
            return prompts.ContainsKey(roleName) ? prompts[roleName] : null;
        }

        /// <summary>
        /// Set a specific role prompt
        /// </summary>
        /// <param name="roleName">Role name</param>
        /// <param name="prompt">Prompt text</param>
        public void SetRolePrompt(string roleName, string prompt)
        {
            var prompts = GetRolePrompts();
            prompts[roleName] = prompt;
            SaveRolePrompts(prompts);
        }

        #endregion

        #region Snel Aanpassen

        /// <summary>
        /// Get quick adjustment options (snel aanpassen)
        /// </summary>
        /// <returns>List of quick adjustment options</returns>
        public List<KeyValuePair<string, string>> GetSnelAanpassen()
        {
            var filePath = Path.Combine(_workspacePath, SnelAanpassenFile);

            if (!File.Exists(filePath))
            {
                // Return defaults
                return new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("Meer diepgang", "Geef de tekst meer diepgang."),
                    new KeyValuePair<string, string>("Eenvoudiger", "Maak de tekst meer eenvoudiger."),
                    new KeyValuePair<string, string>("Commercieler", "Maak de tekst meer commercieler."),
                    new KeyValuePair<string, string>("Minder commercieel", "Maak de tekst meer informeel."),
                    new KeyValuePair<string, string>("Langer maken", "Maak de tekst langer."),
                    new KeyValuePair<string, string>("Korter maken", "Maak de tekst korter."),
                    new KeyValuePair<string, string>("Formeler", "Maak de tekst meer formeel."),
                    new KeyValuePair<string, string>("Informeler", "Maak de tekst meer informeel."),
                    new KeyValuePair<string, string>("Zeg het anders", "Schrijf de tekst anders.")
                };
            }

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<KeyValuePair<string, string>>>(json) ?? new List<KeyValuePair<string, string>>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading snel aanpassen: {ex.Message}");
                return new List<KeyValuePair<string, string>>();
            }
        }

        /// <summary>
        /// Save quick adjustment options
        /// </summary>
        /// <param name="snelAanpassen">List of options</param>
        public void SaveSnelAanpassen(List<KeyValuePair<string, string>> snelAanpassen)
        {
            var filePath = Path.Combine(_workspacePath, SnelAanpassenFile);
            var json = JsonSerializer.Serialize(snelAanpassen, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        #endregion

        #region Conversation Starters

        /// <summary>
        /// Get conversation starters configuration
        /// </summary>
        /// <returns>Conversation starters data</returns>
        public string GetConversationStarters()
        {
            var filePath = Path.Combine(_workspacePath, ConversationStartersFile);

            if (!File.Exists(filePath))
                return "[]";

            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Save conversation starters configuration
        /// </summary>
        /// <param name="conversationStarters">JSON string of conversation starters</param>
        public void SaveConversationStarters(string conversationStarters)
        {
            var filePath = Path.Combine(_workspacePath, ConversationStartersFile);
            File.WriteAllText(filePath, conversationStarters ?? "[]");
        }

        #endregion

        #region Users

        /// <summary>
        /// Get users configuration
        /// </summary>
        /// <returns>Users data as JSON string</returns>
        public string GetUsers()
        {
            var filePath = Path.Combine(_workspacePath, UsersFile);

            if (!File.Exists(filePath))
                return "[]";

            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Save users configuration
        /// </summary>
        /// <param name="users">JSON string of users</param>
        public void SaveUsers(string users)
        {
            var filePath = Path.Combine(_workspacePath, UsersFile);
            File.WriteAllText(filePath, users ?? "[]");
        }

        #endregion

        #region Custom Settings

        /// <summary>
        /// Get a custom setting file content
        /// </summary>
        /// <param name="filename">Setting filename</param>
        /// <returns>File content or null if not found</returns>
        public string GetCustomSetting(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            var filePath = Path.Combine(_workspacePath, filename);

            if (!File.Exists(filePath))
                return null;

            return File.ReadAllText(filePath);
        }

        /// <summary>
        /// Save a custom setting file
        /// </summary>
        /// <param name="filename">Setting filename</param>
        /// <param name="content">File content</param>
        public void SaveCustomSetting(string filename, string content)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename cannot be empty", nameof(filename));

            var filePath = Path.Combine(_workspacePath, filename);
            File.WriteAllText(filePath, content ?? string.Empty);
        }

        /// <summary>
        /// Check if a custom setting file exists
        /// </summary>
        /// <param name="filename">Setting filename</param>
        /// <returns>True if file exists</returns>
        public bool CustomSettingExists(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return false;

            var filePath = Path.Combine(_workspacePath, filename);
            return File.Exists(filePath);
        }

        #endregion
    }
}
