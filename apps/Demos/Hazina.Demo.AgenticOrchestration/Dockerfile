# Multi-stage Dockerfile for Hazina Terminal Orchestrator
# Optimized for production deployment with minimal image size

# ============================================================================
# Stage 1: Build frontend (React + Vite)
# ============================================================================
FROM node:20-alpine AS frontend-build

WORKDIR /app/frontend

# Copy package files for dependency caching
COPY ClientApp/package*.json ./
RUN npm ci --only=production

# Copy frontend source
COPY ClientApp/ ./

# Build production bundle
RUN npm run build

# ============================================================================
# Stage 2: Build backend (.NET 9.0)
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS backend-build

WORKDIR /app

# Copy csproj and restore dependencies (caching layer)
COPY *.csproj ./
RUN dotnet restore --runtime linux-musl-x64

# Copy source code
COPY . ./

# Copy built frontend from previous stage
COPY --from=frontend-build /app/frontend/dist ./wwwroot

# Build backend in Release mode
RUN dotnet publish -c Release \
    --runtime linux-musl-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -o /app/publish

# ============================================================================
# Stage 3: Runtime (minimal Alpine image)
# ============================================================================
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS runtime

# Install dependencies for PTY support
RUN apk add --no-cache \
    icu-libs \
    tzdata

# Create non-root user for security
RUN addgroup -g 1000 hazina && \
    adduser -D -u 1000 -G hazina hazina

WORKDIR /app

# Copy published app from build stage
COPY --from=backend-build --chown=hazina:hazina /app/publish ./

# Create directories for data persistence
RUN mkdir -p /app/data /app/logs && \
    chown -R hazina:hazina /app/data /app/logs

# Switch to non-root user
USER hazina

# Expose HTTP port
EXPOSE 5000

# Health check endpoint
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5000/health || exit 1

# Environment variables (can be overridden)
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    AgenticOrchestration__DatabasePath=/app/data/agent-activity.db \
    AgenticOrchestration__LogsPath=/app/logs \
    AgenticOrchestration__SessionLogging__BasePath=/app/logs/agent-sessions

# Start the application
ENTRYPOINT ["./HazinaOrchestration"]
