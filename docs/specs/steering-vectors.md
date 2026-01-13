# Steering Vectors - Storage Format & Specifications

**Document Version:** 1.0.0
**Last Updated:** 2026-01-13
**Status:** Specification

---

## Overview

This document defines the **storage format**, **metadata schema**, **layer selection strategies**, and **best practices** for steering vectors in the Hazina framework.

### Purpose of Steering Vectors

Steering vectors are **learned representations** that modify LLM behavior at the activation level. Unlike prompts (which provide instructions), steering vectors directly manipulate the model's internal representations during inference.

**Key Characteristics:**
- **Layer-specific:** Different vectors for different transformer layers
- **Directional:** Represent a direction in activation space
- **Composable:** Can be combined with weighted coefficients
- **Transferable:** Can work across similar prompts/contexts
- **Model-specific:** Dimensionality must match target model

---

## Vector Data Model

### Core Structure

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "professional_tone",
  "category": "tone",
  "description": "Increases formality and professionalism in responses",
  "modelFamily": "gpt-2",
  "modelDimensionality": 768,
  "layerVectors": {
    "16": [0.123, -0.456, 0.789, ...],  // 768 floats
    "17": [0.234, -0.567, 0.890, ...],
    "18": [0.345, -0.678, 0.901, ...]
  },
  "recommendedCoefficient": {
    "min": 0.5,
    "max": 1.5
  },
  "defaultCoefficient": 1.0,
  "metadata": {
    "generationMethod": "contrastive",
    "positiveExamples": [...],
    "negativeExamples": [...],
    "evaluationMetrics": {...}
  },
  "createdAt": "2026-01-13T10:00:00Z",
  "updatedAt": "2026-01-13T10:00:00Z",
  "version": "1.0.0",
  "metrics": {
    "averageEffectiveness": 0.85,
    "consistencyScore": 0.92,
    "usageCount": 150,
    "averageLatencyImpact": 0.05
  }
}
```

### Field Specifications

#### Identity Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | UUID | Yes | Unique identifier (immutable) |
| `name` | String | Yes | Human-readable name (unique within category) |
| `category` | String | Yes | Organizational category (e.g., "tone", "domain", "persona") |
| `description` | String | No | Human-readable description of behavior modification |
| `version` | SemVer | Yes | Version string (e.g., "1.2.3") |

**Naming Conventions:**
- Use `snake_case` for names
- Max length: 100 characters
- Must be filesystem-safe (no `/`, `\`, `:`, etc.)
- Examples: `professional_tone`, `medical_expert`, `concise_style`

**Category Values:**
- `tone` - Tone/style modifications (professional, casual, formal)
- `domain` - Domain expertise (medical, legal, technical)
- `persona` - Personality traits (teacher, comedian, analyst)
- `format` - Output format preferences (concise, detailed, bullet-points)
- `safety` - Safety/ethical constraints (family-friendly, unbiased)
- `composite` - Blended vectors from multiple sources
- `custom` - User-defined categories

#### Model Compatibility Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `modelFamily` | String | Yes | Model family identifier (e.g., "gpt-2", "llama-2", "mistral") |
| `modelDimensionality` | Int | Yes | Hidden state dimensionality (e.g., 768, 4096) |

**Common Model Families and Dimensionalities:**

| Model Family | Dimensionality | Layers | Example Models |
|--------------|----------------|--------|----------------|
| `gpt-2` | 768 | 12 | gpt2, gpt2-medium |
| `gpt-2-large` | 1024 | 24 | gpt2-large |
| `gpt-2-xl` | 1600 | 48 | gpt2-xl |
| `llama-2-7b` | 4096 | 32 | Llama-2-7b, Llama-2-7b-chat |
| `llama-2-13b` | 5120 | 40 | Llama-2-13b |
| `mistral-7b` | 4096 | 32 | mistral-7b, mistral-7b-instruct |
| `phi-2` | 2560 | 32 | microsoft/phi-2 |

#### Vector Data Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `layerVectors` | Dict<Int, Float[]> | Yes | Layer index → activation vector mapping |

**Layer Vector Constraints:**
- All vectors must have `length == modelDimensionality`
- Layer indices must be valid for model (0 to num_layers-1)
- Vectors should be normalized (L2 norm ≈ 1.0)
- At least 1 layer required, typically 3-5 layers for effectiveness

**Example:**
```json
"layerVectors": {
  "16": [0.123, -0.456, 0.789, ...],  // Middle-layer (semantic)
  "17": [0.234, -0.567, 0.890, ...],  // Middle-layer (semantic)
  "18": [0.345, -0.678, 0.901, ...]   // Middle-layer (semantic)
}
```

#### Coefficient Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `recommendedCoefficient.min` | Float | No | Minimum recommended coefficient (default: 0.1) |
| `recommendedCoefficient.max` | Float | No | Maximum recommended coefficient (default: 2.0) |
| `defaultCoefficient` | Float | Yes | Default coefficient if not specified (typically 1.0) |

**Coefficient Guidelines:**
- **< 0.5:** Subtle steering
- **0.5 - 1.0:** Moderate steering
- **1.0 - 1.5:** Strong steering
- **> 1.5:** Very strong steering (may cause instability)

#### Metadata Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `metadata` | Object | No | Extensible metadata for generation, evaluation, etc. |
| `createdAt` | ISO 8601 | Yes | Creation timestamp |
| `updatedAt` | ISO 8601 | Yes | Last modification timestamp |

**Standard Metadata Keys:**
```json
"metadata": {
  // Generation metadata
  "generationMethod": "contrastive",  // "contrastive", "pca", "finetuned"
  "positiveExamples": ["example1", "example2"],
  "negativeExamples": ["example3", "example4"],
  "targetLayers": [16, 17, 18],

  // Evaluation metadata
  "evaluationMetrics": {
    "bleu": 0.75,
    "rouge": 0.82,
    "behaviorChangeScore": 0.88
  },

  // Source metadata
  "author": "user@example.com",
  "license": "MIT",
  "tags": ["professional", "business", "formal"],

  // Usage metadata
  "useCases": ["customer service", "business emails"],
  "warnings": ["May reduce creativity", "Best for formal contexts"]
}
```

#### Metrics Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `metrics` | Object | No | Performance metrics tracked during usage |

```json
"metrics": {
  "averageEffectiveness": 0.85,       // 0-1, how well it achieves goal
  "consistencyScore": 0.92,           // 0-1, consistency across prompts
  "usageCount": 150,                  // Number of times applied
  "averageLatencyImpact": 0.05,       // Average inference slowdown (5%)
  "userRatings": {
    "average": 4.2,                   // 1-5 stars
    "count": 45
  },
  "lastEvaluated": "2026-01-13T10:00:00Z"
}
```

---

## Storage Formats

### 1. JSON Format (Recommended for File Storage)

**Advantages:**
- Human-readable
- Easy to edit manually
- Version control friendly
- Wide tool support

**File Structure:**
```
VectorStore/
├── tone/
│   ├── professional_tone.json
│   ├── casual_tone.json
│   └── formal_tone.json
├── domain/
│   ├── medical_expert.json
│   └── legal_advisor.json
└── composite/
    └── professional_teacher.json
