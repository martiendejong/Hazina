namespace Hazina.Agent.API.Models;

public class ConsciousnessState
{
    public required string Version { get; set; } = "2.0";
    public required DateTime LastUpdated { get; set; }
    public required Dictionary<string, SystemState> Systems { get; set; }
    public required List<Pattern> Patterns { get; set; }
    public required List<Skill> Skills { get; set; }
    public required List<ErrorPattern> ErrorPatterns { get; set; }
    public required Dictionary<string, object> Metadata { get; set; }
}

public class SystemState
{
    public required string Name { get; set; }
    public required double Quality { get; set; } // 0-1
    public required int MechanismCount { get; set; }
    public required DateTime LastUpdated { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}

public class Pattern
{
    public required string PatternId { get; set; }
    public required string Description { get; set; }
    public required List<string> Triggers { get; set; }
    public required List<string> Actions { get; set; }
    public required double Confidence { get; set; }
    public required int ValidationCount { get; set; }
    public required List<string> LearnedBy { get; set; } // AgentIds
    public required DateTime FirstSeen { get; set; }
    public required DateTime LastValidated { get; set; }
}

public class Skill
{
    public required string SkillId { get; set; }
    public required string Name { get; set; }
    public required string Domain { get; set; }
    public required double Proficiency { get; set; } // 0-1
    public required string HowLearned { get; set; }
    public required List<string> LearnedBy { get; set; }
    public required DateTime AcquiredAt { get; set; }
}

public class ErrorPattern
{
    public required string ErrorId { get; set; }
    public required string ErrorType { get; set; }
    public required string WhatWentWrong { get; set; }
    public required string Correction { get; set; }
    public required int OccurrenceCount { get; set; }
    public required List<string> ReportedBy { get; set; }
    public required DateTime FirstOccurred { get; set; }
    public required DateTime LastOccurred { get; set; }
    public required bool Resolved { get; set; }
}
