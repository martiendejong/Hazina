using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Hazina.Tools.Data;
using Hazina.Tools.Models;
using Hazina.Tools.Services.Chat.Optimization;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hazina.Tools.Services.Chat.Tests.Optimization
{
    public class RequestClassifierTests
    {
        private readonly Mock<ProjectLocalCache> _mockCache;
        private readonly RequestClassifier _classifier;
        private readonly LLMOptimizationOptions _options;
        private readonly string _testProjectId = "test-project-123";

        public RequestClassifierTests()
        {
            _options = new LLMOptimizationOptions
            {
                ClassificationRules = new ClassificationRules
                {
                    SimpleTestPatterns = new List<string> { "test", "testing", "dit is een test" },
                    GreetingPatterns = new List<string> { "hello", "hi", "hey", "hallo", "good morning" },
                    LocalDataFields = new Dictionary<string, List<string>>
                    {
                        ["color-scheme"] = new List<string> { "color", "scheme", "colors", "kleur" },
                        ["brand-profile"] = new List<string> { "brand name", "company name", "business name" },
                        ["target-audience"] = new List<string> { "target audience", "customers", "market" },
                        ["tagline"] = new List<string> { "tagline", "slogan", "motto" },
                        ["tone-of-voice"] = new List<string> { "tone of voice", "communication style" },
                        ["narrative"] = new List<string> { "narrative", "story", "verhaal" }
                    }
                }
            };

            // Create a real file locator for the mock cache
            var fileLocator = new ProjectFileLocator(Path.GetTempPath());

            _mockCache = new Mock<ProjectLocalCache>(
                fileLocator,
                Options.Create(_options)
            );

            _classifier = new RequestClassifier(_mockCache.Object, Options.Create(_options));
        }

        #region Simple Test Detection

        [Theory]
        [InlineData("test")]
        [InlineData("Test")]
        [InlineData("TEST")]
        [InlineData("testing")]
        [InlineData("dit is een test")]
        public async Task ClassifyRequestAsync_ShouldReturnImmediateResponse_ForSimpleTest(string message)
        {
            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<ImmediateResponseStrategy>();
            var strategy = result as ImmediateResponseStrategy;
            strategy!.Response.Should().Contain("Test received");
            strategy.TokensUsed.Should().Be(0);
        }

        #endregion

        #region Greeting Detection

        [Theory]
        [InlineData("hello")]
        [InlineData("Hello")]
        [InlineData("hi there")]
        [InlineData("hey")]
        [InlineData("hallo")]
        [InlineData("good morning")]
        public async Task ClassifyRequestAsync_ShouldReturnImmediateResponse_ForGreeting(string message)
        {
            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<ImmediateResponseStrategy>();
            var strategy = result as ImmediateResponseStrategy;
            strategy!.Response.Should().ContainAny("morning", "afternoon", "evening");
            strategy.Response.Should().Contain("How can I assist");
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldGenerateTimeBasedGreeting()
        {
            // Arrange
            var message = "hello";
            var hour = DateTime.Now.Hour;
            var expectedTimeOfDay = hour < 12 ? "morning" : hour < 18 ? "afternoon" : "evening";

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            var strategy = result as ImmediateResponseStrategy;
            strategy!.Response.Should().Contain(expectedTimeOfDay);
        }

        #endregion

        #region Local Data Detection

        [Theory]
        [InlineData("what is my color scheme", "color-scheme")]
        [InlineData("show me the colors", "color-scheme")]
        [InlineData("what's my brand name", "brand-profile")]
        [InlineData("tell me about my company name", "brand-profile")]
        [InlineData("who is my target audience", "target-audience")]
        [InlineData("what's my tagline", "tagline")]
        [InlineData("show me my slogan", "tagline")]
        public async Task ClassifyRequestAsync_ShouldIdentifyLocalDataNeeds(string message, string expectedField)
        {
            // Arrange
            var foundData = new Dictionary<string, string>
            {
                [expectedField] = "Test Data"
            };

            _mockCache.Setup(x => x.GetDataAsync(_testProjectId, It.IsAny<List<string>>()))
                .ReturnsAsync(new DataRetrievalResult
                {
                    AllFound = true,
                    FoundFields = foundData,
                    MissingFields = new List<string>()
                });

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<LocalDataResponseStrategy>();
            var strategy = result as LocalDataResponseStrategy;
            strategy!.Data.Should().ContainKey(expectedField);
            strategy.Data[expectedField].Should().Be("Test Data");
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldReturnTargetedRetrieval_WhenLocalDataPartiallyMissing()
        {
            // Arrange
            var message = "what are my colors and tagline";

            _mockCache.Setup(x => x.GetDataAsync(_testProjectId, It.IsAny<List<string>>()))
                .ReturnsAsync(new DataRetrievalResult
                {
                    AllFound = false,
                    FoundFields = new Dictionary<string, string> { ["color-scheme"] = "Blue" },
                    MissingFields = new List<string> { "tagline" }
                });

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<TargetedRetrievalStrategy>();
            var strategy = result as TargetedRetrievalStrategy;
            strategy!.TaskType.Should().Be(TaskType.DataRetrieval);
            strategy.Requirements.Should().Contain("specific project data");
        }

        #endregion

        #region Content Generation Detection

        [Theory]
        [InlineData("generate logo", "logo")]
        [InlineData("create logo for me", "logo")]
        [InlineData("generate tagline", "tagline")]
        [InlineData("create tagline", "tagline")]
        [InlineData("make slogan", "tagline")]
        [InlineData("generate narrative", "narrative")]
        [InlineData("create story", "narrative")]
        [InlineData("generate tone of voice", "tone-of-voice")]
        [InlineData("generate colors", "color-scheme")]
        [InlineData("color palette", "color-scheme")]
        public async Task ClassifyRequestAsync_ShouldDetectGenerationRequest(string message, string expectedType)
        {
            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<TargetedRetrievalStrategy>();
            var strategy = result as TargetedRetrievalStrategy;
            strategy!.TaskType.Should().Be(TaskType.ContentGeneration);
            strategy.Requirements.Should().Contain($"Generate {expectedType}");
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldRequestCorrectFields_ForLogoGeneration()
        {
            // Arrange
            var message = "generate logo";

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            var strategy = result as TargetedRetrievalStrategy;
            strategy!.RequestedFields.Should().Contain("brand-profile");
            strategy.RequestedFields.Should().Contain("color-scheme");
            strategy.RequestedFields.Should().Contain("target-audience");
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldRequestCorrectFields_ForTaglineGeneration()
        {
            // Arrange
            var message = "create tagline";

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            var strategy = result as TargetedRetrievalStrategy;
            strategy!.RequestedFields.Should().Contain("brand-profile");
            strategy.RequestedFields.Should().Contain("target-audience");
            strategy.RequestedFields.Should().Contain("tone-of-voice");
        }

        #endregion

        #region Data Extraction Detection

        [Theory]
        [InlineData("my business is selling software")]
        [InlineData("my company makes widgets")]
        [InlineData("we are a consulting firm")]
        [InlineData("we do web development")]
        [InlineData("we sell products online")]
        [InlineData("our target audience is millennials")]
        [InlineData("our customers are small businesses")]
        [InlineData("mijn bedrijf verkoopt producten")]
        public async Task ClassifyRequestAsync_ShouldDetectDataExtraction(string message)
        {
            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<TargetedRetrievalStrategy>();
            var strategy = result as TargetedRetrievalStrategy;
            strategy!.TaskType.Should().Be(TaskType.DataExtraction);
            strategy.Requirements.Should().Contain("Extract and store");
        }

        #endregion

        #region Simple Question Detection

        [Theory]
        [InlineData("what is my brand")]
        [InlineData("what are the colors")]
        [InlineData("can you tell me about the project")]
        [InlineData("show me the tagline")]
        public async Task ClassifyRequestAsync_ShouldDetectSimpleQuestion_WithRecentContext(string message)
        {
            // Arrange
            var recentHistory = new List<ConversationMessage>
            {
                new ConversationMessage { Role = ChatMessageRole.User, Text = "Previous message 1" },
                new ConversationMessage { Role = ChatMessageRole.Assistant, Text = "Previous response 1" },
                new ConversationMessage { Role = ChatMessageRole.User, Text = "Previous message 2" }
            };

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, recentHistory, _testProjectId);

            // Assert
            // Note: This might return LocalDataResponseStrategy or TargetedRetrievalStrategy
            // depending on whether the cache has the data, so we just verify it's not FullExploration
            result.Should().NotBeOfType<FullExplorationStrategy>();
        }

        [Theory]
        [InlineData("what is that")]
        [InlineData("can you tell me more")]
        public async Task ClassifyRequestAsync_ShouldUseTargetedRetrieval_ForSimpleQuestionWithContext(string message)
        {
            // Arrange
            var recentHistory = new List<ConversationMessage>
            {
                new ConversationMessage { Role = ChatMessageRole.User, Text = "Tell me about the brand" },
                new ConversationMessage { Role = ChatMessageRole.Assistant, Text = "Your brand is focused on innovation" }
            };

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, recentHistory, _testProjectId);

            // Assert
            result.Should().BeOfType<TargetedRetrievalStrategy>();
            var strategy = result as TargetedRetrievalStrategy;
            strategy!.TaskType.Should().Be(TaskType.Question);
        }

        #endregion

        #region Full Exploration Fallback

        [Fact]
        public async Task ClassifyRequestAsync_ShouldReturnFullExploration_ForComplexRequest()
        {
            // Arrange
            var message = "Can you analyze our brand positioning and suggest improvements based on market trends?";

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<FullExplorationStrategy>();
            var strategy = result as FullExplorationStrategy;
            strategy!.Reason.Should().Contain("Complex");
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldReturnFullExploration_ForUnclearRequest()
        {
            // Arrange
            var message = "hmm, I'm not sure what I need exactly";

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<FullExplorationStrategy>();
        }

        #endregion

        #region Edge Cases

        [Fact]
        public async Task ClassifyRequestAsync_ShouldReturnImmediateResponse_ForEmptyMessage()
        {
            // Act
            var result = await _classifier.ClassifyRequestAsync("", new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<ImmediateResponseStrategy>();
            var strategy = result as ImmediateResponseStrategy;
            strategy!.Response.Should().Contain("didn't receive a message");
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldReturnImmediateResponse_ForWhitespaceMessage()
        {
            // Act
            var result = await _classifier.ClassifyRequestAsync("   ", new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<ImmediateResponseStrategy>();
            var strategy = result as ImmediateResponseStrategy;
            strategy!.Response.Should().Contain("didn't receive a message");
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldHandleNullHistory()
        {
            // Arrange
            var message = "what is that?";

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, null!, _testProjectId);

            // Assert
            // Should not throw and should return a strategy
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldBeCaseInsensitive()
        {
            // Arrange
            var messages = new[]
            {
                "TEST",
                "Test",
                "test",
                "HELLO",
                "Hello",
                "hello",
                "GENERATE LOGO",
                "Generate Logo",
                "generate logo"
            };

            foreach (var message in messages)
            {
                // Act
                var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

                // Assert
                result.Should().NotBeOfType<FullExplorationStrategy>($"message '{message}' should be classified");
            }
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldHandleMultipleDataFieldsInOneMessage()
        {
            // Arrange
            var message = "tell me my brand name, colors, and tagline";

            _mockCache.Setup(x => x.GetDataAsync(_testProjectId, It.IsAny<List<string>>()))
                .ReturnsAsync((string projectId, IEnumerable<string> fields) => new DataRetrievalResult
                {
                    AllFound = false,
                    FoundFields = new Dictionary<string, string>(),
                    MissingFields = new List<string>(fields)
                });

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert
            result.Should().BeOfType<TargetedRetrievalStrategy>();

            // Verify cache was called with multiple fields
            _mockCache.Verify(x => x.GetDataAsync(
                _testProjectId,
                It.Is<List<string>>(f => f.Count >= 2)
            ), Times.Once);
        }

        #endregion

        #region Pattern Priority

        [Fact]
        public async Task ClassifyRequestAsync_ShouldPrioritizeTest_OverOtherPatterns()
        {
            // Arrange - message that could match multiple patterns
            var message = "test my color scheme";

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert - should detect as test first
            result.Should().BeOfType<ImmediateResponseStrategy>();
            var strategy = result as ImmediateResponseStrategy;
            strategy!.Response.Should().Contain("Test received");
        }

        [Fact]
        public async Task ClassifyRequestAsync_ShouldPrioritizeGreeting_OverLocalData()
        {
            // Arrange
            var message = "hello, what are my colors";

            // Act
            var result = await _classifier.ClassifyRequestAsync(message, new List<ConversationMessage>(), _testProjectId);

            // Assert - should detect as greeting first
            result.Should().BeOfType<ImmediateResponseStrategy>();
            var strategy = result as ImmediateResponseStrategy;
            strategy!.Response.Should().ContainAny("morning", "afternoon", "evening");
        }

        #endregion
    }
}
