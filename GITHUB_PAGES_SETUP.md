# GitHub Pages Setup for Hazina Documentation

This guide explains how to enable GitHub Pages for automatic documentation deployment.

## One-Time Setup (Repository Admin)

### Step 1: Enable GitHub Pages

1. Go to your repository on GitHub
2. Click **Settings** → **Pages** (in the left sidebar)
3. Under **Build and deployment**:
   - **Source**: Select "GitHub Actions"
4. Click **Save**

That's it! The workflow will automatically deploy documentation when you push to `develop` or `main`.

## How It Works

### Automatic Deployment

The `.github/workflows/deploy-docs.yml` workflow:

1. **Triggers on**:
   - Push to `develop` or `main` branches
   - Pull requests to `develop` or `main` (build only, no deploy)
   - Manual trigger via GitHub Actions UI

2. **Build process**:
   - Checks out code
   - Sets up .NET 9.0
   - Restores dotnet tools (DocFX)
   - Generates API metadata from XML comments
   - Builds documentation site

3. **Deployment** (only on push to develop/main):
   - Uploads documentation to GitHub Pages
   - Makes it live at: `https://<username>.github.io/<repository>/`

### Documentation URL

Once deployed, your documentation will be available at:

**https://martiendejong.github.io/Hazina/**

## Manual Triggering

You can manually trigger documentation deployment:

1. Go to **Actions** tab in GitHub
2. Select **Deploy Documentation** workflow
3. Click **Run workflow**
4. Select branch (develop or main)
5. Click **Run workflow**

## Local Preview

To preview documentation locally before pushing:

```bash
# Generate and serve locally
.\generate-docs.ps1 -Serve

# Opens at http://localhost:8080
```

## Workflow Status

Check deployment status:
- **Actions** tab in GitHub
- Look for "Deploy Documentation" workflow runs
- Green checkmark = successful deployment
- Red X = build/deployment failed

## Troubleshooting

### Build Fails

**Issue**: DocFX build fails with errors

**Solution**:
1. Run `.\generate-docs.ps1` locally
2. Fix any errors or warnings
3. Commit and push fixes

### Deployment Fails

**Issue**: Build succeeds but deployment fails

**Solution**:
1. Verify GitHub Pages is enabled (Settings → Pages)
2. Check workflow has `pages: write` and `id-token: write` permissions
3. Ensure "Source" is set to "GitHub Actions"

### Documentation Not Updating

**Issue**: Changes not visible on GitHub Pages

**Solution**:
1. Check Actions tab for recent workflow runs
2. Wait 1-2 minutes for CDN cache to clear
3. Hard refresh browser (Ctrl+Shift+R / Cmd+Shift+R)

## Costs

**GitHub Pages is 100% FREE for public repositories!**

Limitations:
- 1GB storage limit (documentation rarely exceeds 100MB)
- 100GB bandwidth per month (more than enough for docs)

## Security

The workflow uses:
- **Least privilege permissions**: Only `contents: read`, `pages: write`, `id-token: write`
- **Concurrency control**: Prevents multiple simultaneous deployments
- **No secrets required**: Uses GitHub's built-in GITHUB_TOKEN

## Customization

### Change Deployment Branch

Edit `.github/workflows/deploy-docs.yml`:

```yaml
on:
  push:
    branches:
      - main  # Change to your preferred branch
```

### Add Custom Domain

1. Add `CNAME` file to `docs/` directory:
   ```
   docs.hazina.ai
   ```

2. Configure DNS:
   - Add CNAME record pointing to `<username>.github.io`

3. Enable HTTPS in GitHub Pages settings

## Monitoring

View deployment history:
- **Actions** tab → **Deploy Documentation** workflow
- Each run shows:
  - Build duration
  - Deployment URL
  - Error logs (if failed)

---

**Documentation is now automatically published on every push to develop/main!** 🚀
