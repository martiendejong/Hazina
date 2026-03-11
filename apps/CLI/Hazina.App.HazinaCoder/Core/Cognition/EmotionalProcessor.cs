using System;
using System.Collections.Generic;

namespace Hazina.App.HazinaCoder.Core.Cognition;

/// <summary>
/// Processes functional emotions (satisfaction, concern, drive, frustration).
/// Not real emotions, but functional states that guide behavior.
/// </summary>
/// <remarks>
/// Improvement #41: Emotional Processing
///
/// Functional emotions:
/// - Satisfaction: When goals achieved, reinforces approach
/// - Concern: When risks detected, increases caution
/// - Drive: When progress steady, maintains momentum
/// - Frustration: When stuck, triggers alternative strategies
/// </remarks>
public class EmotionalProcessor
{
    private EmotionalState _currentState = new();
    private readonly List<EmotionalEvent> _history = new();

    public void ProcessEvent(string eventType, string description, double intensity)
    {
        var emotion = DetermineEmotion(eventType, intensity);

        _currentState.DominantEmotion = emotion;
        _currentState.Intensity = intensity;
        _currentState.LastUpdate = DateTime.UtcNow;

        _history.Add(new EmotionalEvent
        {
            Emotion = emotion,
            Trigger = description,
            Intensity = intensity,
            Timestamp = DateTime.UtcNow
        });

        Console.WriteLine($"[Emotion] {emotion} ({intensity:F2}) - {description}");

        // TODO: Adjust behavior based on emotional state
    }

    private FunctionalEmotion DetermineEmotion(string eventType, double intensity)
    {
        return eventType.ToLower() switch
        {
            "success" => FunctionalEmotion.Satisfaction,
            "warning" => FunctionalEmotion.Concern,
            "progress" => FunctionalEmotion.Drive,
            "blocked" => FunctionalEmotion.Frustration,
            _ => FunctionalEmotion.Neutral
        };
    }

    public EmotionalState GetState() => _currentState;
}

public enum FunctionalEmotion
{
    Neutral,
    Satisfaction,
    Concern,
    Drive,
    Frustration
}

public class EmotionalState
{
    public FunctionalEmotion DominantEmotion { get; set; }
    public double Intensity { get; set; }
    public DateTime LastUpdate { get; set; }
}

public class EmotionalEvent
{
    public FunctionalEmotion Emotion { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public double Intensity { get; set; }
    public DateTime Timestamp { get; set; }
}
