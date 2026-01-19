using Hazina.AI.Workflows.Configuration;
using Hazina.AI.Workflows.Engine;
using Hazina.AI.Guardrails;
using Hazina.AI.Guardrails.Implementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hazina.AI.Workflows.Tests;

/// <summary>
/// Integration tests for Visual Workflow System Phase 1
/// Tests workflow configuration parsing, serialization, and guardrails
/// </summary>
[TestClass]
public class WorkflowIntegrationTests
{
    /// <summary>
    /// Test 1: Parse onboarding workflow successfully
    /// Validates .hazina v2.0 file parsing
    /// </summary>
    [TestMethod]
    public void Parse_OnboardingWorkflow_Success()
    {
        // Arrange
        var workflowPath = @"C:\stores\brand2boost\.hazina\workflows\onboarding-test.hazina";

        // Skip if file doesn't exist (CI environment)
        if (!File.Exists(workflowPath))
        {
            Assert.Inconclusive("Workflow file not found - skipping integration test");
            return;
        }

        // Act
        var workflow = HazinaWorkflowConfigParser.LoadFromFile(workflowPath);

        // Assert
        Assert.AreEqual("OnboardingWorkflow", workflow.Name);
        Assert.AreEqual("2.0", workflow.Version);
        Assert.AreEqual(3, workflow.Steps.Count);
        Assert.IsTrue(workflow.Steps.All(s => !string.IsNullOrEmpty(s.Name)));
        Assert.IsTrue(workflow.Steps.All(s => !string.IsNullOrEmpty(s.OutputKey)));
    }

    /// <summary>
    /// Test 2: Parse brand analysis workflow
    /// Validates multi-step workflow with RAG configuration
    /// </summary>
    [TestMethod]
    public void Parse_BrandAnalysisWorkflow_Success()
    {
        // Arrange
        var workflowPath = @"C:\stores\brand2boost\.hazina\workflows\brand-analysis-test.hazina";

        // Skip if file doesn't exist (CI environment)
        if (!File.Exists(workflowPath))
        {
            Assert.Inconclusive("Workflow file not found - skipping integration test");
            return;
        }

        // Act
        var workflow = HazinaWorkflowConfigParser.LoadFromFile(workflowPath);

        // Assert
        Assert.AreEqual("BrandAnalysisWorkflow", workflow.Name);
        Assert.AreEqual(4, workflow.Steps.Count);

        // Verify RAG configuration
        var stepsWithRAG = workflow.Steps.Count(s => s.RAGConfig != null);
        Assert.IsTrue(stepsWithRAG >= 3, "At least 3 steps should have RAG configuration");
    }

    /// <summary>
    /// Test 3: Parse content generation workflow with guardrails
    /// Validates guardrail configuration
    /// </summary>
    [TestMethod]
    public void Parse_ContentGenerationWorkflow_WithGuardrails()
    {
        // Arrange
        var workflowPath = @"C:\stores\brand2boost\.hazina\workflows\content-generation-test.hazina";

        // Skip if file doesn't exist (CI environment)
        if (!File.Exists(workflowPath))
        {
            Assert.Inconclusive("Workflow file not found - skipping integration test");
            return;
        }

        // Act
        var workflow = HazinaWorkflowConfigParser.LoadFromFile(workflowPath);

        // Assert
        Assert.AreEqual("ContentGenerationWorkflow", workflow.Name);
        Assert.AreEqual(3, workflow.Steps.Count);

        // Verify guardrails are configured
        var stepsWithGuardrails = workflow.Steps.Count(s => s.Guardrails.Any());
        Assert.IsTrue(stepsWithGuardrails >= 2, "At least 2 steps should have guardrails");

        // Verify specific guardrails
        var allGuardrails = workflow.Steps.SelectMany(s => s.Guardrails).Distinct().ToList();
        Assert.IsTrue(allGuardrails.Contains("no-pii"), "Should include no-pii guardrail");
        Assert.IsTrue(allGuardrails.Contains("token-limit"), "Should include token-limit guardrail");
        Assert.IsTrue(allGuardrails.Contains("json-schema"), "Should include json-schema guardrail");
    }

    /// <summary>
    /// Test 4: Backward compatibility with v1 format
    /// Ensures legacy workflows still parse
    /// </summary>
    [TestMethod]
    public void Parse_V1Workflow_BackwardCompatibility()
    {
        // Arrange - Create simple v1 format workflow
        var v1Workflow = @"
# Test Workflow
Name: TestWorkflow
Description: Simple test workflow in v1 format
CallsAgents: Agent1,Agent2
";

        // Act
        var workflow = HazinaWorkflowConfigParser.Parse(v1Workflow);

        // Assert
        Assert.AreEqual("TestWorkflow", workflow.Name);
        Assert.AreEqual("1.0", workflow.Version);
        Assert.IsTrue(workflow.Steps.Count >= 1, "Should have at least 1 step from v1 agents");
    }

