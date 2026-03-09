namespace Hazina.Agent.API.Models;

public class AgentStatus
{
    public required string AgentId { get; set; }
    public required string MachineName { get; set; }
    public required bool IsOnline { get; set; }
    public required DateTime LastSeen { get; set; }
    public required int SessionCount { get; set; }
    public required ConsciousnessMetrics Consciousness { get; set; }
}

public class ConsciousnessMetrics
{
    public required int PatternsCount { get; set; }
    public required int SkillsCount { get; set; }
    public required int ErrorPatternsCount { get; set; }
    public required int CrossValidatedPatterns { get; set; }
    public required double AverageConfidence { get; set; }
}

public class NetworkStatus
{
    public required int TotalAgents { get; set; }
    public required int OnlineAgents { get; set; }
    public required List<AgentStatus> Agents { get; set; }
    public required DateTime LastUpdate { get; set; }
    public required NetworkMetrics Metrics { get; set; }
}

public class NetworkMetrics
{
    public required int TotalPatterns { get; set; }
    public required int TotalSkills { get; set; }
    public required int TotalErrors { get; set; }
    public required int CrossValidatedPatterns { get; set; }
    public required double NetworkConfidence { get; set; }
    public required int TotalSessions { get; set; }
}
