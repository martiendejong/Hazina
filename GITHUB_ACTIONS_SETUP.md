# GitHub Actions Setup Guide

## Required Secrets

To enable automated builds, publishing, and security scanning, you need to configure the following secrets in your GitHub repository.

### 1. Add NUGET_API_KEY

**Steps:**
1. Go to https://github.com/martiendejong/Hazina/settings/secrets/actions
2. Click "New repository secret"
3. Name: `NUGET_API_KEY`
4. Value: `<your-nuget-api-key-here>`
5. Click "Add secret"

**To get your NuGet API key:**
- Go to https://www.nuget.org/account/apikeys
- Create new API key with "Push" permission
- Copy the key and paste it in step 4 above

**Used by:**
- `.github/workflows/build-and-test.yml` - Automatic NuGet publishing on version tags
- `.github/workflows/docker.yml` - Publishing packages alongside Docker images

### 2. GitHub Token (Automatic)

The `GITHUB_TOKEN` is automatically provided by GitHub Actions for:
- Pushing Docker images to ghcr.io (GitHub Container Registry)
- Publishing security scan results (CodeQL, Trivy)
- Creating pull request comments

**No configuration needed** - automatically available as `${{ secrets.GITHUB_TOKEN }}`

## Workflow Configuration

### Build and Test Workflow
**File:** `.github/workflows/build-and-test.yml`

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests
- Version tags (e.g., `v1.0.0`)

**Features:**
- Multi-job pipeline: Build → Security → Quality → Publish
- Code coverage with Coverlet
- Trivy security scanning
- Automatic NuGet publishing on version tags
- Test result publishing to GitHub

**Environment Variables:**
```yaml
NUGET_SOURCE: https://api.nuget.org/v3/index.json
```

### Docker Build Workflow
**File:** `.github/workflows/docker.yml`

**Triggers:**
- Version tags (e.g., `v1.0.0`)
- Manual workflow dispatch

**Features:**
- Matrix builds for multiple apps:
  - hazina-cli
  - hazina-app-builder
  - hazina-embeddings-viewer
  - hazina-mockup-generator
- Trivy vulnerability scanning
- SBOM generation (SPDX format)
- Multi-tag support (version + latest)

**Registry:**
```
ghcr.io/martiendejong/hazina-*:1.0.0
ghcr.io/martiendejong/hazina-*:latest
```

### CodeQL Security Analysis
**File:** `.github/workflows/codeql.yml`

**Triggers:**
- Weekly schedule (Mondays at 00:00 UTC)
- Manual workflow dispatch

**Features:**
- Static application security testing (SAST)
- Detects security vulnerabilities in C# code
- Results published to GitHub Security tab

## Enable GitHub Container Registry

### 1. Make Packages Public (Optional)
1. Go to https://github.com/martiendejong/Hazina/pkgs/container/hazina-cli
2. Click "Package settings"
3. Under "Danger Zone", click "Change visibility"
4. Select "Public" if you want the images to be publicly accessible

### 2. Verify Push Permissions
GitHub Actions automatically has permission to push to GHCR. If you encounter issues:

1. Go to https://github.com/martiendejong/Hazina/settings/actions
2. Under "Workflow permissions", ensure:
   - ☑ "Read and write permissions" is selected
   - ☑ "Allow GitHub Actions to create and approve pull requests" (optional)

## Testing Workflows

### Test Build Workflow
```bash
# Push a commit to trigger build
git add .
git commit -m "Test: Trigger GitHub Actions build"
git push origin main
```

### Test NuGet Publishing
```bash
# Create and push a version tag
git tag v1.0.1
git push origin v1.0.1

# GitHub Actions will:
# 1. Build solution
# 2. Run tests
# 3. Pack NuGet packages
# 4. Publish to NuGet.org (using NUGET_API_KEY)
```

### Test Docker Build
```bash
# Create and push a version tag
git tag v1.0.1
git push origin v1.0.1

# GitHub Actions will:
# 1. Build Docker images for all apps
# 2. Scan with Trivy
# 3. Generate SBOM
# 4. Push to ghcr.io
```

### Manual Workflow Trigger
1. Go to https://github.com/martiendejong/Hazina/actions
2. Select workflow (e.g., "Docker Build and Push")
3. Click "Run workflow"
4. Select branch
5. Click "Run workflow"

## Monitoring Workflow Runs

### View Status
- All workflows: https://github.com/martiendejong/Hazina/actions
- Build and Test: https://github.com/martiendejong/Hazina/actions/workflows/build-and-test.yml
- Docker: https://github.com/martiendejong/Hazina/actions/workflows/docker.yml
- CodeQL: https://github.com/martiendejong/Hazina/actions/workflows/codeql.yml

### Check Security Results
- Security tab: https://github.com/martiendejong/Hazina/security
- CodeQL alerts: https://github.com/martiendejong/Hazina/security/code-scanning
- Dependabot: https://github.com/martiendejong/Hazina/security/dependabot

## Troubleshooting

### NuGet Push Fails
**Error:** `401 Unauthorized` or `403 Forbidden`

**Solution:**
1. Verify `NUGET_API_KEY` is correctly set in GitHub Secrets
2. Check NuGet.org API key hasn't expired
3. Ensure API key has "Push" permissions

### Docker Push Fails
**Error:** `denied: permission_denied`

**Solution:**
1. Verify workflow has "Read and write permissions" in repository settings
2. Check package visibility settings
3. Ensure `GITHUB_TOKEN` has correct scopes

### Workflow Not Triggering
**Error:** Workflow doesn't run on push/tag

**Solution:**
1. Check `.github/workflows/` files are in the `main` branch
2. Verify YAML syntax: https://www.yamllint.com/
3. Check workflow triggers match your event (push vs pull_request vs tag)

## Success Criteria

After configuration, you should see:

✅ **NuGet Packages**
- Published to: https://www.nuget.org/packages?q=Hazina
- Automatic versioning from git tags
- Symbol packages included

✅ **Docker Images**
- Published to: https://github.com/martiendejong?tab=packages
- Tagged with version and `latest`
- Security scanned by Trivy
- SBOM available

✅ **Security Scanning**
- CodeQL analysis: https://github.com/martiendejong/Hazina/security/code-scanning
- Trivy results in workflow logs
- No high/critical vulnerabilities

✅ **CI/CD Pipeline**
- Green checkmarks on commits
- Automatic testing on PRs
- Code coverage reports
- Fast feedback (<10 minutes)