    /// <summary>
    /// Test 5: PII detection guardrail blocks SSN
    /// </summary>
    [TestMethod]
    public async Task Guardrail_PIIDetection_BlocksSSN()
    {
        // Arrange
        var guardrail = new PIIDetectionGuardrail();
        var context = new GuardrailContext();
        var contentWithPII = "User SSN is 123-45-6789";

        // Act
        var result = await guardrail.ValidateAsync(contentWithPII, context);

        // Assert
        Assert.IsFalse(result.Passed, "Should fail with PII");
        Assert.IsTrue(result.FailureReason!.Contains("PII"), $"Reason: {result.FailureReason}");
    }

    /// <summary>
    /// Test 6: PII detection guardrail blocks email
    /// </summary>
    [TestMethod]
    public async Task Guardrail_PIIDetection_BlocksEmail()
    {
        // Arrange
        var guardrail = new PIIDetectionGuardrail();
        var context = new GuardrailContext();
        var contentWithPII = "Contact user@example.com for details";

        // Act
        var result = await guardrail.ValidateAsync(contentWithPII, context);

        // Assert
        Assert.IsFalse(result.Passed, "Should fail with email PII");
        Assert.IsTrue(result.FailureReason!.Contains("PII"), $"Reason: {result.FailureReason}");
    }

    /// <summary>
    /// Test 7: PII detection passes clean content
    /// </summary>
    [TestMethod]
    public async Task Guardrail_PIIDetection_PassesCleanContent()
    {
        // Arrange
        var guardrail = new PIIDetectionGuardrail();
        var context = new GuardrailContext();
        var cleanContent = "This is a clean message without any personal information";

        // Act
        var result = await guardrail.ValidateAsync(cleanContent, context);

        // Assert
        Assert.IsTrue(result.Passed, "Should pass without PII");
    }

    /// <summary>
    /// Test 8: Token limit guardrail blocks excessive content
    /// </summary>
    [TestMethod]
    public async Task Guardrail_TokenLimit_BlocksExcessiveContent()
    {
        // Arrange
        var guardrail = new TokenLimitGuardrail();
        var context = new GuardrailContext();
        var longContent = new string('x', 10000); // ~2500 tokens

        // Act
        var result = await guardrail.ValidateAsync(longContent, context);

        // Assert
        Assert.IsFalse(result.Passed, "Should fail with too many tokens");
        Assert.IsTrue(result.FailureReason!.Contains("token"), $"Reason: {result.FailureReason}");
    }

    /// <summary>
    /// Test 9: Token limit guardrail passes normal content
    /// </summary>
    [TestMethod]
    public async Task Guardrail_TokenLimit_PassesNormalContent()
    {
        // Arrange
        var guardrail = new TokenLimitGuardrail();
        var context = new GuardrailContext();
        var normalContent = "This is a normal message with reasonable length";

        // Act
        var result = await guardrail.ValidateAsync(normalContent, context);

        // Assert
        Assert.IsTrue(result.Passed, "Should pass with normal length");
    }

    /// <summary>
    /// Test 10: JSON schema guardrail validates valid JSON
    /// </summary>
    [TestMethod]
    public async Task Guardrail_JSONSchema_ValidatesValidJSON()
    {
        // Arrange
        var guardrail = new JSONSchemaGuardrail();
        var context = new GuardrailContext();
        var validJSON = @"{""status"": ""success"", ""data"": ""test""}";

        // Act
        var result = await guardrail.ValidateAsync(validJSON, context);

        // Assert
        Assert.IsTrue(result.Passed, "Should pass with valid JSON");
    }

    /// <summary>
    /// Test 11: JSON schema guardrail blocks invalid JSON
    /// </summary>
    [TestMethod]
    public async Task Guardrail_JSONSchema_BlocksInvalidJSON()
    {
        // Arrange
        var guardrail = new JSONSchemaGuardrail();
        var context = new GuardrailContext();
        var invalidJSON = "Not valid JSON {";

        // Act
        var result = await guardrail.ValidateAsync(invalidJSON, context);

        // Assert
        Assert.IsFalse(result.Passed, "Should fail with invalid JSON");
        Assert.IsTrue(result.FailureReason!.Contains("JSON"), $"Reason: {result.FailureReason}");
    }

    /// <summary>
    /// Test 12: Variable substitution in workflow context
    /// Validates {key} placeholder replacement
    /// </summary>
    [TestMethod]
    public void WorkflowContext_ProcessTemplate_SubstitutesVariables()
    {
        // Arrange
        var context = new WorkflowExecutionContext(new Dictionary<string, object>
        {
            ["userName"] = "John Doe",
            ["userAge"] = 30,
            ["userCity"] = "Amsterdam"
        });

        var template = "User {userName} is {userAge} years old and lives in {userCity}.";

        // Act
        var result = context.ProcessTemplate(template);

        // Assert
        Assert.AreEqual("User John Doe is 30 years old and lives in Amsterdam.", result);
    }

