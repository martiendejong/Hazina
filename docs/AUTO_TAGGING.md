# Automatic Stable Release Tagging

## Overview

The Hazina framework repository uses an automated GitHub Actions workflow to create stable release tags whenever a PR is merged from `develop` to `main`.

## How It Works

### Trigger
- **Event**: Pull Request closed
- **Branch**: `main` (target branch)
- **Condition**: PR must be merged (not just closed)

### Tag Format
Tags follow the pattern: `v{YYYY}.{MM}.{DD}-stable`

**Examples:**
- `v2026.01.11-stable` (January 11, 2026)
- `v2026.12.25-stable` (December 25, 2026)

### Workflow Steps

1. **PR Merged to Main**: When a PR from `develop` (or any branch) is merged into `main`
2. **Workflow Triggers**: The `auto-tag-stable.yml` workflow activates automatically
3. **Tag Generation**: Creates a tag with current date in format `vYYYY.MM.DD-stable`
4. **Duplicate Check**: Skips if tag already exists (multiple PRs on same day)
5. **Tag Creation**: Creates annotated tag with PR information and metadata
6. **Push to Remote**: Pushes tag to GitHub

### Tag Content

Each tag includes:
- **Date**: Stable release checkpoint date
- **PR Information**: PR number and title
- **Source Branch**: Which branch was merged
- **Signature**: Automated tag attribution

**Example Tag Message:**
```
Stable release checkpoint - 2026-01-11

Merged PR #36: Add automatic stable tagging workflow

Source branch: feature/auto-stable-tagging

Automatically tagged by GitHub Actions on PR merge to main.

Signed-off-by: github-actions[bot] <github-actions[bot]@users.noreply.github.com>
```

## Benefits

✅ **Automatic**: No manual intervention needed
✅ **Consistent**: Same format across all releases
✅ **Traceable**: Tags include PR information
✅ **Date-based**: Easy to identify when release was created
✅ **Duplicate-safe**: Won't create duplicate tags on same day

## Viewing Tags

### Via Git CLI
```bash
# List all stable tags
git tag -l "v*-stable"

# List stable tags sorted by date (newest first)
git tag -l "v*-stable" --sort=-creatordate

# Show tag details
git show v2026.01.11-stable
```

### Via GitHub UI
1. Go to repository
2. Click **"Releases"** or **"Tags"** tab
3. Find tags matching `v*-stable` pattern

## Workflow File Location

```
.github/workflows/auto-tag-stable.yml
```

## Permissions Required

The workflow needs:
- `contents: write` - To create and push tags

## Manual Override

If you need to create a tag manually:
```bash
cd /c/Projects/hazina
git checkout main && git pull origin main
git tag -a "vYYYY.MM.DD-stable" -m "Stable release checkpoint - YYYY-MM-DD

Manual tag description here.

Signed-off-by: Your Name <your.email@example.com>"
git push origin vYYYY.MM.DD-stable
```

## Troubleshooting

### Tag Not Created
**Check:**
1. Was the PR merged (not just closed)?
2. Was the target branch `main`?
3. Check Actions tab for workflow run status
4. Check workflow logs for errors

### Duplicate Tag Warning
If multiple PRs are merged to `main` on the same day, only the first one will create the tag. Subsequent PRs will skip tag creation with a warning:
```
⚠️ Tag v2026.01.11-stable already exists. Skipping tag creation.
```
This is expected behavior - only one stable tag per day.

### Permissions Error
If workflow fails with permissions error:
1. Check repository settings → Actions → General
2. Ensure "Read and write permissions" is enabled for workflows
3. Verify workflow has `contents: write` permission

## Related Documentation

- **Manual Tagging Process**: See `C:\scripts\claude.md` § "Stable Release Tagging"
- **Git-Flow Workflow**: See `C:\scripts\claude.md` § "GIT-FLOW WORKFLOW RULES"
- **Client-Manager Tagging**: Client-manager repository has identical auto-tagging workflow

## Coordination with Client-Manager

Both `hazina` and `client-manager` repositories have this workflow. When merging related PRs:

1. Merge Hazina PR to `main` first → Creates tag `vYYYY.MM.DD-stable` in Hazina
2. Merge client-manager PR to `main` → Creates tag `vYYYY.MM.DD-stable` in client-manager

Both repositories will have the **same tag name** for releases on the same day, maintaining synchronization.

## Version History

- **2026-01-11**: Automatic tagging workflow implemented
- **Previous**: Manual tagging process (see claude.md line 860-900)

---

**Workflow Status**: ✅ Active
**Last Updated**: 2026-01-11
