using Bogus;

using Hazina.AgentFactory.Services.Email;
using Hazina.LLMs;

namespace Hazina.AgentFactory.Tests.Fixtures;

/// <summary>
/// Generates fake data for testing using Bogus library.
/// </summary>
public static class FakeDataGenerator
{
    private static readonly Faker Faker = new Faker();

    /// <summary>
    /// Generates a random email summary.
    /// </summary>
    public static EmailSummary GenerateEmailSummary()
    {
        return new EmailSummary
        {
            Id = Faker.Random.Guid().ToString(),
            Sender = Faker.Internet.Email(),
            Subject = Faker.Lorem.Sentence(5),
            Date = Faker.Date.Recent()
        };
    }

    /// <summary>
    /// Generates multiple email summaries.
    /// </summary>
    public static List<EmailSummary> GenerateEmailSummaries(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateEmailSummary())
            .ToList();
    }

    /// <summary>
    /// Generates a random email detail.
    /// </summary>
    public static EmailDetail GenerateEmailDetail()
    {
        return new EmailDetail
        {
            Sender = Faker.Internet.Email(),
            Subject = Faker.Lorem.Sentence(5),
            Body = Faker.Lorem.Paragraphs(3)
        };
    }

    /// <summary>
    /// Generates a random store config.
    /// </summary>
    public static StoreConfig GenerateStoreConfig()
    {
        return new StoreConfig
        {
            Name = Faker.Lorem.Word(),
            Description = Faker.Lorem.Sentence(),
            Path = Faker.System.DirectoryPath(),
            FileFilters = new[] { "*.cs", "*.json" },
            SubDirectory = "",
            ExcludePattern = new[] { "bin", "obj" }
        };
    }

    /// <summary>
    /// Generates a random agent config.
    /// </summary>
    public static AgentConfig GenerateAgentConfig()
    {
        return new AgentConfig
        {
            Name = Faker.Lorem.Word() + "-agent",
            Description = Faker.Lorem.Sentence(),
            ExplicitModify = Faker.Random.Bool()
        };
    }

    /// <summary>
    /// Generates a random flow config.
    /// </summary>
    public static FlowConfig GenerateFlowConfig()
    {
        return new FlowConfig
        {
            Name = Faker.Lorem.Word() + "-flow",
            Description = Faker.Lorem.Sentence()
        };
    }

    /// <summary>
    /// Generates a random chat message with default User role.
    /// </summary>
    public static HazinaChatMessage GenerateChatMessage() => GenerateChatMessage(HazinaMessageRole.User);

    /// <summary>
    /// Generates a random chat message with specified role.
    /// </summary>
    public static HazinaChatMessage GenerateChatMessage(HazinaMessageRole role)
    {
        return new HazinaChatMessage
        {
            MessageId = Guid.NewGuid(),
            Role = role,
            Text = Faker.Lorem.Paragraph(),
            AgentName = Faker.Lorem.Word(),
            FunctionName = string.Empty,
            FlowName = string.Empty,
            Response = string.Empty
        };
    }

    /// <summary>
    /// Generates multiple chat messages.
    /// </summary>
    public static List<HazinaChatMessage> GenerateChatMessages(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => GenerateChatMessage(i % 2 == 0 ? HazinaMessageRole.User : HazinaMessageRole.Assistant))
            .ToList();
    }

    /// <summary>
    /// Generates a random SQL query.
    /// </summary>
    public static string GenerateSqlQuery()
    {
        var table = Faker.Lorem.Word();
        return $"SELECT * FROM {table} WHERE id = {Faker.Random.Int(1, 1000)}";
    }

    /// <summary>
    /// Generates random BigQuery dataset names.
    /// </summary>
    public static List<string> GenerateDatasetNames(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => Faker.Lorem.Word() + "_dataset")
            .ToList();
    }
}
