# Hazina.Tools.Services.Database

Secure database tooling layer for AI agents with structured query model.

## Overview

This package provides secure database access for AI agents through:
- **Structured Query Model**: Type-safe query representation (no raw SQL from agents)
- **Schema Validation**: All queries validated against defined schemas
- **Parameterized SQL**: Injection-proof query compilation
- **Row-Level Security**: Multi-tenant restriction policies via subquery wrappers

## Components

### Core
- `StructuredQuery` - Query representation model
- `SqlCompiler` - Compiles queries to parameterized SQL
- `TableSchema`/`DatabaseSchema` - Schema definitions
- `RestrictionPolicy` - Row-level security policies

### Executors
- `IDbExecutor` - Database execution interface
- `SqliteExecutor` - SQLite implementation

### Tools
- `DatabaseToolFullAccess` - Owner-level unrestricted queries
- `DatabaseToolRestrictedAccess` - Context-scoped queries with silent failure

## Usage

```csharp
// Define schema
var schema = new DatabaseSchema()
    .AddTable(new TableSchema
    {
        Name = "posts",
        Columns = new List<ColumnSchema>
        {
            new() { Name = "id", DataType = "TEXT" },
            new() { Name = "content", DataType = "TEXT" },
            new() { Name = "project_id", DataType = "TEXT" }
        },
        ForbiddenColumns = new() { "internal_notes" }
    });

// Define restriction policy
var restriction = new RestrictionPolicy
{
    Name = "ProjectScope",
    RequiredPredicates = new List<RestrictionPredicate>
    {
        new() { Column = "project_id", Operator = "=", ValueSource = "context.ProjectId" }
    }
};

// Execute restricted query
var result = await databaseService.ExecuteRestrictedQueryAsync(
    query, dbPath, schema, restriction, context);
```

## Security Features

1. **No Raw SQL**: Agents provide structured queries, never SQL strings
2. **Column Whitelisting**: Only allowed columns can be selected/filtered
3. **Identifier Validation**: Table/column names validated (alphanumeric + underscore)
4. **Hard Limits**: Max 10,000 rows per query
5. **Silent Restrictions**: Returns empty results (not errors) for inaccessible data
