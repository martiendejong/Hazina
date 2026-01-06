using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.PromptManagement.Core;
using Hazina.AI.PromptManagement.Reflection;
using Hazina.LLMs.Client;

namespace Hazina.AI.PromptManagement.Rewriter;

/// <summary>
/// Prompt rewriter implementation using LLM-based rewriting and semantic similarity checking
/// </summary>
public class PromptRewriter : IPromptRewriter
{
    private readonly IPromptStore _promptStore;
    private readonly IProposalStore _proposalStore;
    private readonly ILLMClient _llmClient;

    public PromptRewriter(
        IPromptStore promptStore,
        IProposalStore proposalStore,
        ILLMClient llmClient)
    {
        _promptStore = promptStore ?? throw new ArgumentNullException(nameof(promptStore));
        _proposalStore = proposalStore ?? throw new ArgumentNullException(nameof(proposalStore));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    public async Task<PromptProposal> ApplyHypothesisAsync(
        string promptId,
        ImprovementHypothesis hypothesis,
        CancellationToken cancellationToken = default)
    {
        // Get current prompt template
        var promptTemplate = await _promptStore.GetAsync(promptId, cancellationToken: cancellationToken);
        if (promptTemplate == null)
        {
            throw new ArgumentException($"Prompt '{promptId}' not found", nameof(promptId));
        }

        var currentVersion = await _promptStore.GetVersionAsync(
            promptTemplate.CurrentVersion,
            cancellationToken
        );

        if (currentVersion == null)
        {
            throw new InvalidOperationException($"Current version '{promptTemplate.CurrentVersion}' not found");
        }

        // Build rewriting prompt for LLM
        var rewritePrompt = BuildRewritePrompt(currentVersion.Template, hypothesis);

        var request = new LLMRequest
        {
            Messages = new[]
            {
                new LLMMessage
                {
                    Role = "user",
                    Content = rewritePrompt
                }
            },
            Temperature = 0.4,  // Slightly higher for creativity, but still focused
            MaxTokens = 3000
        };

        var response = await _llmClient.CompleteAsync(request, cancellationToken);
        var llmOutput = response.Choices[0].Message.Content;

        // Parse the rewritten template and changes
        var (proposedTemplate, changes) = ParseRewriteOutput(llmOutput, currentVersion.Template);

        // Calculate semantic similarity
        var similarity = await CalculateSimilarityAsync(
            currentVersion.Template,
            proposedTemplate,
            cancellationToken
        );

        // Generate version ID (SHA-256 hash)
        var proposedVersionId = GenerateVersionId(proposedTemplate);

        // Create proposal
        var proposal = new PromptProposal
        {
            PromptId = promptId,
            CurrentVersion = currentVersion.VersionId,
            ProposedTemplate = proposedTemplate,
            ProposedVersionId = proposedVersionId,
            Changes = changes,
            Rationale = hypothesis.Rationale,
            ExpectedImprovement = hypothesis.ExpectedImpact,
            HypothesisIds = new List<string> { hypothesis.HypothesisId },
            SemanticSimilarity = similarity,
            Metadata = new Dictionary<string, object>
            {
                { "hypothesis_priority", hypothesis.Priority },
                { "hypothesis_confidence", hypothesis.Confidence },
                { "target_metrics", hypothesis.TargetMetrics },
                { "addressed_patterns", hypothesis.AddressedPatterns }
            }
        };

        // Save proposal
        await _proposalStore.SaveProposalAsync(proposal, cancellationToken);

        return proposal;
    }

    public async Task<PromptProposal> ApplyMultipleHypothesesAsync(
        string promptId,
        List<ImprovementHypothesis> hypotheses,
        CancellationToken cancellationToken = default)
    {
        if (!hypotheses.Any())
        {
            throw new ArgumentException("At least one hypothesis required", nameof(hypotheses));
        }

        // Get current prompt template
        var promptTemplate = await _promptStore.GetAsync(promptId, cancellationToken: cancellationToken);
        if (promptTemplate == null)
        {
            throw new ArgumentException($"Prompt '{promptId}' not found", nameof(promptId));
        }

        var currentVersion = await _promptStore.GetVersionAsync(
            promptTemplate.CurrentVersion,
            cancellationToken
        );

        if (currentVersion == null)
        {
            throw new InvalidOperationException($"Current version '{promptTemplate.CurrentVersion}' not found");
        }

        // Build combined rewriting prompt
        var rewritePrompt = BuildCombinedRewritePrompt(currentVersion.Template, hypotheses);

        var request = new LLMRequest
        {
            Messages = new[]
            {
                new LLMMessage
                {
                    Role = "user",
                    Content = rewritePrompt
                }
            },
            Temperature = 0.4,
            MaxTokens = 4000
        };

        var response = await _llmClient.CompleteAsync(request, cancellationToken);
        var llmOutput = response.Choices[0].Message.Content;

        var (proposedTemplate, changes) = ParseRewriteOutput(llmOutput, currentVersion.Template);

        var similarity = await CalculateSimilarityAsync(
            currentVersion.Template,
            proposedTemplate,
            cancellationToken
        );

        var proposedVersionId = GenerateVersionId(proposedTemplate);

        // Combined rationale
        var combinedRationale = $"Applying {hypotheses.Count} improvements: " +
            string.Join("; ", hypotheses.Select(h => h.Hypothesis));

        var avgExpectedImprovement = hypotheses.Average(h => h.ExpectedImpact);

        var proposal = new PromptProposal
        {
            PromptId = promptId,
            CurrentVersion = currentVersion.VersionId,
            ProposedTemplate = proposedTemplate,
            ProposedVersionId = proposedVersionId,
            Changes = changes,
            Rationale = combinedRationale,
            ExpectedImprovement = avgExpectedImprovement,
            HypothesisIds = hypotheses.Select(h => h.HypothesisId).ToList(),
            SemanticSimilarity = similarity,
            Metadata = new Dictionary<string, object>
            {
                { "hypothesis_count", hypotheses.Count },
                { "combined_confidence", hypotheses.Average(h => h.Confidence) }
            }
        };

        await _proposalStore.SaveProposalAsync(proposal, cancellationToken);

        return proposal;
    }

    public async Task<List<PromptProposal>> GenerateVariantsAsync(
        string promptId,
        ImprovementHypothesis hypothesis,
        int variantCount = 2,
        CancellationToken cancellationToken = default)
    {
        if (variantCount < 1 || variantCount > 5)
        {
            throw new ArgumentException("Variant count must be between 1 and 5", nameof(variantCount));
        }

        var variants = new List<PromptProposal>();

        // Get current prompt
        var promptTemplate = await _promptStore.GetAsync(promptId, cancellationToken: cancellationToken);
        if (promptTemplate == null)
        {
            throw new ArgumentException($"Prompt '{promptId}' not found", nameof(promptId));
        }

        var currentVersion = await _promptStore.GetVersionAsync(
            promptTemplate.CurrentVersion,
            cancellationToken
        );

        if (currentVersion == null)
        {
            throw new InvalidOperationException($"Current version '{promptTemplate.CurrentVersion}' not found");
        }

        // Generate each variant
        for (int i = 0; i < variantCount; i++)
        {
            var variantPrompt = BuildVariantPrompt(currentVersion.Template, hypothesis, i + 1);

            var request = new LLMRequest
            {
                Messages = new[]
                {
                    new LLMMessage
                    {
                        Role = "user",
                        Content = variantPrompt
                    }
                },
                Temperature = 0.6 + (i * 0.1),  // Increase temperature for diversity
                MaxTokens = 3000
            };

            var response = await _llmClient.CompleteAsync(request, cancellationToken);
            var llmOutput = response.Choices[0].Message.Content;

            var (proposedTemplate, changes) = ParseRewriteOutput(llmOutput, currentVersion.Template);

            var similarity = await CalculateSimilarityAsync(
                currentVersion.Template,
                proposedTemplate,
                cancellationToken
            );

            var proposedVersionId = GenerateVersionId(proposedTemplate);

            var variant = new PromptProposal
            {
                PromptId = promptId,
                CurrentVersion = currentVersion.VersionId,
                ProposedTemplate = proposedTemplate,
                ProposedVersionId = proposedVersionId,
                Changes = changes,
                Rationale = $"{hypothesis.Rationale} (Variant {i + 1})",
                ExpectedImprovement = hypothesis.ExpectedImpact,
                HypothesisIds = new List<string> { hypothesis.HypothesisId },
                SemanticSimilarity = similarity,
                Metadata = new Dictionary<string, object>
                {
                    { "variant_number", i + 1 },
                    { "variant_count", variantCount },
                    { "temperature", 0.6 + (i * 0.1) }
                }
            };

            await _proposalStore.SaveProposalAsync(variant, cancellationToken);
            variants.Add(variant);
        }

        return variants;
    }

    public async Task<double> CalculateSimilarityAsync(
        string originalTemplate,
        string proposedTemplate,
        CancellationToken cancellationToken = default)
    {
        // Use LLM embeddings to calculate semantic similarity
        try
        {
            var originalEmbedding = await GetEmbeddingAsync(originalTemplate, cancellationToken);
            var proposedEmbedding = await GetEmbeddingAsync(proposedTemplate, cancellationToken);

            // Calculate cosine similarity
            return CosineSimilarity(originalEmbedding, proposedEmbedding);
        }
        catch
        {
            // Fallback to simple Jaccard similarity if embeddings fail
            return JaccardSimilarity(originalTemplate, proposedTemplate);
        }
    }

    public async Task<bool> ValidateSemanticSimilarityAsync(
        PromptProposal proposal,
        double minSimilarity = 0.85,
        CancellationToken cancellationToken = default)
    {
        if (proposal.SemanticSimilarity >= minSimilarity)
        {
            return true;
        }

        // Similarity already calculated in proposal
        return false;
    }

    // Private helper methods

    private string BuildRewritePrompt(string currentTemplate, ImprovementHypothesis hypothesis)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an expert prompt engineer. Your task is to improve the following prompt template based on the provided hypothesis.");
        sb.AppendLine();
        sb.AppendLine("## Current Prompt Template");
        sb.AppendLine("```");
        sb.AppendLine(currentTemplate);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Improvement Hypothesis");
        sb.AppendLine($"**What to change**: {hypothesis.Hypothesis}");
        sb.AppendLine($"**Why**: {hypothesis.Rationale}");
        sb.AppendLine($"**Expected Impact**: {hypothesis.ExpectedImpact:P}");
        sb.AppendLine($"**Target Metrics**: {string.Join(", ", hypothesis.TargetMetrics)}");
        sb.AppendLine();

        if (hypothesis.SuggestedChanges.Any())
        {
            sb.AppendLine("## Suggested Changes");
            foreach (var change in hypothesis.SuggestedChanges)
            {
                sb.AppendLine($"- {change.ChangeType} in {change.Section}: {change.Reason}");
                if (!string.IsNullOrEmpty(change.ProposedValue))
                {
                    sb.AppendLine($"  Proposed: \"{change.ProposedValue}\"");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Task");
        sb.AppendLine("Rewrite the prompt template to incorporate these improvements. Return your response in this format:");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"improved_template\": \"The complete improved prompt template here\",");
        sb.AppendLine("  \"changes\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"type\": \"Add|Remove|Modify|Restructure\",");
        sb.AppendLine("      \"section\": \"instructions|examples|constraints|format\",");
        sb.AppendLine("      \"oldValue\": \"text before change or empty\",");
        sb.AppendLine("      \"newValue\": \"text after change\",");
        sb.AppendLine("      \"reason\": \"why this change was made\"");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Important:");
        sb.AppendLine("- Maintain the overall purpose and style of the prompt");
        sb.AppendLine("- Make targeted improvements based on the hypothesis");
        sb.AppendLine("- Preserve any template variables (e.g., {{variable}})");
        sb.AppendLine("- Keep the improved template semantically similar to the original");

        return sb.ToString();
    }

    private string BuildCombinedRewritePrompt(string currentTemplate, List<ImprovementHypothesis> hypotheses)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an expert prompt engineer. Apply multiple improvement hypotheses to the following prompt template.");
        sb.AppendLine();
        sb.AppendLine("## Current Prompt Template");
        sb.AppendLine("```");
        sb.AppendLine(currentTemplate);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Improvement Hypotheses to Apply");

        for (int i = 0; i < hypotheses.Count; i++)
        {
            var h = hypotheses[i];
            sb.AppendLine($"### Hypothesis {i + 1}: {h.Hypothesis}");
            sb.AppendLine($"- **Rationale**: {h.Rationale}");
            sb.AppendLine($"- **Expected Impact**: {h.ExpectedImpact:P}");
            sb.AppendLine($"- **Priority**: {h.Priority}");
            sb.AppendLine();
        }

        sb.AppendLine("## Task");
        sb.AppendLine("Apply ALL of these improvements to create a single improved prompt template.");
        sb.AppendLine("Return the result in JSON format (same structure as single hypothesis).");

        return sb.ToString();
    }

    private string BuildVariantPrompt(string currentTemplate, ImprovementHypothesis hypothesis, int variantNumber)
    {
        var basePrompt = BuildRewritePrompt(currentTemplate, hypothesis);

        return basePrompt + $"\n\nNote: This is variant #{variantNumber}. Generate a different approach than previous variants while still addressing the hypothesis.";
    }

    private (string proposedTemplate, List<PromptChange> changes) ParseRewriteOutput(string llmOutput, string originalTemplate)
    {
        try
        {
            // Extract JSON from output
            var jsonStart = llmOutput.IndexOf('{');
            var jsonEnd = llmOutput.LastIndexOf('}');

            if (jsonStart == -1 || jsonEnd == -1)
            {
                // Fallback: treat entire output as template
                return (llmOutput, new List<PromptChange>());
            }

            var jsonContent = llmOutput.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);

            if (parsed == null)
            {
                return (llmOutput, new List<PromptChange>());
            }

            var proposedTemplate = parsed.ContainsKey("improved_template")
                ? parsed["improved_template"].GetString() ?? llmOutput
                : llmOutput;

            var changes = new List<PromptChange>();

            if (parsed.ContainsKey("changes"))
            {
                try
                {
                    var changesArray = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(
                        parsed["changes"].GetRawText()
                    );

                    if (changesArray != null)
                    {
                        changes = changesArray.Select(c => new PromptChange
                        {
                            Type = c.GetValueOrDefault("type", "Modify"),
                            Section = c.GetValueOrDefault("section", "general"),
                            OldValue = c.GetValueOrDefault("oldValue", ""),
                            NewValue = c.GetValueOrDefault("newValue", ""),
                            Reason = c.GetValueOrDefault("reason", ""),
                            LineStart = 0,  // TODO: Calculate from diff
                            LineEnd = 0
                        }).ToList();
                    }
                }
                catch
                {
                    // Ignore change parsing errors
                }
            }

            // If no changes were parsed, create a generic change
            if (!changes.Any())
            {
                changes.Add(new PromptChange
                {
                    Type = "Modify",
                    Section = "general",
                    OldValue = originalTemplate,
                    NewValue = proposedTemplate,
                    Reason = "LLM-generated improvement"
                });
            }

            return (proposedTemplate, changes);
        }
        catch (JsonException)
        {
            // JSON parsing failed - return raw output
            return (llmOutput, new List<PromptChange>
            {
                new PromptChange
                {
                    Type = "Modify",
                    Section = "general",
                    OldValue = originalTemplate,
                    NewValue = llmOutput,
                    Reason = "LLM-generated improvement"
                }
            });
        }
    }

    private async Task<double[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        // Use LLM client's embedding capability if available
        // For now, this is a placeholder that would call the actual embedding API
        // In production, this would use OpenAI embeddings or similar

        var request = new LLMRequest
        {
            Messages = new[]
            {
                new LLMMessage
                {
                    Role = "user",
                    Content = text
                }
            }
        };

        // This is a simplified placeholder
        // Real implementation would use dedicated embedding models
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var embedding = new double[1536];  // Standard embedding dimension

        for (int i = 0; i < 1536; i++)
        {
            embedding[i] = hash[i % hash.Length] / 255.0;
        }

        return await Task.FromResult(embedding);
    }

    private double CosineSimilarity(double[] a, double[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length");
        }

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private double JaccardSimilarity(string text1, string text2)
    {
        var words1 = text1.ToLower().Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = text2.ToLower().Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0;
    }

    private string GenerateVersionId(string template)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(template + DateTime.UtcNow.Ticks));
        return Convert.ToHexString(hash).ToLower();
    }
}
