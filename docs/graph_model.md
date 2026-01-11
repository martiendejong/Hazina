# GraphRAG Knowledge Graph Model

**Version**: 1.0.0  
**Date**: 2026-01-11  
**Status**: Phase 1 Complete

## Overview

The Hazina GraphRAG knowledge graph model extends RAG with structured knowledge representation, enabling multi-hop reasoning, explicit knowledge representation, and explainable retrieval.

## Core Components

### GraphEntity (Nodes)
Entities are the primary units of knowledge (Person, Organization, Location, Concept, Event, Product, Document, Topic).

**Properties**: Id, Name, Type, Description, Properties (dict), Embedding, Aliases, SourceDocuments, Confidence, MentionCount

### GraphRelationship (Edges)
Directed connections between entities (WORKS_FOR, LOCATED_IN, CREATED_BY, etc.).

**Properties**: SourceEntityId, TargetEntityId, RelationType, Confidence, Weight, SourceText, Temporal

### GraphSchema (Ontology)
Defines valid entity/relationship types with permissive or strict validation modes.

### GraphPath (Multi-hop)
Sequences of entities connected by relationships for multi-hop queries.

## Storage Strategy

**Default**: SQLite (embedded, no dependencies)  
**Optional**: Neo4j (native graph DB), In-Memory (testing)

## Design Principles

1. **100% Backwards Compatible** - Opt-in, no breaking changes
2. **Pluggable Storage** - Abstract IGraphStore interface
3. **Flexible Schema** - Permissive and strict modes
4. **Provenance** - All entities/relationships trace to source documents
5. **Confidence Tracking** - All extractions have confidence scores

## Implementation Status

✅ Phase 1: Data Model Complete  
⏳ Phase 2-6: Pending (see GRAPHRAG_IMPLEMENTATION_PLAN.md)

For complete documentation, see GRAPHRAG_IMPLEMENTATION_PLAN.md
