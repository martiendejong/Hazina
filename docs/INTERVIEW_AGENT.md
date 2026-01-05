# Interview Agent - Structured Conversation Framework

## Overview

The Interview Agent is a formalized component in the Hazina framework that conducts structured conversations to gather information from users through adaptive questioning. It implements explicit question strategies, progress tracking, and configuration-driven question banks.

## Architecture

### Components

1. **IInterviewAgent Interface** (`Hazina.Tools.Services.Chat`)
   - Defines the contract for interview agent implementations
   - Manages interview lifecycle: start, process responses, resume, complete
   - Provides interview state management and progress tracking

2. **InterviewAgent Service** (`Hazina.Tools.Services.Chat.Services`)
   - Default implementation of IInterviewAgent
   - Configuration-driven question banks
   - Adaptive questioning logic based on responses
   - Persistent state storage in project folders

3. **Data Models**
   - `InterviewState`: Current state of an interview session
   - `InterviewQuestion`: Individual question with metadata
   - `QuestionResponse`: Question-response pair with confidence scoring
   - `InterviewSummary`: Final summary of completed interview
   - `InterviewConfiguration`: Question bank configuration

## Features

### 1. Configuration-Driven Question Banks

Define interview types in `appsettings.json` or configuration files:

```json
{
  "InterviewAgent": {
    "Interviews": [
      {
        "InterviewType": "onboarding",
        "Name": "Brand Onboarding Interview",
        "Description": "Initial interview to gather brand information",
        "Questions": [
          {
            "QuestionId": "brand_name",
            "QuestionText": "What is your brand or business name?",
            "Category": "basic_info",
            "ResponseType": "text",
            "IsRequired": true
          }
        ]
      }
    ]
  }
}
```

See `interview-agent-config-example.json` for complete examples.

### 2. Adaptive Questioning

The agent adapts the question flow based on user responses:

- **Follow-up Questions**: Triggered by specific keywords in responses
- **Sequential Flow**: Default progression through question bank
- **Skip Logic**: Optional questions can be skipped
- **Dynamic Progress**: Real-time progress calculation

Example follow-up configuration:

```json
{
  "QuestionId": "target_audience",
  "QuestionText": "Who is your target audience?",
  "FollowUpQuestions": {
    "B2B": ["decision_makers"],
    "businesses": ["decision_makers"]
  }
}
```

### 3. Interview State Management

Each interview session maintains:

- **Session ID**: Unique identifier (projectId:chatId)
- **Progress Tracking**: 0-100% completion
- **Question History**: All questions asked and responses
- **Gathered Data**: Extracted key-value pairs
- **Pause/Resume**: Ability to pause and resume interviews

State is persisted to `{projectFolder}/interview_states/{chatId}.json`

### 4. Response Quality Analysis

Responses are analyzed for quality with confidence scoring:

- Word count heuristics
- Response completeness
- Confidence score (0.0 - 1.0)

### 5. Interview Types

Built-in interview types:

1. **onboarding**: Brand onboarding interview (default)
2. **content_planning**: Content piece planning
3. **brand_refresh**: Brand update/refresh

Custom interview types can be added via configuration.

## Usage

### 1. Dependency Injection

Register the InterviewAgent service:

```csharp
services.AddScoped<IInterviewAgent>(sp =>
{
    var projects = sp.GetRequiredService<ProjectsRepository>();
    var config = sp.GetRequiredService<IConfiguration>();
    return new InterviewAgent(projects, config);
});
```

### 2. Start an Interview

```csharp
var interviewAgent = serviceProvider.GetService<IInterviewAgent>();

var state = await interviewAgent.StartInterviewAsync(
    projectId: "my-project",
    chatId: "chat-123",
    interviewType: "onboarding",
    cancel: cancellationToken
);

// Present the first question to user
var firstQuestion = state.CurrentQuestion.QuestionText;
```

### 3. Process User Responses

```csharp
var updatedState = await interviewAgent.ProcessResponseAsync(
    projectId: "my-project",
    chatId: "chat-123",
    userResponse: "My brand is Acme Corp",
    cancel: cancellationToken
);

// Check if interview is complete
if (updatedState.IsComplete)
{
    var summary = await interviewAgent.CompleteInterviewAsync(
        projectId: "my-project",
        chatId: "chat-123",
        cancel: cancellationToken
    );
}
else
{
    // Present next question
    var nextQuestion = updatedState.CurrentQuestion.QuestionText;
}
```

### 4. Resume a Paused Interview

```csharp
var resumedState = await interviewAgent.ResumeInterviewAsync(
    projectId: "my-project",
    chatId: "chat-123",
    cancel: cancellationToken
);
```

### 5. Skip Questions

