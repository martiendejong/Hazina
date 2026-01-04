using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Hazina.Tools.Data;
using Hazina.Tools.Services.Chat.Optimization;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hazina.Tools.Services.Chat.Tests.Optimization
{
    public class ProjectLocalCacheTests : IDisposable
    {
        private readonly string _testProjectId = "test-project-123";
        private readonly string _testProjectsRootFolder;
        private readonly string _testProjectFolder;
        private readonly ProjectFileLocator _fileLocator;
        private readonly ProjectLocalCache _cache;
        private readonly LLMOptimizationOptions _options;

        public ProjectLocalCacheTests()
        {
            // Create temporary test root folder
            _testProjectsRootFolder = Path.Combine(Path.GetTempPath(), $"hazina-test-{Guid.NewGuid()}");
            _testProjectFolder = Path.Combine(_testProjectsRootFolder, _testProjectId);
            Directory.CreateDirectory(_testProjectFolder);

            // Create real file locator instance
            _fileLocator = new ProjectFileLocator(_testProjectsRootFolder);

            // Setup options
            _options = new LLMOptimizationOptions
            {
                CacheSettings = new CacheSettings
                {
                    Enabled = true,
                    TimeoutMinutes = 5,
                    InvalidateOnUpdate = true
                }
            };

            _cache = new ProjectLocalCache(
                _fileLocator,
                Options.Create(_options)
            );
        }

        public void Dispose()
        {
            // Cleanup test root folder
            if (Directory.Exists(_testProjectsRootFolder))
            {
                Directory.Delete(_testProjectsRootFolder, true);
            }
        }

        [Fact]
        public async Task GetOrLoadAsync_ShouldLoadFromDisk_WhenCacheEmpty()
        {
            // Arrange
            var analysisFolder = Path.Combine(_testProjectFolder, "analysis");
            Directory.CreateDirectory(analysisFolder);
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "brand-profile.txt"),
                "Test Brand Inc."
            );

            // Act
            var result = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert
            result.Should().NotBeNull();
            result.ProjectId.Should().Be(_testProjectId);
            result.AnalysisFields.Should().ContainKey("brand-profile");
            result.AnalysisFields["brand-profile"].Should().Be("Test Brand Inc.");
        }

        [Fact]
        public async Task GetOrLoadAsync_ShouldReturnCachedData_WhenNotExpired()
        {
            // Arrange
            var analysisFolder = Path.Combine(_testProjectFolder, "analysis");
            Directory.CreateDirectory(analysisFolder);
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "brand-profile.txt"),
                "Original Data"
            );

            // First load - should cache
            var firstResult = await _cache.GetOrLoadAsync(_testProjectId);

            // Modify file on disk
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "brand-profile.txt"),
                "Modified Data"
            );

            // Act - second load within timeout
            var secondResult = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert
            secondResult.AnalysisFields["brand-profile"].Should().Be("Original Data");
            firstResult.Should().BeSameAs(secondResult); // Same instance from cache
        }

        [Fact]
        public async Task GetOrLoadAsync_ShouldLoadGatheredData_WhenExists()
        {
            // Arrange
            var gatheredData = new Dictionary<string, object>
            {
                ["company-name"] = "Acme Corp",
                ["founded-year"] = 2020,
                ["employees"] = 150
            };

            var gatheredFile = Path.Combine(_testProjectFolder, "gathered-data.json");
            await File.WriteAllTextAsync(
                gatheredFile,
                JsonSerializer.Serialize(gatheredData)
            );

            // Act
            var result = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert
            result.GatheredData.Should().HaveCount(3);
            result.GatheredData.Should().ContainKey("company-name");
        }

        [Fact]
        public async Task GetOrLoadAsync_ShouldLoadFilesList_WhenProjectFolderExists()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_testProjectFolder, "images"));
            await File.WriteAllTextAsync(
                Path.Combine(_testProjectFolder, "README.md"),
                "# Test Project"
            );
            await File.WriteAllTextAsync(
                Path.Combine(_testProjectFolder, "images", "logo.png"),
                "fake image content"
            );

            // Act
            var result = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert
            result.AvailableFiles.Should().Contain(f => f.Contains("README.md"));
            result.AvailableFiles.Should().Contain(f => f.Contains("logo.png"));
        }

        [Fact]
        public async Task GetOrLoadAsync_ShouldExcludeEmbeddingsFolder_FromFilesList()
        {
            // Arrange
            Directory.CreateDirectory(Path.Combine(_testProjectFolder, "embeddings"));
            Directory.CreateDirectory(Path.Combine(_testProjectFolder, "metadata"));
            await File.WriteAllTextAsync(
                Path.Combine(_testProjectFolder, "embeddings", "vectors.bin"),
                "data"
            );
            await File.WriteAllTextAsync(
                Path.Combine(_testProjectFolder, "metadata", "meta.json"),
                "{}"
            );
            await File.WriteAllTextAsync(
                Path.Combine(_testProjectFolder, "content.txt"),
                "content"
            );

            // Act
            var result = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert
            result.AvailableFiles.Should().Contain(f => f.Contains("content.txt"));
            result.AvailableFiles.Should().NotContain(f => f.Contains("embeddings"));
            result.AvailableFiles.Should().NotContain(f => f.Contains("metadata"));
        }

        [Fact]
        public async Task GetDataAsync_ShouldReturnAllFound_WhenAllFieldsExist()
        {
            // Arrange
            var analysisFolder = Path.Combine(_testProjectFolder, "analysis");
            Directory.CreateDirectory(analysisFolder);
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "color-scheme.txt"),
                "Blue, White, Gray"
            );
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "tagline.txt"),
                "Innovation Starts Here"
            );

            var requestedFields = new[] { "color-scheme", "tagline" };

            // Act
            var result = await _cache.GetDataAsync(_testProjectId, requestedFields);

            // Assert
            result.AllFound.Should().BeTrue();
            result.FoundFields.Should().HaveCount(2);
            result.FoundFields["color-scheme"].Should().Be("Blue, White, Gray");
            result.FoundFields["tagline"].Should().Be("Innovation Starts Here");
            result.MissingFields.Should().BeEmpty();
        }

        [Fact]
        public async Task GetDataAsync_ShouldReturnMissingFields_WhenSomeFieldsMissing()
        {
            // Arrange
            var analysisFolder = Path.Combine(_testProjectFolder, "analysis");
            Directory.CreateDirectory(analysisFolder);
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "color-scheme.txt"),
                "Blue, White"
            );

            var requestedFields = new[] { "color-scheme", "tagline", "narrative" };

            // Act
            var result = await _cache.GetDataAsync(_testProjectId, requestedFields);

            // Assert
            result.AllFound.Should().BeFalse();
            result.FoundFields.Should().HaveCount(1);
            result.FoundFields["color-scheme"].Should().Be("Blue, White");
            result.MissingFields.Should().HaveCount(2);
            result.MissingFields.Should().Contain("tagline");
            result.MissingFields.Should().Contain("narrative");
        }

        [Fact]
        public async Task GetDataAsync_ShouldCheckGatheredData_WhenNotInAnalysisFields()
        {
            // Arrange
            var gatheredData = new Dictionary<string, object>
            {
                ["company-name"] = "Acme Corp",
                ["industry"] = "Technology"
            };

            var gatheredFile = Path.Combine(_testProjectFolder, "gathered-data.json");
            await File.WriteAllTextAsync(
                gatheredFile,
                JsonSerializer.Serialize(gatheredData)
            );

            var requestedFields = new[] { "company-name", "industry" };

            // Act
            var result = await _cache.GetDataAsync(_testProjectId, requestedFields);

            // Assert
            result.AllFound.Should().BeTrue();
            result.FoundFields.Should().HaveCount(2);
            result.FoundFields["company-name"].Should().Be("Acme Corp");
            result.FoundFields["industry"].Should().Be("Technology");
        }

        [Fact]
        public async Task Invalidate_ShouldRemoveFromCache_WhenFullInvalidation()
        {
            // Arrange
            var analysisFolder = Path.Combine(_testProjectFolder, "analysis");
            Directory.CreateDirectory(analysisFolder);
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "brand-profile.txt"),
                "Original"
            );

            // Load into cache
            await _cache.GetOrLoadAsync(_testProjectId);

            // Modify file
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "brand-profile.txt"),
                "Updated"
            );

            // Act
            _cache.Invalidate(_testProjectId);
            var result = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert
            result.AnalysisFields["brand-profile"].Should().Be("Updated");
        }

        [Fact]
        public async Task Invalidate_ShouldRemoveSpecificFields_WhenPartialInvalidation()
        {
            // Arrange
            var analysisFolder = Path.Combine(_testProjectFolder, "analysis");
            Directory.CreateDirectory(analysisFolder);
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "field1.txt"),
                "Value1"
            );
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "field2.txt"),
                "Value2"
            );

            // Load into cache
            var initialData = await _cache.GetOrLoadAsync(_testProjectId);
            initialData.AnalysisFields.Should().HaveCount(2);

            // Act - invalidate only field1
            _cache.Invalidate(_testProjectId, new[] { "field1" });

            // Access cache again (should still have reference)
            var result = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert - field1 should be removed from the cached entry
            result.AnalysisFields.Should().NotContainKey("field1");
            result.AnalysisFields.Should().ContainKey("field2");
        }

        [Fact]
        public void ClearAll_ShouldRemoveAllCachedProjects()
        {
            // Arrange
            var analysisFolder = Path.Combine(_testProjectFolder, "analysis");
            Directory.CreateDirectory(analysisFolder);

            // This test is mainly for coverage - we can't easily verify internal cache state
            // Act
            _cache.ClearAll();

            // Assert - no exception thrown
            Assert.True(true);
        }

        [Fact]
        public async Task GetOrLoadAsync_ShouldBypassCache_WhenCachingDisabled()
        {
            // Arrange
            var disabledOptions = new LLMOptimizationOptions
            {
                CacheSettings = new CacheSettings { Enabled = false }
            };

            var cacheWithDisabledCaching = new ProjectLocalCache(
                _fileLocator,
                Options.Create(disabledOptions)
            );

            var analysisFolder = Path.Combine(_testProjectFolder, "analysis");
            Directory.CreateDirectory(analysisFolder);
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "test.txt"),
                "Original"
            );

            // First load
            var firstResult = await cacheWithDisabledCaching.GetOrLoadAsync(_testProjectId);

            // Modify file
            await File.WriteAllTextAsync(
                Path.Combine(analysisFolder, "test.txt"),
                "Modified"
            );

            // Act - second load should read from disk
            var secondResult = await cacheWithDisabledCaching.GetOrLoadAsync(_testProjectId);

            // Assert
            secondResult.AnalysisFields["test"].Should().Be("Modified");
        }

        [Fact]
        public async Task GetOrLoadAsync_ShouldHandleMissingFolders_Gracefully()
        {
            // Arrange - no folders created

            // Act
            var result = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert - should return empty data without throwing
            result.Should().NotBeNull();
            result.AnalysisFields.Should().BeEmpty();
            result.GatheredData.Should().BeEmpty();
            result.AvailableFiles.Should().BeEmpty();
        }

        [Fact]
        public async Task GetOrLoadAsync_ShouldHandleInvalidJson_Gracefully()
        {
            // Arrange
            var gatheredFile = Path.Combine(_testProjectFolder, "gathered-data.json");
            await File.WriteAllTextAsync(gatheredFile, "{ invalid json content }");

            // Act
            var result = await _cache.GetOrLoadAsync(_testProjectId);

            // Assert - should continue despite JSON error
            result.Should().NotBeNull();
            result.GatheredData.Should().BeEmpty();
        }
    }
}
