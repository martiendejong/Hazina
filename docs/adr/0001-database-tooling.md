# ADR-0001: Database Tooling Architecture

**Status:** Accepted
**Date:** 2026-01-03
**Context:** Hazina agent framework database access patterns

## Context

Hazina agents need to query databases for various use cases:
1. **Project-local databases** - SQLite databases containing imported data (social media, analytics)
2. **Multi-tenant databases** - Shared databases where data from multiple projects/users coexists

After analyzing VeraAI's implementation, we identified critical security gaps that must be addressed:
- No parameterized queries (SQL injection vulnerability)
- String concatenation for SQL construction
- Reliance on AI following instructions to not bypass restrictions
- No query validation or complexity limits

## Decision

We will implement **two database tools** with shared components:

### 1. DatabaseToolFullAccess (Project-Local)
- For project-scoped SQLite databases
- No multi-tenant restrictions needed
- Still uses parameterized queries via structured query compiler
- Safe limits on result size

### 2. DatabaseToolRestrictedAccess (Multi-Tenant Safe)
- For databases containing data from multiple projects/users
- **Mandatory restriction predicates** applied at the query root level
- Restrictions cannot be bypassed by the agent
- Silent restriction (returns empty results, not errors) to prevent data leakage

### Key Design Principles

#### 1. Structured Query Model (No Raw SQL)

Agents cannot provide raw SQL strings. Instead, they provide structured query objects:

```csharp
var query = new StructuredQuery
{
    Table = "posts",
    Select = new[] { "id", "content", "created_at" },
    Where = new[]
    {
        new WhereClause { Column = "status", Operator = "=", Value = "published" }
    },
    OrderBy = new[] { new OrderByClause { Column = "created_at", Descending = true } },
    Limit = 100
};
```

The `SqlCompiler` converts this to parameterized SQL:
```sql
SELECT id, content, created_at FROM posts WHERE status = @p0 ORDER BY created_at DESC LIMIT 100
```

#### 2. Restriction Policy (Multi-Tenant)

For restricted access, a `RestrictionPolicy` is applied:

```csharp
var policy = new RestrictionPolicy
{
    RequiredPredicates = new[]
    {
        new RestrictionPredicate { Column = "project_id", Operator = "=", ValueSource = "context.ProjectId" }
    }
};
```

The restriction is applied as a **subquery wrapper**, making it structurally impossible to bypass:

```sql
SELECT * FROM (
    SELECT id, content, created_at FROM posts
    WHERE project_id = @restriction_project_id  -- ALWAYS applied
) AS restricted_data
WHERE status = @p0  -- Agent's filter applied to restricted subset
ORDER BY created_at DESC LIMIT 100
```

#### 3. Silent Restriction

When restrictions yield no results, the tool returns an empty result set, not an error.
This prevents information leakage:
- Agent cannot determine if data exists but is restricted
- Agent cannot probe for valid project IDs or user IDs

#### 4. Column Whitelisting

Tables define allowed columns. The agent cannot SELECT columns outside the whitelist:

```csharp
var schema = new TableSchema
{
    Name = "posts",
    AllowedColumns = new[] { "id", "content", "created_at", "like_count" },
    ForbiddenColumns = new[] { "user_email", "api_token" }  // Never exposed
};
```

#### 5. Query Validation

Before execution, queries are validated:
- Table must exist in schema
- Columns must be in whitelist
- Operators must be in allowed set (no `DROP`, `DELETE`, etc.)
- Result limits enforced (max 10,000 rows)
- Query complexity limits (max joins, max where clauses)

## Comparison with VeraAI

| Aspect | VeraAI | Hazina (New) |
|--------|--------|--------------|
| Query Construction | String concatenation | Structured query model |
| Parameterization | None | Always parameterized |
| SQL Injection Risk | **CRITICAL** | Eliminated by design |
| Multi-tenant Filtering | Subquery wrapper (good) | Subquery wrapper + policy config |
| Column Access | Unrestricted | Whitelist-based |
| Restriction Bypass | Possible via agent manipulation | Structurally impossible |
| Silent Restriction | Yes (good) | Yes |
| Query Validation | None | Comprehensive |
| Credential Management | Hardcoded path | ISecretProvider abstraction |

## Why Two Tools?

### Full Access Tool
- **Use case:** Project-local SQLite databases (social imports, analytics)
- **Trust model:** Data belongs entirely to the project; no multi-tenant concerns
- **Security:** Still uses structured queries and parameterization
- **Restrictions:** None needed; the database itself is project-scoped

### Restricted Access Tool
- **Use case:** Shared databases, future analytics warehouse, multi-project queries
- **Trust model:** Zero trust; assume agent tries to access unauthorized data
- **Security:** Mandatory restriction predicates + structured queries
- **Restrictions:** Cannot be disabled; enforced at query compilation level

## Implementation Components

```
Hazina.Tools.Services.Database/
├── Core/
│   ├── StructuredQuery.cs          # Query model (SELECT, WHERE, ORDER BY, etc.)
│   ├── SqlCompiler.cs              # Compiles structured queries to parameterized SQL
│   ├── RestrictionPolicy.cs        # Defines mandatory predicates
│   └── TableSchema.cs              # Column whitelists, validation rules
├── Executors/
│   ├── IDbExecutor.cs              # Interface for database execution
│   ├── SqliteExecutor.cs           # SQLite implementation
│   └── PostgresExecutor.cs         # Future: PostgreSQL implementation
├── Tools/
│   ├── DatabaseToolFullAccess.cs   # AgentTool for project-local DBs
│   └── DatabaseToolRestrictedAccess.cs  # AgentTool for multi-tenant DBs
└── Security/
    ├── QueryValidator.cs           # Validates queries before execution
    └── QuerySanitizer.cs           # Additional input sanitization
```

## Consequences

### Positive
- Eliminates SQL injection vulnerabilities
- Enables safe agent access to databases
- Supports multiple database backends
- Future-proof for multi-tenant scenarios
- Clear separation between trusted (project-local) and untrusted (shared) data

### Negative
- More complex than raw SQL
- Agents limited to supported query patterns
- Some advanced SQL features not exposed (CTEs, window functions initially)

### Neutral
- Requires schema definition for each table
- Agents must learn structured query format

## Related Decisions

- ADR-0002: Social Provider Import Framework (uses DatabaseToolFullAccess)
- ADR-0003: Knowledge Database Architecture (uses DatabaseToolRestrictedAccess)

## References

- VeraAI BigQueryContext.cs - analyzed for patterns
- OWASP SQL Injection Prevention Cheat Sheet
- Hazina EmbeddingStore architecture (factory pattern, interface abstraction)
