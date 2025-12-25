using Hazina.Tools.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Hazina.Tools.Services.Store
{
    /// <summary>
    /// Centralized loader for analysis field configuration and prompt metadata.
    /// Used by both the API controller and the data gathering services.
    /// </summary>
    public static class AnalysisFieldConfigLoader
    {
        public const string ConfigFileName = "analysis-fields.config.json";

        /// <summary>
        /// Load analysis field definitions from config or fall back to defaults.
        /// Optionally writes a default config file when missing.
        /// </summary>
        public static IReadOnlyList<AnalysisFieldInfo> LoadFields(string projectsFolder, bool createDefaultConfigFile = false)
        {
            var result = new List<AnalysisFieldInfo>();
            var cfgPath = Path.Combine(projectsFolder, ConfigFileName);
            var configFileExists = File.Exists(cfgPath);

            if (configFileExists)
            {
                try
                {
                     var json = File.ReadAllText(cfgPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("analysisFields", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            if (!el.TryGetProperty("key", out var k) || !el.TryGetProperty("fileName", out var f))
                                continue;

                            var key = k.GetString();
                            var file = f.GetString();
                            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(file))
                                continue;

                            var info = new AnalysisFieldInfo
                            {
                                Key = key,
                                File = file,
                                DisplayName = el.TryGetProperty("displayName", out var d) ? d.GetString() ?? key : key,
                                GenericType = el.TryGetProperty("genericType", out var gt) ? gt.GetString() : null,
                                ConfigFileName = el.TryGetProperty("configFileName", out var cf) ? cf.GetString() : null,
                                ComponentName = el.TryGetProperty("componentName", out var cn) ? cn.GetString() : null,
                                RowComponentName = el.TryGetProperty("rowComponentName", out var rcn) ? rcn.GetString() : null
                            };

                            if (!string.IsNullOrWhiteSpace(info.GenericType))
                            {
                                info.TypeSignature = ResolveTypeSignature(info.GenericType);
                            }

                            result.Add(info);
                        }
                    }
                }
                catch
                {
                    // Ignore and fall back to defaults
                }
            }

            if (result.Count == 0)
            {
                result = GetDefaultFields();

                if (createDefaultConfigFile && !configFileExists)
                {
                    SaveDefaultConfigFile(cfgPath, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Resolve the prompt file name (configFileName or default pattern) for a field.
        /// </summary>
        public static string GetPromptFileName(AnalysisFieldInfo field) =>
            string.IsNullOrWhiteSpace(field?.ConfigFileName)
                ? $"{field?.Key?.Replace("-", ".")}.prompt.txt"
                : field.ConfigFileName;

        /// <summary>
        /// Load a prompt for a field, accepting either { "prompt": "" } or { "systemPrompt": "" } shapes, or plain text.
        /// </summary>
        public static string LoadPrompt(string projectsFolder, AnalysisFieldInfo field)
        {
            var promptFileName = GetPromptFileName(field);
            if (string.IsNullOrWhiteSpace(promptFileName))
                return string.Empty;

            var promptPath = Path.Combine(projectsFolder, promptFileName);
            if (!File.Exists(promptPath))
                return string.Empty;

            try
            {
                var content = File.ReadAllText(promptPath);
                return content;
            }
            catch
            {
                // Ignore parsing errors and fall back to empty prompt
            }

            return string.Empty;
        }

        private static List<AnalysisFieldInfo> GetDefaultFields()
        {
            return new List<AnalysisFieldInfo>
            {
                new() { Key = "topic-synopsis", File = IntakeRepository.TopicSynopsisFile, DisplayName = "Topic Synopsis / Historical Context", ConfigFileName = "topic.synopsis.prompts.txt" },
                new() { Key = "narrative-stance", File = IntakeRepository.NarrativeStanceFile, DisplayName = "Narrative Stance / Interpretive Lens", ConfigFileName = "narrative.stance.prompts.txt" },
                new() { Key = "target-group", File = IntakeRepository.TargetGroupFile, DisplayName = "Intellectual and Cultural Target Group", ConfigFileName = "target.group.prompts.txt" },
                new() { Key = "philosophical-commitments", File = IntakeRepository.PhilosophicalCommitmentsFile, DisplayName = "Philosophical Commitments / Methodological Principles", ConfigFileName = "philosophical.commitments.prompts.txt" },
                new() { Key = "revisionist-claims", File = IntakeRepository.RevisionistClaimsFile, DisplayName = "Revisionist Claims / Evidential Strengths", ConfigFileName = "revisionist.claims.prompts.txt" },
                new() { Key = "central-thesis", File = IntakeRepository.CentralThesisFile, DisplayName = "Central Thesis", ConfigFileName = "central.thesis.prompts.txt" },
                new() { Key = "evidence-base", File = IntakeRepository.EvidenceBaseFile, DisplayName = "Evidence Base", ConfigFileName = "evidence.base.prompts.txt" },
                new() { Key = "counter-narrative-structure", File = IntakeRepository.CounterNarrativeStructureFile, DisplayName = "Counter-Narrative Structure", ConfigFileName = "counter.narrative.structure.prompts.txt" },
                new() { Key = "aesthetic-direction", File = IntakeRepository.AestheticDirectionFile, DisplayName = "Aesthetic Direction", ConfigFileName = "aesthetic.direction.prompts.txt" },
                new() { Key = "proof-strategy", File = IntakeRepository.ProofStrategyFile, DisplayName = "Proof Strategy", ConfigFileName = "proof.strategy.prompts.txt" },
                new() { Key = "intended-impact", File = IntakeRepository.IntendedImpactFile, DisplayName = "Intended Impact", ConfigFileName = "intended.impact.prompts.txt" }
            };
        }

        private static void SaveDefaultConfigFile(string cfgPath, List<AnalysisFieldInfo> fields)
        {
            try
            {
                var config = new
                {
                    analysisFields = fields.Select(f => new
                    {
                        key = f.Key,
                        fileName = f.File,
                        displayName = f.DisplayName,
                        genericType = f.GenericType,
                        configFileName = f.ConfigFileName,
                        componentName = f.ComponentName,
                        rowComponentName = f.RowComponentName
                    }).ToArray()
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(cfgPath, json);
            }
            catch
            {
                // Best effort - don't fail callers if we can't write the config file
            }
        }

        /// <summary>
        /// Resolves the type signature for a GenericType by instantiating it and reading _signature.
        /// </summary>
        public static string ResolveTypeSignature(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            try
            {
                var t = ResolveType(typeName);
                if (t == null) return null;

                var instance = System.Activator.CreateInstance(t);
                var signatureProp = t.GetProperty("_signature");
                if (signatureProp != null)
                {
                    return signatureProp.GetValue(instance) as string;
                }
            }
            catch
            {
                // Ignore failures and fall back to null
            }

            return null;
        }

        /// <summary>
        /// Resolves a type by name, searching all loaded assemblies.
        /// </summary>
        public static System.Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            var t = System.Type.GetType(typeName, throwOnError: false, ignoreCase: true);
            if (t != null) return t;

            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(typeName, throwOnError: false, ignoreCase: true);
                    if (t != null) return t;

                    var types = asm.GetTypes();
                    var match = types.FirstOrDefault(x =>
                        string.Equals(x.FullName, typeName, System.StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.Name, typeName, System.StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match;
                }
                catch
                {
                    // Ignore assembly load issues
                }
            }

            return null;
        }
    }
}