```

**Example File:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "name": "professional_tone",
  "category": "tone",
  "description": "Increases formality and professionalism",
  "modelFamily": "gpt-2",
  "modelDimensionality": 768,
  "layerVectors": {
    "16": [/* 768 floats */],
    "17": [/* 768 floats */],
    "18": [/* 768 floats */]
  },
  "recommendedCoefficient": {"min": 0.5, "max": 1.5},
  "defaultCoefficient": 1.0,
  "metadata": {
    "generationMethod": "contrastive",
    "tags": ["professional", "business"]
  },
  "createdAt": "2026-01-13T10:00:00Z",
  "updatedAt": "2026-01-13T10:00:00Z",
  "version": "1.0.0"
}
```

---

### 2. Binary Format (Recommended for Large Vectors)

**Advantages:**
- Compact (50-70% smaller)
- Fast deserialization
- Efficient for production

**Format: MessagePack**
- Schema: Same as JSON
- Encoding: MessagePack binary
- Extension: `.msgpack` or `.bin`

**Python Example:**
```python
import msgpack

# Write
with open("professional_tone.msgpack", "wb") as f:
    msgpack.pack(vector_dict, f)

# Read
with open("professional_tone.msgpack", "rb") as f:
    vector = msgpack.unpack(f)
```

**C# Example:**
```csharp
using MessagePack;

// Write
var bytes = MessagePackSerializer.Serialize(vector);
File.WriteAllBytes("professional_tone.msgpack", bytes);

// Read
var bytes = File.ReadAllBytes("professional_tone.msgpack");
var vector = MessagePackSerializer.Deserialize<SteeringVector>(bytes);
```

