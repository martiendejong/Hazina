# Failure Modes - Storage Layer

## Quick Reference

| Failure | Severity | Auto-Recovery | Manual Action |
|---------|----------|---------------|---------------|
| DB connection lost | CRITICAL | Yes (retry) | Check DB |
| Disk full | CRITICAL | No | Free space |
| Corrupted index | DEGRADED | No | Rebuild |
| Migration fails | CRITICAL | No | Fix & retry |
| Embedding gen fails | DEGRADED | Yes (skip) | Retry item |
| Query timeout | DEGRADED | No | Optimize |

---

## Database Failures

### Connection Lost
```
Severity: CRITICAL
Symptoms: SqlException, NpgsqlException
Impact: Cannot read or write data

Recovery:
1. [AUTO] Connection pool retries
2. [AUTO] Exponential backoff
3. [MANUAL] Check database server status
4. [MANUAL] Verify connection string
5. [MANUAL] Check network to DB

Prevention:
- Use connection pooling
- Configure retry policies
- Monitor DB availability
- Use managed DB service
```

### SQLite File Locked
```
Severity: CRITICAL
Symptoms: SQLiteException "database is locked"
Impact: Cannot write to database

Recovery:
1. [MANUAL] Check for other processes using DB
2. [MANUAL] Ensure single-writer access
3. [MANUAL] Restart application
4. [MANUAL] Check for zombie processes

Prevention:
- Use WAL mode for SQLite
- Ensure proper connection disposal
- Don't share SQLite across processes
```

### PostgreSQL Connection Exhausted
```
Severity: CRITICAL
Symptoms: "too many connections" error
Impact: New requests fail

Recovery:
1. [MANUAL] Check connection pool settings
2. [MANUAL] Identify connection leaks
3. [MANUAL] Restart application
4. [MANUAL] Increase max_connections

Prevention:
- Configure pool size properly
- Always dispose connections
- Monitor active connections
```

---

## Storage Failures

### Disk Full
```
Severity: CRITICAL
Symptoms: IOException "No space left on device"
Impact: Cannot write new data

Recovery:
1. [MANUAL] Check disk usage
2. [MANUAL] Delete old data/logs
3. [MANUAL] Expand storage
4. [MANUAL] Move to larger volume

Prevention:
- Monitor disk usage
- Alert at 80% capacity
- Implement data retention policies
- Use cloud storage with auto-scaling
```

### Embedding File Corrupted
```
Severity: DEGRADED
Symptoms: JsonException on load
Impact: Lost embeddings, need regeneration

Recovery:
1. [MANUAL] Restore from backup
2. [MANUAL] Or regenerate: await store.UpdateEmbeddings()
3. [MANUAL] Delete corrupted file if needed

Prevention:
- Regular backups
- Use database storage for production
- Validate JSON on write
```

### Index Corruption
```
Severity: DEGRADED
Symptoms: Slow queries, missing results
Impact: Search quality degraded

Recovery:
1. [MANUAL] Rebuild FTS index:
   - SQLite: DROP TABLE items_fts; recreate
   - PostgreSQL: REINDEX INDEX embeddings_idx;
2. [MANUAL] Vacuum database

Prevention:
- Regular VACUUM/maintenance
- Monitor query performance
- Use checksums for validation
```

---

## Operation Failures

### Embedding Generation Fails
```
Severity: DEGRADED
Symptoms: Embedding API error
Impact: Document not fully indexed

Recovery:
1. [AUTO] Document marked for retry
2. [AUTO] Continue with other documents
3. [MANUAL] Retry failed documents later
4. [MANUAL] Check embedding API status

Prevention:
- Implement retry queue
- Store raw text for regeneration
- Use checksum to track state
```

### Migration Fails Midway
```
Severity: CRITICAL
Symptoms: MigrationException
Impact: Partial data in new store

Recovery:
1. [MANUAL] Check migration logs
2. [MANUAL] Identify failed documents
3. [MANUAL] Fix source data issues
4. [MANUAL] Re-run migration (idempotent)

Prevention:
- Use dry-run mode first
- Implement checkpoints
- Keep source data intact
- Validate after migration
```

### Query Timeout
```
Severity: DEGRADED
Symptoms: Query takes > 30s
Impact: Slow user experience

Recovery:
1. [MANUAL] Optimize query
2. [MANUAL] Add missing indexes
3. [MANUAL] Reduce result set size
4. [MANUAL] Check for table locks

Prevention:
- Create appropriate indexes
- Use pagination
- Monitor query performance
- Set query timeouts
```

---

## Data Integrity Failures

### Checksum Mismatch
```
Severity: DEGRADED
Symptoms: Stored checksum != computed
Impact: Stale embeddings

Recovery:
1. [AUTO] Regenerate embedding for item
2. [MANUAL] Investigate source of change

Prevention:
- Always update checksum on content change
- Validate checksums periodically
```

### Orphaned Chunks
```
Severity: DEGRADED
Symptoms: Chunks without parent document
Impact: Wasted storage, potential confusion

Recovery:
1. [MANUAL] Run cleanup:
   DELETE FROM chunks WHERE parent_id NOT IN (SELECT id FROM items)
2. [MANUAL] Investigate how orphans occurred

Prevention:
- Use cascading deletes
- Implement referential integrity
- Run periodic cleanup jobs
```

### Missing Embeddings
```
Severity: DEGRADED
Symptoms: Search returns no results for known content
Impact: Content not discoverable

Recovery:
1. [MANUAL] Check embedding store for gaps
2. [MANUAL] Regenerate: await store.UpdateEmbeddings()
3. [MANUAL] Re-index specific documents

Prevention:
- Validate indexing completed
- Monitor embedding counts
- Implement consistency checks
```

---

## Backend-Specific Issues

### SQLite: WAL Checkpoint
```
Severity: DEGRADED
Symptoms: WAL file growing large
Impact: Increased disk usage

Recovery:
1. [MANUAL] Force checkpoint:
   PRAGMA wal_checkpoint(TRUNCATE);

Prevention:
- Configure auto-checkpoint
- Monitor WAL size
```

### PostgreSQL: Bloat
```
Severity: DEGRADED
Symptoms: Table/index size growing, slow queries
Impact: Performance degradation

Recovery:
1. [MANUAL] VACUUM FULL table_name;
2. [MANUAL] REINDEX TABLE table_name;

Prevention:
- Configure autovacuum
- Monitor bloat metrics
```

### Supabase: Rate Limits
```
Severity: DEGRADED
Symptoms: 429 from Supabase API
Impact: Operations throttled

Recovery:
1. [AUTO] Retry with backoff
2. [MANUAL] Check Supabase dashboard

Prevention:
- Use connection pooling
- Batch operations
- Monitor usage
```

---

## Monitoring Checklist

### Metrics to Watch
- [ ] Database connection pool usage
- [ ] Query latency (P50, P95, P99)
- [ ] Disk usage percentage
- [ ] Embedding count vs document count
- [ ] Failed operations rate

### Alerts to Configure
- [ ] Disk > 80% full
- [ ] DB connections > 80% pool
- [ ] Query latency > 5s
- [ ] Error rate > 1%
- [ ] Connection failures
