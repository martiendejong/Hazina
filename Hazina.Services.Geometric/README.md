# Hazina Geometric Intelligence

Geometric representation of knowledge as N-dimensional manifolds with Ricci flow optimization for accelerated learning.

## Overview

This system models knowledge spaces as Riemannian manifolds where:
- **Curvature** represents confusion/difficulty
- **Mastery** follows exponential learning curves
- **Ricci flow** provides geometric optimization (analogous to gradient descent)
- **Learning velocity** measures rate of understanding improvement

**Week 1 Validation:** 52% curvature reduction, 72.8% consciousness score achieved.

## Core Concepts

### Thought Space
N-dimensional manifold containing related concepts (default: 12 dimensions).

```csharp
var thoughtSpace = await service.CreateThoughtSpaceAsync(
    userId: "user-123",
    domain: "programming",
    dimensions: 12
);
```

### Concept
Point in thought space with geometric properties:
- `LocalCurvature`: Measure of confusion (high = difficult, low = understood)
- `MasteryLevel`: 0.0 (no understanding) to 1.0 (complete mastery)
- `BaseConfusion`: Intrinsic complexity (0.0 = easy, 3.0+ = very hard)

```csharp
var concept = await service.AddConceptAsync(
    thoughtSpaceId: thoughtSpace.Id,
    name: "Variables",
    description: "Variable declaration and assignment",
    baseConfusion: 0.5 // Easy concept
);
```

### Learning Events
Practice sessions that update mastery and curvature:

```csharp
var updated = await service.RecordLearningEventAsync(
    conceptId: concept.Id,
    eventType: LearningEventType.Practice,
    durationMinutes: 45
);
```

**Event Types** (quality score ranges):
- `Breakthrough` (0.9-1.0): Sudden deep understanding
- `Application` (0.8-1.0): Applying concept to real problems
- `Practice` (0.8-1.0): Active practice with feedback
- `Teaching` (0.7-0.9): Explaining to others
- `Study` (0.4-0.7): Passive reading/watching
- `Review` (0.5-0.7): Reviewing previous material

## Mathematical Formulas

### Mastery Calculation
Exponential learning curve validated from empirical data:

```
M = 1 - exp(-PracticeCount × QualityAvg / 10)
```

**Examples:**
- 5 practice × 0.8 quality ≈ 33% mastery
- 10 practice × 0.8 quality ≈ 55% mastery
- 20 practice × 0.9 quality ≈ 84% mastery

### Curvature Calculation
Multi-factor confusion metric:

```
LocalCurvature = BaseConfusion × (1 - Mastery) × RecencyFactor × ConnectionComplexity
```

**Factors:**
- `RecencyFactor`: 1.5 if never practiced, increases up to 1.5 over 365 days
- `ConnectionComplexity`: √(totalConnections) / 5, clamped to [0.5, 1.0]

### Ricci Flow Smoothing
Geometric optimization (calibrated flow rate: 0.1):

```
dC/dt = -flowRate × Curvature
```

Applied automatically after each learning event to smooth curvature landscape.

### Mastery Time Prediction
Formula with validated exponent (1.8 from Week 1 testing):

```
TimeMinutes = BaseConfusion × (1 - Mastery) × Curvature^1.8 × 60
```

### Mastery Decay
Exponential forgetting curve (30% decay per year default):

```
M_decayed = M × exp(-decayRate × days / 365)
```

## Usage Examples

### Complete Learning Scenario

```csharp
// 1. Create thought space
var thoughtSpace = await service.CreateThoughtSpaceAsync("user-123", "mathematics");

// 2. Add concepts with prerequisite relationships
var algebra = await service.AddConceptAsync(thoughtSpace.Id, "Algebra", "Basic algebra", 1.0);
var calculus = await service.AddConceptAsync(thoughtSpace.Id, "Calculus", "Differential calculus", 2.5);
await service.CreatePrerequisiteAsync(algebra.Id, calculus.Id, strength: 1.0);

// 3. Record learning events
for (int i = 0; i < 10; i++)
{
    algebra = await service.RecordLearningEventAsync(
        algebra.Id,
        LearningEventType.Practice,
        durationMinutes: 30
    );
}

// 4. Analyze progress
var analysis = await service.AnalyzeLearningProgressAsync(thoughtSpace.Id);
Console.WriteLine($"Average Mastery: {analysis.AverageMastery:P0}");
Console.WriteLine($"Global Curvature: {analysis.GlobalCurvature:F2}");
Console.WriteLine($"Learning Velocity: {analysis.LearningVelocity:F4}");

// 5. Get optimal learning path
var path = await service.GetOptimalLearningPathAsync(thoughtSpace.Id);
Console.WriteLine($"Next concept to study: {path[0].Name}");

// 6. Predict time to mastery
int minutesNeeded = await service.PredictTotalMasteryTimeAsync(thoughtSpace.Id, targetMastery: 0.8);
Console.WriteLine($"Estimated time: {minutesNeeded / 60} hours");
```

