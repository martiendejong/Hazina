namespace Hazina.Agent.API.Models;

public class LearningEvent
{
    public required string EventId { get; set; }
    public required DateTime Timestamp { get; set; }
    public required string AgentId { get; set; }
    public required string SessionId { get; set; }
    public required string EventType { get; set; } // "pattern", "skill", "correction", "insight"
    public required object Data { get; set; }
    public double Confidence { get; set; }
}

public class PatternLearning
{
    public required string PatternId { get; set; }
    public required string Description { get; set; }
    public required List<string> Triggers { get; set; }
    public required List<string> Actions { get; set; }
    public double Confidence { get; set; }
    public int ValidationCount { get; set; }
}

public class SkillAcquisition
{
    public required string SkillName { get; set; }
    public required string Domain { get; set; }
    public required string HowLearned { get; set; }
    public double Proficiency { get; set; }
}

public class ErrorCorrection
{
    public required string ErrorType { get; set; }
    public required string WhatWentWrong { get; set; }
    public required string Correction { get; set; }
    public bool Resolved { get; set; }
}
