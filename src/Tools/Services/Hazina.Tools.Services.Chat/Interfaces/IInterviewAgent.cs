using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.Chat
{
    /// <summary>
    /// Represents an interview agent that conducts structured conversations
    /// to gather information from users through adaptive questioning.
    /// </summary>
    public interface IInterviewAgent
    {
        /// <summary>
        /// Starts a new interview session for a project.
        /// </summary>
        /// <param name="projectId">The project identifier</param>
        /// <param name="chatId">The chat session identifier</param>
        /// <param name="interviewType">Type of interview (e.g., "onboarding", "brand_discovery", "content_planning")</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>The initial interview state with first question</returns>
        Task<InterviewState> StartInterviewAsync(string projectId, string chatId, string interviewType, CancellationToken cancel = default);

        /// <summary>
        /// Processes a user response and determines the next question.
        /// </summary>
        /// <param name="projectId">The project identifier</param>
        /// <param name="chatId">The chat session identifier</param>
        /// <param name="userResponse">The user's response text</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>Updated interview state with next question or completion status</returns>
        Task<InterviewState> ProcessResponseAsync(string projectId, string chatId, string userResponse, CancellationToken cancel = default);

        /// <summary>
        /// Gets the current state of an interview session.
        /// </summary>
        /// <param name="projectId">The project identifier</param>
        /// <param name="chatId">The chat session identifier</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>The current interview state</returns>
        Task<InterviewState> GetInterviewStateAsync(string projectId, string chatId, CancellationToken cancel = default);

        /// <summary>
        /// Resumes a paused interview session.
        /// </summary>
        /// <param name="projectId">The project identifier</param>
        /// <param name="chatId">The chat session identifier</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>Resumed interview state</returns>
        Task<InterviewState> ResumeInterviewAsync(string projectId, string chatId, CancellationToken cancel = default);

        /// <summary>
        /// Completes an interview and finalizes gathered data.
        /// </summary>
        /// <param name="projectId">The project identifier</param>
        /// <param name="chatId">The chat session identifier</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>Final interview summary</returns>
        Task<InterviewSummary> CompleteInterviewAsync(string projectId, string chatId, CancellationToken cancel = default);

        /// <summary>
        /// Validates if a specific interview type is available.
        /// </summary>
        /// <param name="interviewType">The interview type to check</param>
        /// <returns>True if the interview type is configured and available</returns>
        bool IsInterviewTypeAvailable(string interviewType);

        /// <summary>
        /// Gets all available interview types.
        /// </summary>
        /// <returns>List of available interview type identifiers</returns>
        List<string> GetAvailableInterviewTypes();

        /// <summary>
        /// Skips the current question and moves to the next one.
        /// </summary>
        /// <param name="projectId">The project identifier</param>
        /// <param name="chatId">The chat session identifier</param>
        /// <param name="cancel">Cancellation token</param>
        /// <returns>Updated interview state</returns>
        Task<InterviewState> SkipCurrentQuestionAsync(string projectId, string chatId, CancellationToken cancel = default);
    }

    /// <summary>
    /// Represents the current state of an interview session.
    /// </summary>
    public class InterviewState
    {
        /// <summary>
        /// Unique identifier for this interview session.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// The type of interview being conducted.
        /// </summary>
        public string InterviewType { get; set; }

        /// <summary>
        /// The current question being asked.
        /// </summary>
        public InterviewQuestion CurrentQuestion { get; set; }

        /// <summary>
        /// List of all questions asked so far with their responses.
        /// </summary>
        public List<QuestionResponse> QuestionHistory { get; set; } = new List<QuestionResponse>();

        /// <summary>
        /// Progress indicator (0-100).
        /// </summary>
        public int ProgressPercentage { get; set; }

        /// <summary>
        /// Whether the interview is complete.
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// Whether the interview is currently paused.
        /// </summary>
        public bool IsPaused { get; set; }

        /// <summary>
        /// Data gathered so far during the interview.
        /// </summary>
        public Dictionary<string, object> GatheredData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Suggested next questions based on responses.
        /// </summary>
        public List<string> SuggestedNextQuestions { get; set; } = new List<string>();
    }

    /// <summary>
    /// Represents a question in the interview flow.
    /// </summary>
    public class InterviewQuestion
    {
        /// <summary>
        /// Unique identifier for this question.
        /// </summary>
        public string QuestionId { get; set; }

        /// <summary>
        /// The question text to present to the user.
        /// </summary>
        public string QuestionText { get; set; }

        /// <summary>
        /// Category or topic of this question.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Expected response type (text, multiple_choice, yes_no, numeric, etc.).
        /// </summary>
        public string ResponseType { get; set; }

        /// <summary>
        /// Optional list of predefined response options.
        /// </summary>
        public List<string> ResponseOptions { get; set; }

        /// <summary>
        /// Whether this question is required or optional.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Validation rules for the response.
        /// </summary>
        public string ValidationRules { get; set; }

        /// <summary>
        /// Follow-up questions triggered by specific responses.
        /// </summary>
        public Dictionary<string, List<string>> FollowUpQuestions { get; set; } = new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// Represents a question-response pair.
    /// </summary>
    public class QuestionResponse
    {
        /// <summary>
        /// The question that was asked.
        /// </summary>
        public InterviewQuestion Question { get; set; }

        /// <summary>
        /// The user's response.
        /// </summary>
        public string Response { get; set; }

        /// <summary>
        /// Timestamp of when the response was received.
        /// </summary>
        public System.DateTime RespondedAt { get; set; }

        /// <summary>
        /// Whether this question was skipped.
        /// </summary>
        public bool WasSkipped { get; set; }

        /// <summary>
        /// Confidence score of response quality (0-1).
        /// </summary>
        public double ConfidenceScore { get; set; }
    }

    /// <summary>
    /// Summary of a completed interview.
    /// </summary>
    public class InterviewSummary
    {
        /// <summary>
        /// The interview session identifier.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Type of interview conducted.
        /// </summary>
        public string InterviewType { get; set; }

        /// <summary>
        /// Total number of questions asked.
        /// </summary>
        public int TotalQuestionsAsked { get; set; }

        /// <summary>
        /// Number of questions answered (not skipped).
        /// </summary>
        public int QuestionsAnswered { get; set; }

        /// <summary>
        /// Completion percentage.
        /// </summary>
        public int CompletionPercentage { get; set; }

        /// <summary>
        /// All gathered data from the interview.
        /// </summary>
        public Dictionary<string, object> GatheredData { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Key insights extracted from the interview.
        /// </summary>
        public List<string> KeyInsights { get; set; } = new List<string>();

        /// <summary>
        /// Recommended next actions based on interview results.
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new List<string>();

        /// <summary>
        /// When the interview was completed.
        /// </summary>
        public System.DateTime CompletedAt { get; set; }

        /// <summary>
        /// Duration of the interview.
        /// </summary>
        public System.TimeSpan Duration { get; set; }
    }
}
