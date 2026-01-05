-- Initialize Hazina database with pgvector extension

-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Create schema for Hazina
CREATE SCHEMA IF NOT EXISTS hazina;

-- Grant permissions
GRANT ALL ON SCHEMA hazina TO hazina;

-- Embeddings table
CREATE TABLE IF NOT EXISTS hazina.embeddings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    collection_name VARCHAR(255) NOT NULL,
    document_id VARCHAR(255) NOT NULL,
    chunk_id VARCHAR(255),
    embedding vector(1536),  -- Adjust dimension as needed (1536 for OpenAI, 768 for others)
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Create index for fast similarity search
CREATE INDEX IF NOT EXISTS embeddings_collection_idx ON hazina.embeddings(collection_name);
CREATE INDEX IF NOT EXISTS embeddings_document_idx ON hazina.embeddings(document_id);
CREATE INDEX IF NOT EXISTS embeddings_vector_idx ON hazina.embeddings
    USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 100);

-- Document chunks table
CREATE TABLE IF NOT EXISTS hazina.document_chunks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    document_id VARCHAR(255) NOT NULL,
    chunk_index INTEGER NOT NULL,
    chunk_text TEXT NOT NULL,
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(document_id, chunk_index)
);

CREATE INDEX IF NOT EXISTS document_chunks_document_idx ON hazina.document_chunks(document_id);

-- Document metadata table
CREATE TABLE IF NOT EXISTS hazina.document_metadata (
    document_id VARCHAR(255) PRIMARY KEY,
    title VARCHAR(500),
    source VARCHAR(500),
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Texts table for simple text storage
CREATE TABLE IF NOT EXISTS hazina.texts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    collection_name VARCHAR(255) NOT NULL,
    text TEXT NOT NULL,
    metadata JSONB,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS texts_collection_idx ON hazina.texts(collection_name);

-- Function to update updated_at timestamp
CREATE OR REPLACE FUNCTION hazina.update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers for updated_at
CREATE TRIGGER update_embeddings_updated_at BEFORE UPDATE ON hazina.embeddings
    FOR EACH ROW EXECUTE FUNCTION hazina.update_updated_at_column();

CREATE TRIGGER update_document_metadata_updated_at BEFORE UPDATE ON hazina.document_metadata
    FOR EACH ROW EXECUTE FUNCTION hazina.update_updated_at_column();

-- Grant permissions on tables
GRANT ALL ON ALL TABLES IN SCHEMA hazina TO hazina;
GRANT ALL ON ALL SEQUENCES IN SCHEMA hazina TO hazina;
GRANT ALL ON ALL FUNCTIONS IN SCHEMA hazina TO hazina;

-- Success message
DO $$
BEGIN
    RAISE NOTICE 'Hazina database initialized successfully with pgvector support';
END $$;
