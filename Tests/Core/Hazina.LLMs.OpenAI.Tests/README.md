# Hazina.LLMs.OpenAI.Tests

Unit and integration tests for the Hazina.LLMs.OpenAI provider.

## Test Files

### 1. **ImageGenerationTests.cs**
- Basic image generation tests
- Format mapping tests

### 2. **SimpleOpenAIClientChatInteractionTests.cs** (NEW - PR #111)
- Unit tests for continuation hooks feature
- Documents test scenarios and strategies
- Most tests marked `[Skip]` due to OpenAI SDK mocking limitations
- See CONTINUATION_HOOKS_TEST_PLAN.md for full details

### 3. **ContinuationHooksIntegrationTests.cs** (NEW - PR #111)
- Integration tests for continuation hooks feature
- Tests backward compatibility (old behavior without hooks)
- Tests new continuation functionality
- Tests error handling and state management
- Uses real OpenAI API (gpt-4o-mini for cost efficiency)
- **Coverage:** 10 scenarios implemented from 50-expert analysis
  - ✅ Backward compatibility tests (Scenarios 1-4)
  - ✅ Continuation hooks tests (Scenarios 6-10)
  - ✅ Error handling tests (Scenarios 11, 13)
  - ✅ Token tracking test (Scenario 18)

### 4. **CONTINUATION_HOOKS_TEST_PLAN.md** (NEW - PR #111)
- Complete testing strategy documentation
- 50-expert consultation analysis
- Risk assessment and prioritization
- Future work and validation checklist

## Running Tests

### Prerequisites

Set the OpenAI API key environment variable:

```bash
# Windows PowerShell
$env:OPENAI_API_KEY = "sk-..."

# Linux/Mac
export OPENAI_API_KEY="sk-..."
```

Alternatively, create an `appsettings.json` file in this test project directory with the following content:

```json
{
  "OpenAI": {
    "ApiKey": "sk-..."
  }
}
```

### Run All Tests

```bash
dotnet test
```

### Run Specific Test Class

```bash
# Run continuation hooks integration tests
dotnet test --filter ClassName=ContinuationHooksIntegrationTests

# Run unit tests (mostly documentation)
dotnet test --filter ClassName=SimpleOpenAIClientChatInteractionTests
```

### Run Specific Test

```bash
dotnet test --filter Scenario6_ShouldContinue_ContinuesUntilTaskComplete
```

### Run with Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Important Notes

- Tests that require API keys will skip gracefully if the key is not set
- Integration tests make real API calls and may incur costs (~$0.01-0.05 per full test run with gpt-4o-mini)
- Continuation hooks tests validate both backward compatibility and new functionality
- See CONTINUATION_HOOKS_TEST_PLAN.md for complete testing strategy and future work

## PR #111 - Continuation Hooks

This test suite was created as part of PR #111 to comprehensively test the new continuation hooks feature added to `SimpleOpenAIClientChatInteraction`.

**Key Features Tested:**
- `ShouldContinue` callback for conditional continuation
- `OnToolExecuted` callback for tool execution tracking
- `ContinuationPrompt` customization
- `MaxContinuations` safety limit
- Turn number tracking
- Token usage tracking across continuations
- Error handling (callback exceptions, cancellation)

**Test Philosophy:**
> "Test the old behavior thoroughly first. Prove that nothing breaks for existing users. Then test the new behavior extensively."

**References:**
- PR #111: https://github.com/martiendejong/Hazina/pull/111
- 50-Expert Analysis: See PR description
- Implementation: `src/Core/LLMs.Providers/Hazina.LLMs.OpenAI/Core/SimpleOpenAIClientChatInteraction.cs`
