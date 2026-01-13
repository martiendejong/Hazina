# Hazina.API.Search - Cognitive Search REST API

**Phase 1 Implementation** - REST API for Hazina Cognitive Search Platform

## Overview

This project implements a production-ready REST API for the Hazina Cognitive Search platform, providing endpoints for:

- **Search Operations**: Natural language query, semantic search, hybrid search (vector + graph)
- **Document Management**: Upload, retrieve, update, delete, and find similar documents
- **Authentication**: JWT-based authentication with role-based authorization
- **Security**: Rate limiting, CORS, security headers, exception handling

## Features Implemented

### ✅ Task 1.1: Project Setup & Infrastructure
- ASP.NET Core 9.0 project
- Swagger/OpenAPI documentation
- Serilog structured logging
- Dependency injection
- CORS configuration
- Health check endpoints

### ✅ Task 1.2: Core Search Endpoints
- `POST /api/v1/search/query` - Natural language search
- `POST /api/v1/search/semantic` - Vector similarity search
- `POST /api/v1/search/hybrid` - Combined vector + graph search
- Request/response DTOs with validation
- Error handling and logging

### ✅ Task 1.3: Document Endpoints
- `GET /api/v1/documents` - List documents with pagination and filtering
- `GET /api/v1/documents/{id}` - Get document by ID
- `POST /api/v1/documents/upload` - Upload document (multipart/form-data)
- `PUT /api/v1/documents/{id}` - Update document metadata
- `DELETE /api/v1/documents/{id}` - Soft delete document
- `GET /api/v1/documents/{id}/similar` - Find similar documents

### ✅ Task 1.4: Authentication & Security
- JWT Bearer token authentication
- Role-based authorization (Admin, User)
- Rate limiting (100 req/min for search, 20 req/min for uploads)
- Exception handling middleware (RFC 7807 Problem Details)
- Security headers (X-Frame-Options, X-Content-Type-Options, etc.)
- HTTPS and HSTS support

## Quick Start

### Prerequisites
- .NET 9.0 SDK
- PostgreSQL with pgvector (for production)
- Redis (optional, for caching)

### Running the API

```bash
cd apps/Web/Hazina.API.Search
dotnet run
```

The API will start on `https://localhost:5001`

### Accessing Swagger UI

Navigate to: `https://localhost:5001/swagger`

### Authentication

1. Get a JWT token:
```bash
curl -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"any"}'
```

2. Use the token in subsequent requests:
```bash
curl -X POST https://localhost:5001/api/v1/search/query \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query":"What is OAuth2?","topK":10}'
```

## API Endpoints

### Health Check
- `GET /health` - API health status

### Authentication
- `POST /api/v1/auth/login` - Get JWT token

### Search
- `POST /api/v1/search/query` - Natural language search
- `POST /api/v1/search/semantic` - Semantic vector search
- `POST /api/v1/search/hybrid` - Hybrid search (vector + graph)

### Documents
- `GET /api/v1/documents` - List documents (paginated)
- `GET /api/v1/documents/{id}` - Get document
- `POST /api/v1/documents/upload` - Upload document
- `PUT /api/v1/documents/{id}` - Update document
- `DELETE /api/v1/documents/{id}` - Delete document
- `GET /api/v1/documents/{id}/similar` - Find similar documents

## Configuration

Edit `appsettings.json`:

```json
{
  "Jwt": {
    "Key": "your-secret-key-here",
    "Issuer": "Hazina.API.Search",
    "Audience": "Hazina.API.Search",
    "ExpiryMinutes": 60
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=hazina;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  }
}
```

## Rate Limits

- **Search queries**: 100 requests per minute
- **Document uploads**: 20 requests per minute
- **Queue limit**: 10 pending requests

## Security

- JWT authentication required for all endpoints (except `/health` and `/auth/login`)
- Role-based authorization:
  - `User` role: Read access to search and documents
  - `Admin` role: Full access including upload, update, delete
- File upload size limit: 100 MB
- Security headers enabled
- HTTPS enforced in production

## Next Steps (Future Phases)

### Phase 2: NLP Enhancements
- Key phrase extraction
- Sentiment analysis
- Advanced OCR integration

### Phase 3: GraphQL API
- GraphQL schema and resolvers
- Real-time subscriptions via WebSocket

### Phase 4: Advanced Features
- Search analytics
- Performance optimization (Redis caching)
- Docker and Kubernetes deployment

## Development Notes

**Current Status**: Phase 1 COMPLETE (mock implementation)

The API endpoints are functional with mock data. Integration with actual Hazina services (DocumentStore, EmbeddingStore, RAGEngine) is pending and marked with `// TODO` comments in the code.

**To integrate real services**:
1. Uncomment service registrations in `ServiceCollectionExtensions.cs`
2. Replace mock responses in controllers with actual service calls
3. Configure connection strings for PostgreSQL and Redis

## Project Structure

```
Hazina.API.Search/
├── Controllers/           # API controllers
│   ├── AuthController.cs
│   ├── SearchController.cs
│   └── DocumentsController.cs
├── Models/               # DTOs and request/response models
│   ├── SearchRequest.cs
│   └── DocumentModels.cs
├── Middleware/           # Custom middleware
│   └── ExceptionHandlingMiddleware.cs
├── Extensions/           # Service extensions
│   └── ServiceCollectionExtensions.cs
├── Services/             # Business logic services
├── Program.cs            # Application entry point
├── appsettings.json      # Configuration
└── README.md            # This file
```

## License

Part of the Hazina Framework project.