```csharp
var skippedState = await interviewAgent.SkipCurrentQuestionAsync(
    projectId: "my-project",
    chatId: "chat-123",
    cancel: cancellationToken
);
```

## Interview State Structure

```csharp
public class InterviewState
{
    public string SessionId { get; set; }                    // "project:chat"
    public string InterviewType { get; set; }                // "onboarding"
    public InterviewQuestion CurrentQuestion { get; set; }   // Current question
    public List<QuestionResponse> QuestionHistory { get; set; }
    public int ProgressPercentage { get; set; }              // 0-100
    public bool IsComplete { get; set; }
    public bool IsPaused { get; set; }
    public Dictionary<string, object> GatheredData { get; set; }
    public List<string> SuggestedNextQuestions { get; set; }
}
```

## Interview Summary

When an interview completes, a summary is generated:

```csharp
public class InterviewSummary
{
    public string SessionId { get; set; }
    public string InterviewType { get; set; }
    public int TotalQuestionsAsked { get; set; }
    public int QuestionsAnswered { get; set; }
    public int CompletionPercentage { get; set; }
    public Dictionary<string, object> GatheredData { get; set; }
    public List<string> KeyInsights { get; set; }
    public List<string> RecommendedActions { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
}
```

## Integration with Chat

The Interview Agent can be integrated into chat controllers:

1. Detect when interview mode should activate
2. Start interview session
3. Process each user message as a response
4. Present questions in chat UI
5. Complete interview and store gathered data

Example integration:

```csharp
// In ChatController
private readonly IInterviewAgent _interviewAgent;

// Check if interview is active
var interviewState = await _interviewAgent.GetInterviewStateAsync(projectId, chatId);

if (!interviewState.IsComplete && interviewState.CurrentQuestion != null)
{
    // Process as interview response
    var updatedState = await _interviewAgent.ProcessResponseAsync(
        projectId, chatId, userMessage
    );

    // Return next question as chat response
    return updatedState.CurrentQuestion?.QuestionText ?? "Interview complete!";
}
```

## Best Practices

1. **Question Design**
   - Keep questions clear and concise
   - Use follow-up questions for depth
   - Mark critical questions as required
   - Provide response options when appropriate

2. **Interview Flow**
   - Start with basic, easy questions
   - Progress to more detailed questions
   - Group related questions by category
   - Limit total questions to 10-15 for good UX

3. **Data Extraction**
   - Use QuestionId as data key
   - Store responses in GatheredData
   - Group by category for easy access
   - Validate critical data points

4. **State Management**
   - Persist state after each response
   - Handle resume scenarios gracefully
   - Clean up completed interview states
   - Monitor state file size

## Extensibility

### Custom Question Types

Extend `ResponseType` for custom validation:

```csharp
public class CustomInterviewAgent : InterviewAgent
{
    protected override bool ValidateResponse(string response, string responseType)
    {
        return responseType switch
        {
            "email" => IsValidEmail(response),
            "url" => IsValidUrl(response),
            _ => base.ValidateResponse(response, responseType)
        };
    }
}
```

### Custom Adaptive Logic

Override `DetermineNextQuestionAsync` for advanced logic:

```csharp
protected override async Task<InterviewQuestion> DetermineNextQuestionAsync(
    InterviewState state,
    string userResponse,
    CancellationToken cancel)
{
    // Custom logic based on AI analysis, sentiment, etc.
    if (await RequiresFollowUp(userResponse))
    {
        return await GenerateDynamicFollowUpQuestion(state, userResponse);
    }

    return await base.DetermineNextQuestionAsync(state, userResponse, cancel);
}
```

## File Locations

- **Interface**: `Hazina.Tools.Services.Chat/Interfaces/IInterviewAgent.cs`
- **Implementation**: `Hazina.Tools.Services.Chat/Services/InterviewAgent.cs`
- **Configuration Example**: `docs/interview-agent-config-example.json`
- **Documentation**: `docs/INTERVIEW_AGENT.md`

## Future Enhancements

1. **AI-Powered Question Generation**: Dynamic question creation based on context
2. **Multi-Language Support**: Internationalized question banks
3. **Voice Integration**: Voice-based interview mode
4. **Analytics Dashboard**: Visualize interview completion rates and insights
5. **Question Templates**: Reusable question fragments
6. **Conditional Logic**: Complex branching based on multiple responses
7. **Integration with Analysis Fields**: Auto-trigger analysis field generation from responses

## Related Documentation

- [Architecture Analysis](ARCHITECTURE_ANALYSIS_2026-01-05.md)
- [Gathered Data System](../src/Tools/Services/Hazina.Tools.Services.DataGathering/README.md)
- [Chat Services](../src/Tools/Services/Hazina.Tools.Services.Chat/README.md)

---

*Generated 2026-01-05 - Hazina Framework Documentation*
