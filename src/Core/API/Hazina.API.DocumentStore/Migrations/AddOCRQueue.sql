-- Migration: Add OCR Queue Table
-- Created: 2026-05-03
-- Description: Adds OCRQueue table for background OCR processing

CREATE TABLE IF NOT EXISTS OCRQueues (
    Id TEXT PRIMARY KEY,
    DocumentId TEXT NOT NULL,
    RAGStoreId TEXT NOT NULL,
    Status INTEGER NOT NULL,
    Priority INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    ProcessedAt TEXT,
    CompletedAt TEXT,
    ErrorMessage TEXT,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    MaxRetries INTEGER NOT NULL DEFAULT 3,
    ConfidenceScore REAL,
    ExtractedText TEXT,
    OriginalFilename TEXT,
    Language TEXT,
    ExtractedMetadata TEXT
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS IX_OCRQueues_Status ON OCRQueues(Status);
CREATE INDEX IF NOT EXISTS IX_OCRQueues_DocumentId ON OCRQueues(DocumentId);
CREATE INDEX IF NOT EXISTS IX_OCRQueues_RAGStoreId ON OCRQueues(RAGStoreId);
CREATE INDEX IF NOT EXISTS IX_OCRQueues_CreatedAt ON OCRQueues(CreatedAt);

-- Comments
-- Status values:
--   0 = Pending
--   1 = Processing
--   2 = Completed
--   3 = Failed