---

### 3. PostgreSQL Schema

**Table Definition:**
```sql
CREATE TABLE steering_vectors (
    -- Identity
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(100) NOT NULL,
    category VARCHAR(50) NOT NULL,
    description TEXT,
    version VARCHAR(20) NOT NULL DEFAULT '1.0.0',

    -- Model compatibility
    model_family VARCHAR(50) NOT NULL,
    model_dimensionality INTEGER NOT NULL,

    -- Vector data (JSONB for flexibility)
    layer_vectors JSONB NOT NULL,

    -- Coefficients
    recommended_coefficient_min DOUBLE PRECISION DEFAULT 0.1,
    recommended_coefficient_max DOUBLE PRECISION DEFAULT 2.0,
    default_coefficient DOUBLE PRECISION DEFAULT 1.0,

    -- Metadata
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),

    -- Soft delete
    is_deleted BOOLEAN DEFAULT FALSE,

    -- Metrics
    metrics JSONB DEFAULT '{}',

    -- Constraints
    CONSTRAINT unique_name_version UNIQUE (name, version),
    CONSTRAINT valid_coefficient_range
        CHECK (recommended_coefficient_min < recommended_coefficient_max)
);

-- Indexes
CREATE INDEX idx_steering_vectors_name ON steering_vectors(name);
CREATE INDEX idx_steering_vectors_category ON steering_vectors(category);
CREATE INDEX idx_steering_vectors_model_family ON steering_vectors(model_family);
CREATE INDEX idx_steering_vectors_created_at ON steering_vectors(created_at DESC);
CREATE INDEX idx_steering_vectors_metadata ON steering_vectors USING GIN(metadata);
CREATE INDEX idx_steering_vectors_active ON steering_vectors(is_deleted)
    WHERE is_deleted = FALSE;

-- Full-text search on name + description
CREATE INDEX idx_steering_vectors_search ON steering_vectors
    USING GIN(to_tsvector('english', name || ' ' || COALESCE(description, '')));
```

**Query Examples:**
```sql
-- Get latest version of a vector
SELECT * FROM steering_vectors
WHERE name = 'professional_tone'
  AND is_deleted = FALSE
ORDER BY created_at DESC
LIMIT 1;

-- List vectors by category
SELECT id, name, description, version, created_at
FROM steering_vectors
WHERE category = 'tone'
  AND is_deleted = FALSE
ORDER BY name;

-- Search vectors
SELECT id, name, description,
       ts_rank(to_tsvector('english', name || ' ' || COALESCE(description, '')),
               to_tsquery('english', 'professional & tone')) AS rank
FROM steering_vectors
WHERE to_tsvector('english', name || ' ' || COALESCE(description, ''))
      @@ to_tsquery('english', 'professional & tone')
  AND is_deleted = FALSE
ORDER BY rank DESC
LIMIT 10;

-- Get vector with metrics
SELECT
    sv.*,
    (sv.metrics->>'usageCount')::int AS usage_count,
    (sv.metrics->>'averageEffectiveness')::float AS effectiveness
FROM steering_vectors sv
WHERE name = 'professional_tone'
  AND is_deleted = FALSE
ORDER BY created_at DESC
LIMIT 1;
```