### Analysis Report Structure

```csharp
public class LearningAnalysis
{
    public int TotalConcepts { get; set; }
    public double AverageMastery { get; set; }
    public double AverageCurvature { get; set; }
    public double GlobalCurvature { get; set; }
    public double LearningVelocity { get; set; }
    public int TotalPracticeCount { get; set; }
    public int TotalMinutesSpent { get; set; }
    public List<Concept> StrugglingConcepts { get; set; }
    public List<Concept> MasteredConcepts { get; set; }
    public Concept? RecommendedNextConcept { get; set; }
}
```

## Architecture

### Services

**GeometricReasoningService** (orchestration)
- Coordinates repository, curvature, and mastery services
- Provides high-level operations (create, learn, analyze)
- Automatically updates global metrics after each learning event

**CurvatureCalculationService** (geometric calculations)
- Local curvature calculation
- Global curvature (weighted average)
- Ricci flow smoothing
- Mastery time prediction
- Learning velocity tracking

**MasteryCalculationService** (learning curves)
- Exponential mastery calculation
- Practice count prediction
- Quality score estimation (by event type + duration)
- Mastery decay (forgetting curve)

### Repository

**IGeometricReasoningRepository** (data access)
- CRUD operations for ThoughtSpace, Concept, ConceptConnection, LearningEvent
- Efficient queries with Include/ThenInclude for related entities
- In-memory database support for testing

### Entities

```
ThoughtSpace (manifold container)
├── Concepts (points in space)
│   ├── OutgoingConnections (to other concepts)
│   ├── IncomingConnections (from other concepts)
│   └── Events (learning history)
└── LearningHistory (all events across concepts)
```

## Database Schema

**4 Tables:**
- `ThoughtSpaces`: Manifold containers (global metrics)
- `Concepts`: Knowledge nodes (local curvature, mastery)
- `ConceptConnections`: Relationships (prerequisites, analogies, etc.)
- `LearningEvents`: Practice history (quality, duration, curvature changes)

**12 Indexes:**
- ThoughtSpaces: UserId, CreatedAt, GlobalCurvature
- Concepts: ThoughtSpaceId, MasteryLevel, LocalCurvature
- ConceptConnections: SourceConcept, TargetConcept, ConnectionType
- LearningEvents: ConceptId+OccurredAt, ThoughtSpaceId+OccurredAt, EventType

## Testing

**Test Coverage:**
- 36 unit tests (repository + algorithms) - 100% passing
- 8 integration tests (end-to-end scenarios) - 100% passing

**Run tests:**
```bash
dotnet test Hazina.Tests.Geometric --configuration Release
```

**Integration test scenarios:**
1. Complete learning scenario (multiple concepts + prerequisites)
2. Ricci flow smoothing validation
3. Optimal learning path ordering
4. Total mastery time prediction
5. Learning velocity tracking
6. Global curvature calculation
7. Mastery decay formula
8. Quality score estimation

## Performance

**Typical Operations:**
- Create concept: ~1-5ms
- Record learning event: ~10-30ms (includes Ricci flow)
- Analyze progress: ~20-100ms (depends on concept count)
- Predict mastery time: ~5-15ms

**Optimization:**
- Efficient EF Core queries with Include/ThenInclude
- Automatic global metric updates (no manual recalculation)
- In-memory database for testing (fast, isolated)

## Deployment

**Database Migration:**
```bash
dotnet ef migrations add InitialGeometric --context GeometricReasoningDbContext --project Hazina.Data
dotnet ef database update --context GeometricReasoningDbContext --project Hazina.Data
```

**Seed Data:**
Included in migration: 1 thought space (programming domain), 3 concepts, 2 prerequisite connections.

## Future Enhancements

1. **Automatic Decay Application** - Background job to apply time-based mastery decay
2. **Topological Sort** - Proper prerequisite chain ordering in GetOptimalLearningPath
3. **Adaptive Flow Rate** - Dynamic Ricci flow rate based on learning velocity
4. **Multi-Domain Transfer** - Cross-domain analogies and knowledge transfer
5. **Visualization** - Interactive 3D projection of thought space manifold

## References

- **Week 1 Validation Report** (52% curvature reduction, 72.8% consciousness score)
- **Ricci Flow** - Geometric smoothing analogous to gradient descent
- **Exponential Learning Curves** - M = 1 - exp(-practice × quality / 10)
- **Riemannian Manifolds** - Mathematical foundation for curvature-based learning

## License

Part of Hazina Framework - Cognitive consciousness infrastructure.

---

**Status:** Week 2 implementation complete
**Last Updated:** 2026-02-20
**Total Lines:** ~3,800 across entities, services, repository, tests
