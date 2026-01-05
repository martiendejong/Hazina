# Docker Build Commands for Hazina

## Prerequisites
- Docker Desktop installed and running
- Authenticated to GitHub Container Registry: `echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin`

## Build Commands

### 1. Hazina CLI (ClaudeCode)
```bash
cd C:\Projects\hazina

docker build \
  --build-arg PROJECT_PATH=apps/CLI/Hazina.App.ClaudeCode \
  --build-arg PROJECT_NAME=Hazina.App.ClaudeCode \
  -t ghcr.io/martiendejong/hazina-cli:1.0.0 \
  -t ghcr.io/martiendejong/hazina-cli:latest \
  .

docker push ghcr.io/martiendejong/hazina-cli:1.0.0
docker push ghcr.io/martiendejong/hazina-cli:latest
```

### 2. Hazina App Builder
```bash
docker build \
  --build-arg PROJECT_PATH=apps/AppBuilder/Hazina.App.AppBuilder \
  --build-arg PROJECT_NAME=Hazina.App.AppBuilder \
  -t ghcr.io/martiendejong/hazina-app-builder:1.0.0 \
  -t ghcr.io/martiendejong/hazina-app-builder:latest \
  .

docker push ghcr.io/martiendejong/hazina-app-builder:1.0.0
docker push ghcr.io/martiendejong/hazina-app-builder:latest
```

### 3. Hazina Embeddings Viewer
```bash
docker build \
  --build-arg PROJECT_PATH=apps/Visualizers/Hazina.App.EmbeddingsViewer \
  --build-arg PROJECT_NAME=Hazina.App.EmbeddingsViewer \
  -t ghcr.io/martiendejong/hazina-embeddings-viewer:1.0.0 \
  -t ghcr.io/martiendejong/hazina-embeddings-viewer:latest \
  .

docker push ghcr.io/martiendejong/hazina-embeddings-viewer:1.0.0
docker push ghcr.io/martiendejong/hazina-embeddings-viewer:latest
```

### 4. Hazina HTML Mockup Generator
```bash
docker build \
  --build-arg PROJECT_PATH=apps/Tools/Hazina.App.HtmlMockupGenerator \
  --build-arg PROJECT_NAME=Hazina.App.HtmlMockupGenerator \
  -t ghcr.io/martiendejong/hazina-mockup-generator:1.0.0 \
  -t ghcr.io/martiendejong/hazina-mockup-generator:latest \
  .

docker push ghcr.io/martiendejong/hazina-mockup-generator:1.0.0
docker push ghcr.io/martiendejong/hazina-mockup-generator:latest
```

## Automated Build (GitHub Actions)

The `.github/workflows/docker.yml` workflow will automatically build and push Docker images when:
- A version tag is pushed (e.g., `v1.0.0`)
- Manually triggered via GitHub Actions UI

### Trigger Automated Build:
```bash
# Tag current commit
git tag v1.0.0
git push origin v1.0.0

# GitHub Actions will automatically:
# 1. Build all Docker images
# 2. Scan with Trivy for vulnerabilities
# 3. Generate SBOM
# 4. Push to ghcr.io
```

## Verify Published Images

```bash
# Pull and test
docker pull ghcr.io/martiendejong/hazina-cli:1.0.0
docker run --rm ghcr.io/martiendejong/hazina-cli:1.0.0 --help

# View on GitHub
https://github.com/martiendejong/Hazina/pkgs/container/hazina-cli
```

## Notes

- All images use multi-stage builds for minimal size
- Images run as non-root user `hazina` for security
- Health checks are included for production deployments
- Base image: `mcr.microsoft.com/dotnet/aspnet:9.0`