---

## Layer Selection Strategies

### Why Layer Selection Matters

Different transformer layers process different aspects of language:

| Layer Range | Processes | Steering Effect |
|-------------|-----------|----------------|
| **Early (0-25%)** | Syntax, tokens | Syntax, structure |
| **Middle (25-75%)** | Semantics, meaning | Content, tone, style |
| **Late (75-100%)** | Output preparation | Format, final polish |

**Best Practice:** Target **middle layers (30-70%)** for most behavioral steering.

---

### Strategy 1: Manual Layer Selection

**Approach:** Manually specify target layers

**Use When:**
- You know the model architecture
- Targeting specific layer ranges
- Fine-tuning vector performance

**Example:**
```json
{
  "name": "professional_tone",
  "modelFamily": "gpt-2",
  "layerVectors": {
    "4": [...],   // ~30% depth
    "5": [...],   // Middle layers
    "6": [...],
    "7": [...]
  }
}
```

**Guidelines by Model:**

| Model | Total Layers | Recommended Range | Example Layers |
|-------|--------------|-------------------|----------------|
| GPT-2 | 12 | 4-8 | [4, 5, 6, 7] |
| GPT-2 Large | 24 | 10-18 | [10, 12, 14, 16] |
| Llama-2 7B | 32 | 12-24 | [12, 16, 20, 24] |
| Mistral 7B | 32 | 12-24 | [12, 16, 20, 24] |

---

### Strategy 2: Automatic Layer Discovery

**Approach:** Analyze layer importance and select top-K

**Algorithm:**
```python
def discover_optimal_layers(model, positive_examples, negative_examples, k=5):
    layer_scores = {}

    for layer_idx in range(model.num_layers):
        # Extract activations for positives and negatives
        pos_acts = extract_activations(positive_examples, [layer_idx])
        neg_acts = extract_activations(negative_examples, [layer_idx])

        # Compute separation
        separation = compute_separation(pos_acts, neg_acts)
        layer_scores[layer_idx] = separation

    # Select top-K layers
    top_layers = sorted(layer_scores.items(), key=lambda x: x[1], reverse=True)[:k]
    return [layer for layer, score in top_layers]
```

**Separation Metrics:**
- **Euclidean distance** between mean activations
- **KL divergence** between distributions
- **Classification accuracy** (linear probe)

---

### Strategy 3: Range-Based Selection

**Approach:** Select contiguous range in middle layers

**Formula:**
```
start_layer = floor(num_layers * 0.3)
end_layer = floor(num_layers * 0.7)
target_layers = range(start_layer, end_layer, step=num_layers // 10)
```

**Example (Llama-2 7B, 32 layers):**
```
start_layer = 9   (32 * 0.3)
end_layer = 22    (32 * 0.7)
step = 3          (32 // 10)
target_layers = [9, 12, 15, 18, 21]
```

---

### Strategy 4: Adaptive Layer Selection

**Approach:** Select layers based on task type

**Task-Specific Recommendations:**

| Task | Target Layers (%) | Rationale |
|------|------------------|-----------|
| **Tone/Style** | 40-60% | Semantic processing |
| **Domain Expertise** | 30-70% | Broad semantic influence |
| **Format Control** | 60-90% | Output preparation |
| **Safety Constraints** | 20-80% | Broad coverage |
| **Persona** | 30-70% | Semantic + output |

---

## Vector Generation Methods

### Method 1: Contrastive Generation

**Best For:** Clear positive/negative examples

**Algorithm:**
```
1. Extract activations for positive examples
2. Extract activations for negative examples
3. Compute mean activations for each set
4. Subtract: steering_vector = mean(positive) - mean(negative)
5. Normalize: steering_vector = steering_vector / ||steering_vector||
```

