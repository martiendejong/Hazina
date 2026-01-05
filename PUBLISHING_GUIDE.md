# Hazina Publishing Guide

Complete guide for publishing Hazina NuGet packages and Docker images.

## Prerequisites

### For NuGet Publishing

1. **NuGet API Key**
   - Create account at https://www.nuget.org
   - Go to API Keys: https://www.nuget.org/account/apikeys
   - Create new API key with "Push" permission
   - Save the key securely (it's shown only once)

2. **.NET SDK 9.0+**
   ```bash
   dotnet --version
   ```

### For Docker Publishing

1. **Docker Desktop**
   - Install from https://www.docker.com/products/docker-desktop

2. **Container Registry Account**
   - GitHub Container Registry (recommended): Free with GitHub account
   - Docker Hub: https://hub.docker.com
   - Azure Container Registry: https://azure.microsoft.com/services/container-registry/

3. **Authentication**
   ```bash
   # GitHub Container Registry
   echo $GITHUB_TOKEN | docker login ghcr.io -u YOUR_USERNAME --password-stdin

   # Docker Hub
   docker login

   # Azure Container Registry
   az acr login --name YOUR_REGISTRY
   ```

## Publishing NuGet Packages

### Method 1: Using PowerShell Script (Windows)

```powershell
# Dry run (build and pack only, no publish)
.\scripts\publish-nuget.ps1 -ApiKey "your-api-key" -Version "1.0.0" -DryRun

# Publish to NuGet.org
.\scripts\publish-nuget.ps1 -ApiKey "your-api-key" -Version "1.0.0"
```

### Method 2: Using GitHub Actions (Recommended)

1. **Add NuGet API Key to GitHub Secrets**
   - Go to repository Settings → Secrets and variables → Actions
   - Add secret: `NUGET_API_KEY` = your NuGet API key

2. **Create and push version tag**
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   ```

3. **Workflow automatically publishes**
   - Workflow: `.github/workflows/publish.yml`
   - Triggers on tags matching `v*`
   - Builds, tests, packs, and publishes all packages

### Method 3: Manual Publishing

```bash
# Build solution
dotnet build Hazina.sln --configuration Release

# Pack specific package
dotnet pack src/Core/AI/Hazina.AI.Providers/Hazina.AI.Providers.csproj \
  --configuration Release \
  --no-build \
  --output nupkgs \
  /p:Version=1.0.0

# Publish package
dotnet nuget push nupkgs/Hazina.AI.Providers.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

## Publishing Docker Images

### Method 1: Using PowerShell Script (Windows)

```powershell
# Dry run (build only, no push)
.\scripts\publish-docker.ps1 `
  -Registry "ghcr.io/yourorg" `
  -Version "1.0.0" `
  -DryRun

# Build and push to GitHub Container Registry
.\scripts\publish-docker.ps1 `
  -Registry "ghcr.io/yourorg" `
  -Version "1.0.0"

# Build and push to Docker Hub
.\scripts\publish-docker.ps1 `
  -Registry "yourorg" `
  -Version "1.0.0"
```

### Method 2: Using GitHub Actions (Recommended)

1. **Automatic on push to main**
   - Workflow: `.github/workflows/docker.yml`
   - Triggers on push to `main` branch
   - Builds and pushes to GitHub Container Registry (ghcr.io)
   - Uses `GITHUB_TOKEN` (automatically provided)

2. **Manual trigger**
   - Go to Actions → Docker Build and Push → Run workflow
   - Select branch and options

### Method 3: Manual Docker Build

```bash
# Build CLI application
docker build \
  --build-arg PROJECT_PATH=apps/CLI/Hazina.App.ClaudeCode \
  --build-arg PROJECT_NAME=Hazina.App.ClaudeCode \
  -t ghcr.io/yourorg/hazina-cli:1.0.0 \
  -t ghcr.io/yourorg/hazina-cli:latest \
  .

# Push to registry
docker push ghcr.io/yourorg/hazina-cli:1.0.0
docker push ghcr.io/yourorg/hazina-cli:latest

# Build Web application
docker build \
  --build-arg PROJECT_PATH=apps/Web/Hazina.App.HtmlMockupGenerator \
  --build-arg PROJECT_NAME=Hazina.App.HtmlMockupGenerator \
  -t ghcr.io/yourorg/hazina-web:1.0.0 \
  -t ghcr.io/yourorg/hazina-web:latest \
  .

# Push to registry
docker push ghcr.io/yourorg/hazina-web:1.0.0
docker push ghcr.io/yourorg/hazina-web:latest
```

## Package List

### Core Packages

| Package | Description | Dependencies |
|---------|-------------|--------------|
| `Hazina.LLMs.Client` | Base LLM client interface | - |
| `Hazina.LLMs.Classes` | Common LLM data models | - |
| `Hazina.LLMs.OpenAI` | OpenAI provider | LLMs.Client |
| `Hazina.LLMs.Anthropic` | Anthropic (Claude) provider | LLMs.Client |
| `Hazina.AI.Providers` | Multi-provider abstraction | LLMs.Client |
| `Hazina.AI.FluentAPI` | Fluent API for AI operations | AI.Providers |
| `Hazina.Neurochain.Core` | Multi-layer reasoning | AI.Providers |
| `Hazina.Security.Core` | Security and encryption | - |
| `Hazina.Observability.Core` | Logging and telemetry | - |

### All Packages (60+)

See `scripts/publish-nuget.ps1` for complete list in dependency order.

## Versioning Strategy

### Semantic Versioning

Hazina follows [Semantic Versioning 2.0.0](https://semver.org/):

- **MAJOR** (1.x.x): Breaking API changes
- **MINOR** (x.1.x): New features, backward compatible
- **PATCH** (x.x.1): Bug fixes, backward compatible

### Version Synchronization

All packages share the same version number for consistency:
- Current version: **1.0.0**
- Next minor release: **1.1.0**
- Next major release: **2.0.0**

### Release Process

1. **Update version in all .csproj files**
   ```xml
   <Version>1.1.0</Version>
   ```

2. **Update CHANGELOG.md**
   - Document all changes
   - Follow [Keep a Changelog](https://keepachangelog.com/) format

3. **Commit changes**
   ```bash
   git add -A
   git commit -m "Bump version to 1.1.0"
   ```

4. **Create and push tag**
   ```bash
   git tag -a v1.1.0 -m "Release v1.1.0"
   git push origin main
   git push origin v1.1.0
   ```

5. **GitHub Actions automatically publishes**
   - NuGet packages
   - Docker images
   - GitHub Release with notes

## Package Configuration

### Required .csproj Properties

```xml
<PropertyGroup>
  <!-- Package identity -->
  <PackageId>Hazina.YourPackage</PackageId>
  <Version>1.0.0</Version>

  <!-- Package metadata -->
  <Authors>Hazina Team</Authors>
  <Company>Hazina</Company>
  <Description>Your package description</Description>
  <PackageTags>ai;llm;hazina</PackageTags>

  <!-- License and repository -->
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <RepositoryUrl>https://github.com/hazinatech/hazina</RepositoryUrl>
  <RepositoryType>git</RepositoryType>

  <!-- Documentation -->
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <PackageReadmeFile>README.md</PackageReadmeFile>

  <!-- Source link -->
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>
```

## Verification

### Verify NuGet Packages

1. **Check NuGet.org**
   - Go to https://www.nuget.org/packages/Hazina.AI.Providers
   - Verify version is published
   - Check download stats

2. **Test installation**
   ```bash
   dotnet new console -n TestPackage
   cd TestPackage
   dotnet add package Hazina.AI.Providers
   dotnet restore
   ```

3. **Test import**
   ```csharp
   using Hazina.AI.Providers;
   // Use the package
   ```

### Verify Docker Images

1. **Check registry**
   - GitHub: https://github.com/yourorg/hazina/pkgs/container/hazina-cli
   - Docker Hub: https://hub.docker.com/r/yourorg/hazina-cli

2. **Pull and test**
   ```bash
   docker pull ghcr.io/yourorg/hazina-cli:1.0.0
   docker run --rm ghcr.io/yourorg/hazina-cli:1.0.0 --version
   ```

3. **Test with docker-compose**
   ```yaml
   services:
     hazina:
       image: ghcr.io/yourorg/hazina-cli:1.0.0
       environment:
         - OPENAI_API_KEY=${OPENAI_API_KEY}
   ```

## Troubleshooting

### NuGet Publishing Issues

**Error: Package already exists**
```
The package already exists and cannot be modified.
```
Solution: Increment version number. NuGet doesn't allow overwriting.

**Error: Invalid API key**
```
401 (Unauthorized)
```
Solution: Regenerate API key at https://www.nuget.org/account/apikeys

**Error: Package validation failed**
```
Package validation failed: Invalid license expression
```
Solution: Check .csproj has valid `<PackageLicenseExpression>` or `<PackageLicenseFile>`

### Docker Publishing Issues

**Error: Access denied**
```
denied: permission_denied
```
Solution: Ensure you're logged in to the correct registry:
```bash
docker login ghcr.io -u YOUR_USERNAME
```

**Error: Build failed**
```
failed to solve: rpc error
```
Solution: Ensure Docker has enough resources (Settings → Resources → Advanced)

**Error: Image too large**
```
Image size exceeds limit
```
Solution: Use multi-stage builds (already implemented in Dockerfile)

## Best Practices

### Before Publishing

- [ ] Run full test suite: `dotnet test Hazina.sln`
- [ ] Check for security vulnerabilities: `dotnet list package --vulnerable`
- [ ] Update CHANGELOG.md with all changes
- [ ] Update documentation (README.md, docs/)
- [ ] Verify all .csproj files have correct version
- [ ] Test packages locally before publishing
- [ ] Review breaking changes (if any)

### Security

- [ ] Never commit API keys to git
- [ ] Use GitHub Secrets for CI/CD
- [ ] Rotate API keys every 90 days
- [ ] Enable 2FA on NuGet.org and Docker Hub
- [ ] Sign packages (optional, but recommended)
- [ ] Scan Docker images with Trivy before pushing

### Documentation

- [ ] Update package README files
- [ ] Add XML documentation comments
- [ ] Include usage examples
- [ ] Document breaking changes
- [ ] Update migration guides (for major versions)

## Support

For publishing issues:
- GitHub Issues: https://github.com/hazinatech/hazina/issues
- NuGet Support: https://www.nuget.org/policies/Contact
- Docker Support: https://www.docker.com/support

## References

- [NuGet Documentation](https://docs.microsoft.com/en-us/nuget/)
- [Docker Documentation](https://docs.docker.com/)
- [GitHub Container Registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [Semantic Versioning](https://semver.org/)
- [Keep a Changelog](https://keepachangelog.com/)