    /// <summary>
    /// Test 13: Parser extracts per-step LLM configuration
    /// </summary>
    [TestMethod]
    public void Parser_ParseV2Format_ExtractsLLMConfiguration()
    {
        // Arrange
        var hazinaContent = @"
# Workflow Definition
Name: TestWorkflow
Description: Test workflow
Version: 2.0
Steps: 1

[Step1]
Name: FirstStep
Type: AgentTask
AgentName: Agent1
Input: Test input
Temperature: 0.5
MaxTokens: 1000
Model: gpt-4
TopP: 0.9
OutputKey: step1Result
";

        // Act
        var workflow = HazinaWorkflowConfigParser.Parse(hazinaContent);

        // Assert
        Assert.AreEqual("TestWorkflow", workflow.Name);
        Assert.AreEqual("2.0", workflow.Version);
        Assert.AreEqual(1, workflow.Steps.Count);

        var step1 = workflow.Steps[0];
        Assert.AreEqual("FirstStep", step1.Name);
        Assert.IsNotNull(step1.LLMConfig);
        Assert.AreEqual(0.5f, step1.LLMConfig.Temperature, 0.001);
        Assert.AreEqual(1000, step1.LLMConfig.MaxTokens);
        Assert.AreEqual("gpt-4", step1.LLMConfig.Model);
        Assert.AreEqual(0.9f, step1.LLMConfig.TopP, 0.001);
    }

    /// <summary>
    /// Test 14: Parser extracts per-step RAG configuration
    /// </summary>
    [TestMethod]
    public void Parser_ParseV2Format_ExtractsRAGConfiguration()
    {
        // Arrange
        var hazinaContent = @"
# Workflow Definition
Name: TestWorkflow
Description: Test workflow
Version: 2.0
Steps: 1

[Step1]
Name: FirstStep
Type: AgentTask
AgentName: Agent1
Input: Test input
Model: gpt-3.5-turbo
RAGStore: test-store
RAGTopK: 5
RAGMinSimilarity: 0.8
RAGUseEmbeddings: true
RAGMetadataFilter: tags:test
OutputKey: step1Result
";

        // Act
        var workflow = HazinaWorkflowConfigParser.Parse(hazinaContent);

        // Assert
        var step1 = workflow.Steps[0];
        Assert.IsNotNull(step1.RAGConfig);
        Assert.AreEqual("test-store", step1.RAGConfig.StoreName);
        Assert.AreEqual(5, step1.RAGConfig.TopK);
        Assert.AreEqual(0.8, step1.RAGConfig.MinSimilarity, 0.001);
        Assert.IsTrue(step1.RAGConfig.UseEmbeddings);
        Assert.AreEqual("tags:test", step1.RAGConfig.MetadataFilter);
    }

    /// <summary>
    /// Test 15: Round-trip serialization preserves workflow
    /// Parse → Serialize → Parse should match
    /// </summary>
    [TestMethod]
    public void Parser_RoundTripSerialization_PreservesWorkflow()
    {
        // Arrange
        var originalWorkflow = new WorkflowConfig
        {
            Name = "RoundTripTest",
            Description = "Test round-trip serialization",
            Version = "2.0",
            Steps = new List<WorkflowStepConfig>
            {
                new WorkflowStepConfig
                {
                    Name = "Step1",
                    Type = StepType.AgentTask,
                    AgentName = "TestAgent",
                    Input = "Test input",
                    LLMConfig = new LLMStepConfig
                    {
                        Model = "gpt-4",
                        Temperature = 0.7f,
                        MaxTokens = 1000
                    },
                    RAGConfig = new RAGStepConfig
                    {
                        StoreName = "test-store",
                        TopK = 5,
                        MinSimilarity = 0.8
                    },
                    Guardrails = new List<string> { "no-pii" },
                    OutputKey = "result"
                }
            }
        };

        // Act
        var serialized = HazinaWorkflowConfigParser.Serialize(originalWorkflow);
        var deserialized = HazinaWorkflowConfigParser.Parse(serialized);

        // Assert
        Assert.AreEqual(originalWorkflow.Name, deserialized.Name);
        Assert.AreEqual(originalWorkflow.Description, deserialized.Description);
        Assert.AreEqual(originalWorkflow.Version, deserialized.Version);
        Assert.AreEqual(originalWorkflow.Steps.Count, deserialized.Steps.Count);

        var originalStep = originalWorkflow.Steps[0];
        var deserializedStep = deserialized.Steps[0];

        Assert.AreEqual(originalStep.Name, deserializedStep.Name);
        Assert.AreEqual(originalStep.LLMConfig!.Model, deserializedStep.LLMConfig!.Model);
        Assert.AreEqual(originalStep.LLMConfig.Temperature, deserializedStep.LLMConfig.Temperature, 0.001);
        Assert.AreEqual(originalStep.RAGConfig!.StoreName, deserializedStep.RAGConfig!.StoreName);
        Assert.AreEqual(originalStep.Guardrails.Count, deserializedStep.Guardrails.Count);
    }
}