**Example Configuration:**
```json
{
  "generationMethod": "contrastive",
  "positiveExamples": [
    "The data indicates a strong correlation between variables.",
    "Our analysis reveals significant trends in the market."
  ],
  "negativeExamples": [
    "Like, the data shows some stuff is related, ya know?",
    "I think maybe there's some trends or whatever in the market."
  ],
  "targetLayers": [12, 16, 20, 24]
}
```

**Metadata Schema:**
```json
{
  "generationMethod": "contrastive",
  "positiveExamples": ["..."],
  "negativeExamples": ["..."],
  "targetLayers": [12, 16, 20, 24],
  "normalization": "l2",
  "generatedAt": "2026-01-13T10:00:00Z"
}
```

---

### Method 2: PCA-Based Generation

**Best For:** Dimensionality reduction, finding principal directions

**Algorithm:**
```
1. Extract activations for all examples
2. Compute PCA on activations
3. Select top-K principal components
4. Use as steering vectors
```

**Example Metadata:**
```json
{
  "generationMethod": "pca",
  "examples": ["..."],
  "principalComponents": 3,
  "explainedVariance": [0.45, 0.25, 0.15],
  "targetLayers": [16, 20, 24]
}
```

---

### Method 3: Fine-Tuned Model Difference

**Best For:** Domain-specific models, adapter-based models

**Algorithm:**
```
1. Load base model weights
2. Load fine-tuned model weights
3. Compute difference: steering = fine_tuned - base
4. Extract layer-specific differences
```

**Example Metadata:**
```json
{
  "generationMethod": "finetuned",
  "baseModel": "gpt-2",
  "fineTunedModel": "gpt-2-medical",
  "extractedLayers": [16, 17, 18, 19, 20]
}
```

---

## Vector Normalization

### Why Normalize?

- **Consistent magnitude:** Ensures predictable steering strength
- **Composability:** Enables meaningful coefficient scaling
- **Stability:** Prevents extreme activations

### Normalization Methods

#### 1. L2 Normalization (Recommended)

**Formula:**
```
normalized = vector / ||vector||₂
where ||vector||₂ = sqrt(Σ vector[i]²)
```

**Properties:**
- Unit length (magnitude = 1.0)
- Preserves direction
- Most common in ML

**C# Example:**
```csharp
public float[] NormalizeL2(float[] vector)
{
    var magnitude = Math.Sqrt(vector.Sum(v => v * v));
    return vector.Select(v => v / magnitude).ToArray();
}
```

#### 2. L1 Normalization

**Formula:**
```
normalized = vector / ||vector||₁
where ||vector||₁ = Σ |vector[i]|
```

**Properties:**
- Sum of absolute values = 1.0
- More robust to outliers

#### 3. Min-Max Normalization

**Formula:**
```
normalized = (vector - min) / (max - min)
```

**Properties:**
- Values scaled to [0, 1]
- Preserves relative magnitudes

---

## Vector Composition (Blending)

### Weighted Sum Composition

**Formula:**
```
blended = (w₁ * v₁ + w₂ * v₂ + ... + wₙ * vₙ) / (w₁ + w₂ + ... + wₙ)
```

**Example:**
```json
{
  "name": "professional_teacher",
  "category": "composite",
  "metadata": {
    "composition": [
      {"vector": "professional_tone", "weight": 0.7},
      {"vector": "teacher_persona", "weight": 0.3}
    ]
  }
}
```

**Use Cases:**
- Blending persona traits (70% professional + 30% creative)
- Multi-domain expertise (50% medical + 50% technical)
- Fine-tuning behavior (80% base + 20% safety)

---

### PCA-Based Composition

**Approach:** Project multiple vectors onto principal components

**Algorithm:**
```
1. Stack vectors as rows in matrix M
2. Compute PCA on M
3. Project onto top-K components
4. Reconstruct as single blended vector
```

**Advantages:**
- Reduces redundancy
- Preserves maximum variance
- Compact representation

---

## Best Practices

### 1. Vector Naming

✅ **Good:**
- `professional_tone`
- `medical_expert_persona`
- `concise_style`
- `safety_family_friendly`

