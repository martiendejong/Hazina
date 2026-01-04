-- Migration: Add ProjectId and ResponseMessage columns to llm_call_logs table
-- Date: 2026-01-04
-- Description: Adds project_id and response_message columns to improve log visibility

-- Add project_id column if it doesn't exist
-- Note: SQLite doesn't have ALTER TABLE IF NOT EXISTS, so we use a different approach
PRAGMA table_info(llm_call_logs);

-- Add columns (will fail silently if they already exist in production use)
-- For SQLite, we need to check if columns exist first

-- Step 1: Check if we need migration by trying to select from the column
-- If this fails, we need to add the columns

-- For SQLite migration, the safest approach is:
-- 1. Create new table with updated schema
-- 2. Copy data from old table
-- 3. Drop old table
-- 4. Rename new table

-- However, since CREATE TABLE IF NOT EXISTS already includes the new columns,
-- this migration is only needed for databases created before this change.

-- Manual migration steps for existing databases:
-- 1. Stop the application
-- 2. Backup the database file (llm_logs.db)
-- 3. Run these commands:

ALTER TABLE llm_call_logs ADD COLUMN project_id TEXT NULL;
ALTER TABLE llm_call_logs ADD COLUMN response_message TEXT NULL;

-- 4. Create index for project_id
CREATE INDEX IF NOT EXISTS idx_project_id ON llm_call_logs(project_id);

-- 5. Restart the application

-- Note: If columns already exist, this will fail. In that case, database is already migrated.
-- SQLite will error: "duplicate column name" which is safe to ignore.
