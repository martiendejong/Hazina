using FluentAssertions;

using Hazina.AgentFactory.Services.BigQuery;
using Hazina.AgentFactory.Tests.Fixtures;

namespace Hazina.AgentFactory.Tests.Services;

/// <summary>
/// Unit tests for BigQueryService.
/// Note: These tests focus on configuration and interface behavior.
/// Actual BigQuery calls require integration tests with real credentials.
/// </summary>
public class BigQueryServiceTests : TestBase
{
    private const string TestProjectId = "test-project-id";
    private const string TestJsonCredentials = @"{""type"": ""service_account"", ""project_id"": ""test-project-id""}";

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullProjectId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new BigQueryService(null!, TestJsonCredentials);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("projectId");
    }

    [Fact]
    public void Constructor_WithNullJsonCredentials_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new BigQueryService(TestProjectId, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jsonCredentials");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void BigQueryService_ImplementsIBigQueryService()
    {
        // Arrange
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);

        // Assert
        service.Should().BeAssignableTo<IBigQueryService>();
    }

    [Fact]
    public void IBigQueryService_HasAllRequiredMethods()
    {
        // Arrange
        var interfaceType = typeof(IBigQueryService);

        // Assert
        interfaceType.GetMethod("GetClient").Should().NotBeNull();
        interfaceType.GetMethod("GetDatasetsAsync").Should().NotBeNull();
        interfaceType.GetMethod("GetTablesAsync").Should().NotBeNull();
        interfaceType.GetMethod("GetTableFieldsAsync").Should().NotBeNull();
        interfaceType.GetMethod("ExecuteQueryAsync").Should().NotBeNull();
    }

    #endregion

    #region GetTablesAsync Validation Tests

    [Fact]
    public async Task GetTablesAsync_WithEmptyDataset_ReturnsErrorMessage()
    {
        // Arrange
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);

        // Act
        var result = await service.GetTablesAsync("", CancellationToken.None);

        // Assert
        result.Should().Be("No dataset provided.");
    }

    [Fact]
    public async Task GetTablesAsync_WithWhitespaceDataset_ReturnsErrorMessage()
    {
        // Arrange
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);

        // Act
        var result = await service.GetTablesAsync("   ", CancellationToken.None);

        // Assert
        result.Should().Be("No dataset provided.");
    }

    #endregion

    #region GetTableFieldsAsync Validation Tests

    [Fact]
    public async Task GetTableFieldsAsync_WithEmptyDataset_ReturnsErrorMessage()
    {
        // Arrange
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);

        // Act
        var result = await service.GetTableFieldsAsync("", "tablename", CancellationToken.None);

        // Assert
        result.Should().Be("No dataset provided.");
    }

    [Fact]
    public async Task GetTableFieldsAsync_WithEmptyTableName_ReturnsErrorMessage()
    {
        // Arrange
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);

        // Act
        var result = await service.GetTableFieldsAsync("dataset", "", CancellationToken.None);

        // Assert
        result.Should().Be("No table provided.");
    }

    #endregion

    #region ExecuteQueryAsync Validation Tests

    [Fact]
    public async Task ExecuteQueryAsync_WithEmptyQuery_ReturnsErrorMessage()
    {
        // Arrange
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);

        // Act
        var result = await service.ExecuteQueryAsync("", CancellationToken.None);

        // Assert
        result.Should().Be("No query provided.");
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithWhitespaceQuery_ReturnsErrorMessage()
    {
        // Arrange
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);

        // Act
        var result = await service.ExecuteQueryAsync("   ", CancellationToken.None);

        // Assert
        result.Should().Be("No query provided.");
    }

    #endregion

    #region ExtractAsString Tests

    [Fact]
    public void ExtractAsString_WithEmptyResults_ReturnsNoResultsMessage()
    {
        // Arrange
        var emptyResults = Enumerable.Empty<Google.Cloud.BigQuery.V2.BigQueryRow>();

        // Act
        var result = BigQueryService.ExtractAsString(emptyResults);

        // Assert
        result.Should().Be("Query executed, but no results found.");
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task GetDatasetsAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new BigQueryService(TestProjectId, TestJsonCredentials);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Func<Task> act = async () => await service.GetDatasetsAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}
