using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Hazina.Tools.Services.Chat
{
    /// <summary>
    /// Default implementation of the interview agent that conducts structured
    /// conversations using configuration-driven question banks.
    /// </summary>
    public class InterviewAgent : IInterviewAgent
    {
        private readonly ProjectsRepository _projects;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, InterviewConfiguration> _interviewConfigs;
        private readonly Dictionary<string, InterviewState> _activeInterviews = new();

        public InterviewAgent(ProjectsRepository projects, IConfiguration configuration)
        {
            _projects = projects ?? throw new ArgumentNullException(nameof(projects));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _interviewConfigs = LoadInterviewConfigurations();
        }

        public async Task<InterviewState> StartInterviewAsync(string projectId, string chatId, string interviewType, CancellationToken cancel = default)
        {
            if (!_interviewConfigs.ContainsKey(interviewType))
            {
                throw new ArgumentException($"Interview type '{interviewType}' is not configured.", nameof(interviewType));
            }

            var config = _interviewConfigs[interviewType];
            var sessionId = $"{projectId}:{chatId}";

            var state = new InterviewState
            {
                SessionId = sessionId,
                InterviewType = interviewType,
                CurrentQuestion = GetFirstQuestion(config),
                ProgressPercentage = 0,
                IsComplete = false,
                IsPaused = false
            };

            _activeInterviews[sessionId] = state;

            // Save state to project metadata
            await SaveInterviewStateAsync(projectId, chatId, state, cancel);

            return state;
        }

        public async Task<InterviewState> ProcessResponseAsync(string projectId, string chatId, string userResponse, CancellationToken cancel = default)
        {
            var sessionId = $"{projectId}:{chatId}";
            var state = await GetInterviewStateAsync(projectId, chatId, cancel);

            if (state.IsComplete)
            {
                throw new InvalidOperationException("Interview is already complete.");
            }

            // Record the response
            var questionResponse = new QuestionResponse
            {
                Question = state.CurrentQuestion,
                Response = userResponse,
                RespondedAt = DateTime.UtcNow,
                WasSkipped = false,
                ConfidenceScore = AnalyzeResponseQuality(userResponse, state.CurrentQuestion)
            };

            state.QuestionHistory.Add(questionResponse);

            // Extract and store gathered data
            ExtractDataFromResponse(state, questionResponse);

            // Determine next question using adaptive logic
            var nextQuestion = await DetermineNextQuestionAsync(state, userResponse, cancel);

            if (nextQuestion == null)
            {
                // Interview is complete
                state.IsComplete = true;
                state.ProgressPercentage = 100;
                state.CurrentQuestion = null;
            }
            else
            {
                state.CurrentQuestion = nextQuestion;
                state.ProgressPercentage = CalculateProgress(state);
            }

            // Save updated state
            await SaveInterviewStateAsync(projectId, chatId, state, cancel);

            return state;
        }

        public async Task<InterviewState> GetInterviewStateAsync(string projectId, string chatId, CancellationToken cancel = default)
        {
            var sessionId = $"{projectId}:{chatId}";

            // Check in-memory cache first
            if (_activeInterviews.TryGetValue(sessionId, out var cachedState))
            {
                return cachedState;
            }

            // Load from project metadata
            var state = await LoadInterviewStateAsync(projectId, chatId, cancel);

            if (state != null)
            {
                _activeInterviews[sessionId] = state;
                return state;
            }

            // No active interview
            return new InterviewState
            {
                SessionId = sessionId,
                IsComplete = false,
                IsPaused = true,
                ProgressPercentage = 0
            };
        }

        public async Task<InterviewState> ResumeInterviewAsync(string projectId, string chatId, CancellationToken cancel = default)
        {
            var state = await GetInterviewStateAsync(projectId, chatId, cancel);

            if (!state.IsPaused)
            {
                throw new InvalidOperationException("Interview is not paused.");
            }

            state.IsPaused = false;
            await SaveInterviewStateAsync(projectId, chatId, state, cancel);

            return state;
        }

        public async Task<InterviewSummary> CompleteInterviewAsync(string projectId, string chatId, CancellationToken cancel = default)
        {
            var state = await GetInterviewStateAsync(projectId, chatId, cancel);
            var sessionId = $"{projectId}:{chatId}";

            var summary = new InterviewSummary
            {
                SessionId = state.SessionId,
                InterviewType = state.InterviewType,
                TotalQuestionsAsked = state.QuestionHistory.Count,
                QuestionsAnswered = state.QuestionHistory.Count(qr => !qr.WasSkipped),
                CompletionPercentage = state.ProgressPercentage,
                GatheredData = state.GatheredData,
                CompletedAt = DateTime.UtcNow,
                KeyInsights = ExtractKeyInsights(state),
                RecommendedActions = GenerateRecommendations(state)
            };

            // Calculate duration
            if (state.QuestionHistory.Any())
            {
                var firstQuestion = state.QuestionHistory.First().RespondedAt;
                var lastQuestion = state.QuestionHistory.Last().RespondedAt;
                summary.Duration = lastQuestion - firstQuestion;
            }

            // Mark as complete
            state.IsComplete = true;
            await SaveInterviewStateAsync(projectId, chatId, state, cancel);

            // Remove from active interviews
            _activeInterviews.Remove(sessionId);

            return summary;
        }

        public bool IsInterviewTypeAvailable(string interviewType)
        {
            return _interviewConfigs.ContainsKey(interviewType);
        }

        public List<string> GetAvailableInterviewTypes()
        {
            return _interviewConfigs.Keys.ToList();
        }

        public async Task<InterviewState> SkipCurrentQuestionAsync(string projectId, string chatId, CancellationToken cancel = default)
        {
            var state = await GetInterviewStateAsync(projectId, chatId, cancel);

            if (state.CurrentQuestion == null || state.IsComplete)
            {
                return state;
            }

            // Record as skipped
            var skippedResponse = new QuestionResponse
            {
                Question = state.CurrentQuestion,
                Response = "[SKIPPED]",
                RespondedAt = DateTime.UtcNow,
                WasSkipped = true,
                ConfidenceScore = 0
            };

            state.QuestionHistory.Add(skippedResponse);

            // Move to next question
            var config = _interviewConfigs[state.InterviewType];
            var nextQuestion = GetNextQuestion(config, state);

            if (nextQuestion == null)
            {
                state.IsComplete = true;
                state.ProgressPercentage = 100;
                state.CurrentQuestion = null;
            }
            else
            {
                state.CurrentQuestion = nextQuestion;
                state.ProgressPercentage = CalculateProgress(state);
            }

            await SaveInterviewStateAsync(projectId, chatId, state, cancel);

            return state;
        }

        #region Private Helper Methods

        private Dictionary<string, InterviewConfiguration> LoadInterviewConfigurations()
        {
            var configs = new Dictionary<string, InterviewConfiguration>();

            // Load from configuration
            var interviewSection = _configuration.GetSection("InterviewAgent:Interviews");

            if (interviewSection.Exists())
            {
                foreach (var child in interviewSection.GetChildren())
                {
                    var config = child.Get<InterviewConfiguration>();
                    if (config != null && !string.IsNullOrEmpty(config.InterviewType))
                    {
                        configs[config.InterviewType] = config;
                    }
                }
            }

            // If no configurations loaded, add default onboarding interview
            if (!configs.Any())
            {
                configs["onboarding"] = CreateDefaultOnboardingInterview();
            }

            return configs;
        }

        private InterviewConfiguration CreateDefaultOnboardingInterview()
        {
            return new InterviewConfiguration
            {
                InterviewType = "onboarding",
                Name = "Brand Onboarding Interview",
                Description = "Initial interview to gather brand information",
                Questions = new List<InterviewQuestion>
                {
                    new InterviewQuestion
                    {
                        QuestionId = "brand_name",
                        QuestionText = "What is your brand or business name?",
                        Category = "basic_info",
                        ResponseType = "text",
                        IsRequired = true
                    },
                    new InterviewQuestion
                    {
                        QuestionId = "industry",
                        QuestionText = "What industry or sector does your business operate in?",
                        Category = "basic_info",
                        ResponseType = "text",
                        IsRequired = true
                    },
                    new InterviewQuestion
                    {
                        QuestionId = "target_audience",
                        QuestionText = "Who is your target audience or ideal customer?",
                        Category = "marketing",
                        ResponseType = "text",
                        IsRequired = true
                    },
                    new InterviewQuestion
                    {
                        QuestionId = "brand_personality",
                        QuestionText = "How would you describe your brand's personality? (e.g., professional, playful, innovative)",
                        Category = "branding",
                        ResponseType = "text",
                        IsRequired = false
                    }
                }
            };
        }

        private InterviewQuestion GetFirstQuestion(InterviewConfiguration config)
        {
            return config.Questions.FirstOrDefault();
        }

        private InterviewQuestion GetNextQuestion(InterviewConfiguration config, InterviewState state)
        {
            var answeredQuestionIds = state.QuestionHistory.Select(qr => qr.Question.QuestionId).ToHashSet();

            // Find the first unanswered question
            return config.Questions.FirstOrDefault(q => !answeredQuestionIds.Contains(q.QuestionId));
        }

        private async Task<InterviewQuestion> DetermineNextQuestionAsync(InterviewState state, string userResponse, CancellationToken cancel)
        {
            var config = _interviewConfigs[state.InterviewType];

            // Check for follow-up questions based on current response
            if (state.CurrentQuestion?.FollowUpQuestions != null && state.CurrentQuestion.FollowUpQuestions.Any())
            {
                foreach (var followUpTrigger in state.CurrentQuestion.FollowUpQuestions)
                {
                    if (userResponse.Contains(followUpTrigger.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        // Find follow-up question by ID
                        var followUpQuestionId = followUpTrigger.Value.FirstOrDefault();
                        if (!string.IsNullOrEmpty(followUpQuestionId))
                        {
                            var followUp = config.Questions.FirstOrDefault(q => q.QuestionId == followUpQuestionId);
                            if (followUp != null)
                            {
                                return followUp;
                            }
                        }
                    }
                }
            }

            // Otherwise, get next sequential question
            return GetNextQuestion(config, state);
        }

        private void ExtractDataFromResponse(InterviewState state, QuestionResponse questionResponse)
        {
            if (!questionResponse.WasSkipped && !string.IsNullOrWhiteSpace(questionResponse.Response))
            {
                var key = questionResponse.Question.QuestionId;
                state.GatheredData[key] = questionResponse.Response;

                // Also add to category-based grouping
                var category = questionResponse.Question.Category;
                if (!string.IsNullOrEmpty(category))
                {
                    var categoryKey = $"category_{category}";
                    if (!state.GatheredData.ContainsKey(categoryKey))
                    {
                        state.GatheredData[categoryKey] = new List<string>();
                    }

                    if (state.GatheredData[categoryKey] is List<string> categoryList)
                    {
                        categoryList.Add(questionResponse.Response);
                    }
                }
            }
        }

        private double AnalyzeResponseQuality(string response, InterviewQuestion question)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return 0.0;
            }

            // Simple heuristic: longer responses are generally better quality
            var wordCount = response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            if (wordCount < 3)
            {
                return 0.3;
            }
            else if (wordCount < 10)
            {
                return 0.6;
            }
            else
            {
                return 0.9;
            }
        }

        private int CalculateProgress(InterviewState state)
        {
            if (!_interviewConfigs.TryGetValue(state.InterviewType, out var config))
            {
                return 0;
            }

            var totalQuestions = config.Questions.Count;
            if (totalQuestions == 0)
            {
                return 100;
            }

            var answeredQuestions = state.QuestionHistory.Count;
            return Math.Min(100, (answeredQuestions * 100) / totalQuestions);
        }

        private List<string> ExtractKeyInsights(InterviewState state)
        {
            var insights = new List<string>();

            // Analyze gathered data for key patterns
            if (state.GatheredData.ContainsKey("brand_name"))
            {
                insights.Add($"Brand identity: {state.GatheredData["brand_name"]}");
            }

            if (state.GatheredData.ContainsKey("target_audience"))
            {
                insights.Add($"Target audience identified: {state.GatheredData["target_audience"]}");
            }

            // Count response quality
            var highQualityResponses = state.QuestionHistory.Count(qr => qr.ConfidenceScore > 0.7);
            if (highQualityResponses > 0)
            {
                insights.Add($"{highQualityResponses} detailed responses provided");
            }

            return insights;
        }

        private List<string> GenerateRecommendations(InterviewState state)
        {
            var recommendations = new List<string>();

            // Based on completion percentage
            if (state.ProgressPercentage < 100)
            {
                var skippedQuestions = state.QuestionHistory.Count(qr => qr.WasSkipped);
                if (skippedQuestions > 0)
                {
                    recommendations.Add($"Consider answering {skippedQuestions} skipped question(s) for better results");
                }
            }

            // Based on gathered data
            if (state.GatheredData.ContainsKey("brand_personality"))
            {
                recommendations.Add("Use the brand personality in content generation");
            }

            if (state.GatheredData.ContainsKey("target_audience"))
            {
                recommendations.Add("Tailor messaging to your identified target audience");
            }

            return recommendations;
        }

        private async Task SaveInterviewStateAsync(string projectId, string chatId, InterviewState state, CancellationToken cancel)
        {
            // Serialize state to JSON
            var stateJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });

            // Save to project metadata file
            var project = _projects.Load(projectId);
            if (project == null)
            {
                return;
            }

            var metadataFolder = Path.Combine(_projects.ProjectsFolder, projectId, "interview_states");
            Directory.CreateDirectory(metadataFolder);

            var stateFile = Path.Combine(metadataFolder, $"{chatId}.json");
            await System.IO.File.WriteAllTextAsync(stateFile, stateJson, cancel);
        }

        private async Task<InterviewState> LoadInterviewStateAsync(string projectId, string chatId, CancellationToken cancel)
        {
            var metadataFolder = Path.Combine(_projects.ProjectsFolder, projectId, "interview_states");
            var stateFile = Path.Combine(metadataFolder, $"{chatId}.json");

            if (!File.Exists(stateFile))
            {
                return null;
            }

            try
            {
                var stateJson = await System.IO.File.ReadAllTextAsync(stateFile, cancel);
                return JsonSerializer.Deserialize<InterviewState>(stateJson);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Configuration for an interview type.
    /// </summary>
    public class InterviewConfiguration
    {
        public string InterviewType { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<InterviewQuestion> Questions { get; set; } = new List<InterviewQuestion>();
    }
}