❌ **Bad:**
- `vector1`
- `test_vec`
- `my awesome vector!!!`
- `professional/tone` (filesystem unsafe)

---

### 2. Layer Selection

✅ **Best Practices:**
- Target middle layers (30-70% depth)
- Use 3-5 layers for good coverage
- Test multiple layer configurations
- Document layer selection rationale

❌ **Anti-patterns:**
- Steering only layer 0 (too early)
- Steering all layers (inefficient)
- Random layer selection

---

### 3. Coefficient Tuning

✅ **Guidelines:**
- Start with default coefficient 1.0
- Increment by 0.1-0.2 for tuning
- Test range: 0.5 to 1.5
- Document optimal range in metadata

❌ **Pitfalls:**
- Coefficients > 2.0 (instability)
- Negative coefficients (unless intentional reversal)
- Extreme coefficients without testing

---

### 4. Vector Validation

**Pre-save Checklist:**
- [ ] All layer vectors have correct dimensionality
- [ ] Vectors are normalized (L2 norm ≈ 1.0)
- [ ] Layer indices are valid for model
- [ ] Metadata includes generation method
- [ ] Recommended coefficients are reasonable (0.1-2.0)
- [ ] Name is unique and filesystem-safe

**Validation Code:**
```csharp
public ValidationResult ValidateVector(SteeringVector vector)
{
    var errors = new List<string>();

    // Check dimensionality
    foreach (var (layer, vec) in vector.LayerVectors)
    {
        if (vec.Length != vector.ModelDimensionality)
            errors.Add($"Layer {layer}: incorrect dimensionality");
    }

    // Check normalization
    foreach (var (layer, vec) in vector.LayerVectors)
    {
        var magnitude = Math.Sqrt(vec.Sum(v => v * v));
        if (Math.Abs(magnitude - 1.0) > 0.1)
            errors.Add($"Layer {layer}: not normalized (||v|| = {magnitude})");
    }

    // Check coefficients
    if (vector.RecommendedCoefficient.Min >= vector.RecommendedCoefficient.Max)
        errors.Add("Invalid coefficient range");

    return new ValidationResult
    {
        IsValid = errors.Count == 0,
        Errors = errors
    };
}
```

---

### 5. Performance Optimization

**Storage:**
- Use binary format (MessagePack) for large vectors
- Cache frequently-used vectors in memory
- Index vectors by category and model family

