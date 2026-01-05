# Multi-stage Dockerfile for Hazina applications
# Usage: docker build --build-arg PROJECT_PATH=apps/CLI/Hazina.App.ClaudeCode --build-arg PROJECT_NAME=Hazina.App.ClaudeCode -t hazina-claude-code .

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG PROJECT_PATH
ARG PROJECT_NAME
WORKDIR /src

# Copy solution and project files
COPY Hazina.sln ./
COPY ["src/", "src/"]
COPY ["apps/", "apps/"]
COPY ["Tests/", "Tests/"]

# Restore dependencies
RUN dotnet restore "${PROJECT_PATH}/${PROJECT_NAME}.csproj"

# Build application
RUN dotnet build "${PROJECT_PATH}/${PROJECT_NAME}.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/build

# Stage 2: Publish
FROM build AS publish
ARG PROJECT_PATH
ARG PROJECT_NAME
RUN dotnet publish "${PROJECT_PATH}/${PROJECT_NAME}.csproj" \
    --configuration Release \
    --no-build \
    --output /app/publish \
    /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install security updates
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -r hazina && useradd -r -g hazina hazina
RUN chown -R hazina:hazina /app

# Copy published app
COPY --from=publish --chown=hazina:hazina /app/publish .

# Health check (customize port as needed)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Switch to non-root user
USER hazina

# Expose port (customize as needed)
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet"]
# CMD will be set per application, e.g., ["Hazina.App.ClaudeCode.dll"]
