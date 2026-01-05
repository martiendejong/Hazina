# Hazina Deployment Guide

Production-ready deployment guide for Hazina AI Framework with security hardening, observability, and Docker support.

## Table of Contents

- [Quick Start](#quick-start)
- [Prerequisites](#prerequisites)
- [Local Development](#local-development)
- [Docker Deployment](#docker-deployment)
- [Production Deployment](#production-deployment)
- [Observability](#observability)
- [Security](#security)
- [Troubleshooting](#troubleshooting)

## Quick Start

```bash
# 1. Clone and setup
git clone https://github.com/your-org/hazina.git
cd hazina
cp .env.example .env

# 2. Configure secrets (edit .env file)
# Add your API keys: OPENAI_API_KEY, ANTHROPIC_API_KEY, etc.

# 3. Start with Docker Compose
docker-compose up -d

# 4. Access services
# - Grafana: http://localhost:3000 (admin/admin)
# - Jaeger UI: http://localhost:16686
# - Prometheus: http://localhost:9090
# - PostgreSQL: localhost:5432
```

## Prerequisites

### Required Software

- .NET 9.0 SDK or later
- Docker 24.0+ and Docker Compose 2.0+
- PostgreSQL 16+ (if not using Docker)
- Git

### API Keys

Obtain API keys from:
- [OpenAI](https://platform.openai.com/api-keys)
- [Anthropic](https://console.anthropic.com/)
- [Google AI Studio](https://makersuite.google.com/app/apikey) (optional)
- [Mistral](https://console.mistral.ai/) (optional)

### Environment Setup

```bash
# Copy environment template
cp .env.example .env

# Edit .env and configure:
# - Database passwords
# - API keys
# - Service endpoints
```

## Local Development

### Without Docker

```bash
# Restore and build
dotnet restore Hazina.sln
dotnet build Hazina.sln --configuration Release

# Run tests
dotnet test Hazina.sln --configuration Release

# Run specific application
cd apps/CLI/Hazina.App.ClaudeCode
dotnet run
```

### With Docker Compose (Recommended)

```bash
# Start infrastructure only (no apps)
docker-compose up -d postgres redis jaeger prometheus grafana

# Your app connects to:
# - PostgreSQL: localhost:5432
# - Redis: localhost:6379
# - Jaeger OTLP: localhost:4317
# - Prometheus: localhost:9090
```

## Docker Deployment

### Building Docker Images

```bash
# Build CLI application
docker build \
  --build-arg PROJECT_PATH=apps/CLI/Hazina.App.ClaudeCode \
  --build-arg PROJECT_NAME=Hazina.App.ClaudeCode \
  -t hazina-claude-code:latest \
  .

# Build Web application
docker build \
  --build-arg PROJECT_PATH=apps/Web/Hazina.App.HtmlMockupGenerator \
  --build-arg PROJECT_NAME=Hazina.App.HtmlMockupGenerator \
  -t hazina-web:latest \
  .
```

### Running with Docker Compose

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down

# Stop and remove volumes (clean slate)
docker-compose down -v
```

### Custom Docker Compose

Uncomment and configure the Hazina app services in `docker-compose.yml`:

```yaml
hazina-cli:
  build:
    context: .
    dockerfile: Dockerfile
    args:
      PROJECT_PATH: apps/CLI/Hazina.App.ClaudeCode
      PROJECT_NAME: Hazina.App.ClaudeCode
  environment:
    - OPENAI_API_KEY=${OPENAI_API_KEY}
    - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
```

## Production Deployment

### Cloud Platforms

#### Kubernetes (Recommended for Production)

```bash
# Create namespace
kubectl create namespace hazina

# Create secrets
kubectl create secret generic hazina-secrets \
  --from-env-file=.env \
  --namespace=hazina

# Deploy (create deployment.yaml first)
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/ingress.yaml
```

#### Azure Container Apps

```bash
# Create resource group
az group create --name hazina-rg --location eastus

# Create container app environment
az containerapp env create \
  --name hazina-env \
  --resource-group hazina-rg \
  --location eastus

# Deploy container app
az containerapp create \
  --name hazina-app \
  --resource-group hazina-rg \
  --environment hazina-env \
  --image ghcr.io/your-org/hazina-claude-code:latest \
  --secrets \
    openai-key=${OPENAI_API_KEY} \
    anthropic-key=${ANTHROPIC_API_KEY}
```

#### AWS ECS/Fargate

```bash
# Create ECR repository
aws ecr create-repository --repository-name hazina-claude-code

# Tag and push image
docker tag hazina-claude-code:latest \
  aws_account_id.dkr.ecr.region.amazonaws.com/hazina-claude-code:latest

docker push aws_account_id.dkr.ecr.region.amazonaws.com/hazina-claude-code:latest

# Create ECS task definition (see examples/aws-ecs-task-definition.json)
aws ecs register-task-definition --cli-input-json file://task-definition.json
```

### Database Setup

#### PostgreSQL with pgvector

```bash
# Using Docker
docker-compose up -d postgres

# Or install PostgreSQL 16+
sudo apt install postgresql-16 postgresql-16-pgvector

# Initialize schema
psql -U hazina -d hazina -f scripts/init-db.sql
```

#### Supabase (Managed PostgreSQL)

```bash
# 1. Create project at https://supabase.com
# 2. Get connection string from Settings > Database
# 3. Run init script in SQL Editor
# 4. Configure in .env:
SUPABASE_URL=https://your-project.supabase.co
SUPABASE_ANON_KEY=your-anon-key
SUPABASE_CONNECTION_STRING=postgresql://...
```

### Environment Configuration

Create `appsettings.Production.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Postgres": "Host=postgres;Database=hazina;Username=hazina;Password=${POSTGRES_PASSWORD}"
  },
  "OpenTelemetry": {
    "ServiceName": "Hazina",
    "OtlpEndpoint": "http://jaeger:4317",
    "EnableConsoleExporter": false
  },
  "Observability": {
    "EnableStructuredLogging": true,
    "EnableDistributedTracing": true,
    "EnableMetrics": true
  }
}
```

## Observability

### Metrics (Prometheus)

**Endpoints:**
- Prometheus UI: `http://localhost:9090`
- Metrics endpoint: `http://your-app:8080/metrics`

**Key Metrics:**
- `hazina_operations_total` - Total LLM operations
- `hazina_operation_duration_milliseconds` - Operation latency
- `hazina_total_cost_usd` - Cumulative API costs
- `hazina_tokens_used` - Token consumption
- `hazina_hallucinations_detected` - Hallucination events

### Tracing (Jaeger)

**Endpoints:**
- Jaeger UI: `http://localhost:16686`
- OTLP gRPC: `http://localhost:4317`
- OTLP HTTP: `http://localhost:4318`

**Trace Tags:**
- `llm.provider` - AI provider (openai, anthropic, etc.)
- `llm.model` - Model name (gpt-4, claude-sonnet-4, etc.)
- `llm.cost_usd` - Request cost
- `llm.tokens.input` - Input tokens
- `llm.tokens.output` - Output tokens

### Dashboards (Grafana)

**Endpoints:**
- Grafana UI: `http://localhost:3000`
- Default credentials: `admin/admin`

**Pre-configured Datasources:**
- Prometheus (metrics)
- Jaeger (traces)

**Dashboard Ideas:**
1. **AI Operations Dashboard**
   - Request rate by provider
   - P50/P95/P99 latencies
   - Cost tracking
   - Error rates

2. **NeuroChain Dashboard**
   - Layer utilization
   - Confidence scores
   - Cross-validation results
   - Consensus rates

3. **System Health Dashboard**
   - CPU/Memory usage
   - Database connections
   - Cache hit rates
   - API rate limits

### Logging

**Structured Logs:**
```bash
# View logs
docker-compose logs -f hazina-app

# Log locations
./logs/hazina-YYYYMMDD.log  # Rolling daily logs

# Log format
[2026-01-05 10:30:00.123] [INF] [Hazina.AI.Providers] Operation completed | Provider: openai | Cost: $0.0042
```

**Log Aggregation:**
- Serilog sinks to Console and File by default
- Add Elasticsearch sink for centralized logging
- Add Seq for structured log querying

## Security

### Secret Management

**Environment Variables (Recommended):**
```bash
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."
```

**Encrypted Storage:**
```csharp
services.AddHazinaSecurity(options => {
    options.UseEnvironmentVariables = true;
});

// Secrets are automatically encrypted at rest
await secretManager.StoreSecretAsync("api-key", apiKey);
var decrypted = await secretManager.GetSecretAsync("api-key");
```

**Azure Key Vault Integration:**
```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://your-vault.vault.azure.net/"),
    new DefaultAzureCredential());
```

### Network Security

**Security Headers:**
```csharp
app.UseHazinaSecurityHeaders(options => {
    options.EnableStrictTransportSecurity = true;
    options.EnableContentSecurityPolicy = true;
    options.ContentSecurityPolicy = "default-src 'self'";
});
```

**Rate Limiting:**
```csharp
app.UseHazinaRateLimiting(options => {
    options.MaxRequests = 100;
    options.WindowSeconds = 60;
});
```

**Correlation IDs:**
```csharp
app.UseHazinaCorrelationId(options => {
    options.HeaderName = "X-Correlation-ID";
    options.IncludeInResponse = true;
});
```

### Input Validation

```csharp
services.AddHazinaInputValidation();

// Validate user input
var result = validator.ValidateInput(userInput, InputValidationType.Sql);
if (!result.IsValid)
{
    // Input contains SQL injection patterns
}

// Sanitize input
var safe = validator.SanitizeInput(userInput, SanitizationType.Html);
```

## Troubleshooting

### Common Issues

**1. Database Connection Failed**
```bash
# Check PostgreSQL is running
docker-compose ps postgres

# Test connection
docker-compose exec postgres pg_isready -U hazina

# View logs
docker-compose logs postgres
```

**2. API Key Not Found**
```bash
# Verify .env file
cat .env | grep API_KEY

# Check environment variables
docker-compose config
```

**3. Port Already in Use**
```bash
# Find process using port
lsof -i :5432  # Linux/Mac
netstat -ano | findstr :5432  # Windows

# Change port in docker-compose.yml
ports:
  - "5433:5432"  # Map to different host port
```

**4. Build Failures**
```bash
# Clean build
dotnet clean
dotnet restore
dotnet build --configuration Release

# Check for package conflicts
dotnet list package --vulnerable
```

### Health Checks

```bash
# Check all services
curl http://localhost:8080/health

# Check specific endpoints
curl http://localhost:9090/-/healthy  # Prometheus
curl http://localhost:16686/  # Jaeger
```

### Performance Tuning

**Database:**
```sql
-- Check slow queries
SELECT * FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;

-- Analyze tables
ANALYZE hazina.embeddings;
```

**Memory:**
```bash
# Monitor container memory
docker stats

# Increase container limits
docker-compose.yml:
  deploy:
    resources:
      limits:
        memory: 4G
      reservations:
        memory: 2G
```

## Support

For issues and questions:
- GitHub Issues: https://github.com/your-org/hazina/issues
- Documentation: https://docs.hazina.ai
- Community: https://discord.gg/hazina

## License

See [LICENSE](LICENSE) file for details.