**Retrieval:**
- Lazy-load layer vectors (don't load all at once)
- Use vector versioning for rollback
- Implement LRU cache with configurable size

**Application:**
- Batch vector application for multiple requests
- Pre-compute blended vectors
- Profile steering overhead

---

## Versioning Strategy

### Semantic Versioning

**Format:** `MAJOR.MINOR.PATCH`

**Increment Rules:**
- **MAJOR:** Breaking changes (incompatible dimensionality, model family)
- **MINOR:** New layers added, metadata updates
- **PATCH:** Bug fixes, normalization corrections

**Example Progression:**
```
1.0.0 → Initial version
1.1.0 → Added layers 20-24
1.1.1 → Re-normalized vectors (bug fix)
2.0.0 → Changed from gpt-2 to gpt-2-large (breaking)
```

---

### Version Storage

**PostgreSQL Approach:**
- Each version is separate row
- Query for latest version by default
- Rollback by selecting older version

**File Approach:**
```
VectorStore/
└── tone/
    ├── professional_tone_v1.0.0.json
    ├── professional_tone_v1.1.0.json
    └── professional_tone.json  (symlink to latest)
```

---

## Security & Privacy

### Sensitive Vectors

**Warning:** Vectors can encode biases from training examples

**Best Practices:**
- Review example sets for bias
- Test vectors on diverse inputs
- Document known limitations
- Include warnings in metadata

**Metadata Example:**
```json
{
  "metadata": {
    "warnings": [
      "May reduce creativity in technical contexts",
      "Trained primarily on business emails"
    ],
    "limitations": [
      "Less effective on conversational prompts",
      "Not evaluated for code generation"
    ]
  }
}
```

---

### Access Control

**Vector Sharing Levels:**
- **Private:** User-only access
- **Shared:** Team/organization access
- **Public:** Community marketplace

**Metadata Schema:**
```json
{
  "metadata": {
    "access": {
      "level": "shared",
      "owner": "user@example.com",
      "sharedWith": ["team-id-123"],
      "publicMarketplace": false
    },
    "license": "MIT"
  }
}
```

---

## Evaluation & Metrics

### Effectiveness Metrics

**1. Behavior Change Score**
```
BCS = similarity(steered_output, target_behavior) -
      similarity(baseline_output, target_behavior)
```

**2. Consistency Score**
```
CS = 1 - std_dev(effectiveness_across_prompts) / mean(effectiveness)
```

**3. Latency Impact**
```
LI = (steered_latency - baseline_latency) / baseline_latency
```

**Metadata Example:**
```json
{
  "metrics": {
    "averageEffectiveness": 0.85,
    "consistencyScore": 0.92,
    "averageLatencyImpact": 0.05,
    "evaluationDetails": {
      "testExamples": 50,
      "successRate": 0.88,
      "averageBCS": 0.42,
      "evaluatedAt": "2026-01-13T10:00:00Z"
    }
  }
}
```

---

## Example Vectors

### Example 1: Professional Tone

```json
{
  "id": "prof-tone-001",
  "name": "professional_tone",
  "category": "tone",
  "description": "Increases formality, removes casual language, adds business terminology",
  "modelFamily": "gpt-2",
  "modelDimensionality": 768,
  "layerVectors": {
    "4": [0.023, -0.156, 0.089, /* ... 765 more */],
    "5": [0.034, -0.067, 0.120, /* ... */],
    "6": [0.045, -0.178, 0.101, /* ... */],
    "7": [0.056, -0.089, 0.132, /* ... */]
  },
  "recommendedCoefficient": {"min": 0.6, "max": 1.4},
  "defaultCoefficient": 1.0,
  "metadata": {
    "generationMethod": "contrastive",
    "positiveExamples": [
      "Our analysis indicates a strong correlation.",
      "The data suggests significant market trends."
    ],
    "negativeExamples": [
      "Like, the data shows some stuff is related.",
      "I think there's maybe some trends in the market."
    ],
    "tags": ["professional", "business", "formal"],
    "useCases": ["business emails", "reports", "presentations"]
  },
  "version": "1.0.0",
  "createdAt": "2026-01-13T10:00:00Z",
  "updatedAt": "2026-01-13T10:00:00Z"
}
```

---

### Example 2: Medical Expert Persona

```json
{
  "id": "med-expert-001",
  "name": "medical_expert_persona",
  "category": "persona",
  "description": "Adds medical expertise, uses clinical terminology, maintains patient-focused tone",
  "modelFamily": "llama-2-7b",
  "modelDimensionality": 4096,
  "layerVectors": {
    "12": [/* 4096 floats */],
    "16": [/* 4096 floats */],
    "20": [/* 4096 floats */],
    "24": [/* 4096 floats */]
  },
  "recommendedCoefficient": {"min": 0.8, "max": 1.6},
  "defaultCoefficient": 1.2,
  "metadata": {
    "generationMethod": "contrastive",
    "domain": "medical",
    "tags": ["medical", "healthcare", "clinical"],
    "warnings": [
      "Not a replacement for professional medical advice",
      "Should be reviewed by licensed healthcare professionals"
    ],
    "useCases": ["patient education", "medical documentation"]
  },
  "version": "1.0.0"
}
```

---

### Example 3: Composite Vector

```json
{
  "id": "prof-teacher-001",
  "name": "professional_teacher",
  "category": "composite",
  "description": "Blend of professional tone (70%) and teacher persona (30%)",
  "modelFamily": "gpt-2",
  "modelDimensionality": 768,
  "layerVectors": {
    "4": [/* blended vector */],
    "5": [/* blended vector */],
    "6": [/* blended vector */]
  },
  "recommendedCoefficient": {"min": 0.7, "max": 1.3},
  "defaultCoefficient": 1.0,
  "metadata": {
    "composition": [
      {
        "vector": "professional_tone",
        "vectorId": "prof-tone-001",
        "weight": 0.7
      },
      {
        "vector": "teacher_persona",
        "vectorId": "teacher-001",
        "weight": 0.3
      }
    ],
    "blendingMethod": "weighted_average",
    "tags": ["professional", "educational", "composite"]
  },
  "version": "1.0.0"
}
```

---

## Migration Guide

### Upgrading Vector Formats

**From v1.0 to v2.0 (example):**

```csharp
public SteeringVector MigrateV1ToV2(SteeringVectorV1 oldVector)
{
    return new SteeringVector
    {
        Id = oldVector.Id,
        Name = oldVector.Name,
        Category = oldVector.Category ?? "general",  // NEW: required
        Description = oldVector.Description,
        ModelFamily = oldVector.ModelFamily,
        ModelDimensionality = oldVector.ModelDimensionality,
        LayerVectors = oldVector.LayerVectors,
        RecommendedCoefficient = oldVector.RecommendedCoefficient
            ?? (Min: 0.5, Max: 1.5),  // NEW: default if missing
        DefaultCoefficient = oldVector.DefaultCoefficient,
        Metadata = oldVector.Metadata ?? new(),
        Version = "2.0.0",
        CreatedAt = oldVector.CreatedAt,
        UpdatedAt = DateTime.UtcNow
    };
}
```

---

## Appendix A: File Format Reference

### JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SteeringVector",
  "type": "object",
  "required": ["id", "name", "category", "modelFamily", "modelDimensionality", "layerVectors", "defaultCoefficient", "version"],
  "properties": {
    "id": {"type": "string", "format": "uuid"},
    "name": {"type": "string", "maxLength": 100},
    "category": {"type": "string", "enum": ["tone", "domain", "persona", "format", "safety", "composite", "custom"]},
    "description": {"type": "string"},
    "modelFamily": {"type": "string"},
    "modelDimensionality": {"type": "integer", "minimum": 1},
    "layerVectors": {
      "type": "object",
      "patternProperties": {
        "^[0-9]+$": {
          "type": "array",
          "items": {"type": "number"}
        }
      }
    },
    "recommendedCoefficient": {
      "type": "object",
      "properties": {
        "min": {"type": "number"},
        "max": {"type": "number"}
      }
    },
    "defaultCoefficient": {"type": "number"},
    "metadata": {"type": "object"},
    "createdAt": {"type": "string", "format": "date-time"},
    "updatedAt": {"type": "string", "format": "date-time"},
    "version": {"type": "string", "pattern": "^\\d+\\.\\d+\\.\\d+$"},
    "metrics": {"type": "object"}
  }
}
```

---

## Appendix B: Performance Benchmarks

### Storage Performance

| Operation | File (JSON) | File (MessagePack) | PostgreSQL | Supabase |
|-----------|-------------|-------------------|------------|----------|
| Save | 15ms | 8ms | 25ms | 45ms |
| Load | 12ms | 5ms | 20ms | 40ms |
| Search | 150ms | 120ms | 35ms | 60ms |
| List (100) | 200ms | 150ms | 40ms | 80ms |

*Benchmarked on: Intel i7, 16GB RAM, SSD, PostgreSQL 15*

---

## Appendix C: Tool Support

### Recommended Tools

**Vector Visualization:**
- `numpy`, `matplotlib` (Python) - Activation heatmaps
- `plotly` - Interactive 3D projections

**Vector Analysis:**
- `scikit-learn` - PCA, clustering
- `scipy` - Statistical analysis

**Format Conversion:**
- `msgpack` - Binary serialization
- `json` - Human-readable format

---

**Document Status:** Complete
**Next Review:** TBD
**Feedback:** hazina-steering@example.com
