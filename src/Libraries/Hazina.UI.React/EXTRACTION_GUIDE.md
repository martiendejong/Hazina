# MediaLibrary Component Extraction Guide

## Current Status

✅ Package structure created
✅ TypeScript types defined (MediaLibraryConfig, MediaServiceAdapter, etc.)
✅ Documentation complete (README.md with integration examples)
❌ Component implementation pending (see steps below)

## Source Component

**Location:** `C:\Projects\client-manager\ClientManagerFrontend\src\components\media\MediaLibrary.tsx`
**Size:** 567 lines
**Dependencies:**
- `useProject` hook (project-specific, needs removal)
- `mediaService` (needs injection via config)
- `lucide-react` icons (already in package.json)

## Extraction Steps

### Step 1: Copy Component

```bash
cp C:/Projects/client-manager/ClientManagerFrontend/src/components/media/MediaLibrary.tsx \
   C:/Projects/hazina/packages/ui-react/src/components/MediaLibrary.tsx
```

### Step 2: Genericize Context (projectId → contextId)

**Find and replace:**

```typescript
// OLD (project-specific)
const { project } = useProject()
const projectId = project?.id || ''

// NEW (generic)
interface MediaLibraryProps {
  config: MediaLibraryConfig
}

export default function MediaLibrary({ config }: MediaLibraryProps) {
  const contextId = typeof config.contextId === 'function'
    ? config.contextId()
    : config.contextId
```

**Update all API calls:**

```typescript
// OLD
await mediaService.list(projectId, options)

// NEW
await config.service.list(contextId, options)
```

### Step 3: Remove Direct Service Imports

```typescript
// DELETE these imports
import { useProject } from '../../stores/projectStore'
import mediaService from '../../services/media'

// ADD these imports
import type {
  MediaLibraryConfig,
  MediaAsset,
  MediaListResponse,
  MediaVariant
} from '../types/media'
```

### Step 4: Make Platform Presets Configurable

```typescript
// OLD (hardcoded)
const PLATFORM_PRESETS = [
  { platform: 'linkedin', width: 1200, height: 627, label: 'LinkedIn' },
  // ...
]

// NEW (from config)
const platformPresets = config.ui?.platformPresets || [
  { platform: 'linkedin', width: 1200, height: 627, label: 'LinkedIn' },
  { platform: 'instagram', width: 1080, height: 1080, label: 'Instagram' },
  { platform: 'twitter', width: 1200, height: 675, label: 'Twitter/X' },
  { platform: 'facebook', width: 1200, height: 630, label: 'Facebook' },
  { platform: 'thumbnail', width: 300, height: 300, label: 'Thumbnail' },
]
```

### Step 5: Add Feature Flags

```typescript
// Wrap features in conditionals
{config.features?.upload !== false && (
  <button onClick={() => fileInputRef.current?.click()}>
    Upload
  </button>
)}

{config.features?.delete !== false && selectedIds.size > 0 && (
  <button onClick={handleBulkDelete}>
    Delete ({selectedIds.size})
  </button>
)}

{config.features?.variants !== false && asset.mimeType.startsWith('image/') && (
  <button onClick={() => openDetail(asset)}>
    Manage variants
  </button>
)}
```

### Step 6: Add Auth Token Support

```typescript
// Update fetch calls
const headers: HeadersInit = {
  'Content-Type': 'application/json'
}

if (config.getAuthToken) {
  headers['Authorization'] = `Bearer ${config.getAuthToken()}`
}

const response = await fetch(url, { headers })
```

### Step 7: Add Error Handling

```typescript
// Wrap all API calls
try {
  await config.service.upload(contextId, files, options)
  config.onUpload?.(result)
} catch (err) {
  const error = err instanceof Error ? err : new Error(String(err))
  config.onError?.(error)
  console.error('Upload failed:', error)
}
```

### Step 8: Add Callbacks

```typescript
// Trigger config callbacks
const handleAssetClick = (asset: MediaAsset) => {
  if (config.onSelect) {
    config.onSelect(asset)
  } else {
    toggleSelect(asset.id)
  }
}
```

### Step 9: Export Component

```typescript
// src/index.ts
export { default as MediaLibrary } from './components/MediaLibrary'
export type { MediaLibraryConfig, MediaServiceAdapter } from './types/media'
```

### Step 10: Build & Test

```bash
cd C:/Projects/hazina/packages/ui-react
npm install
npm run build

# Test in client-manager
cd C:/Projects/client-manager/ClientManagerFrontend
npm install ../../../hazina/packages/ui-react
```

## Verification Checklist

- [ ] Component compiles without errors
- [ ] No hardcoded `projectId` references
- [ ] No direct service imports
- [ ] Config interface fully implemented
- [ ] Feature flags work correctly
- [ ] Auth token passed to API calls
- [ ] Callbacks fire (onSelect, onUpload, onError)
- [ ] Works in client-manager (project context)
- [ ] Works in SEO God (website context)
- [ ] TypeScript types exported correctly
- [ ] Build output includes .d.ts files

## Integration Testing

**Test in client-manager:**

```typescript
// Replace existing MediaLibrary
import { MediaLibrary } from '@hazina/ui-react'
import type { MediaLibraryConfig } from '@hazina/ui-react'

const config: MediaLibraryConfig = {
  contextType: 'project',
  contextId: project?.id || '',
  service: mediaService,  // existing service
  // ... rest of config
}

<MediaLibrary config={config} />
```

**Test in SEO God:**

```typescript
import { MediaLibrary } from '@hazina/ui-react'

const config: MediaLibraryConfig = {
  contextType: 'website',
  contextId: () => websiteStore.selectedWebsiteId,
  // ... rest of config
}

<MediaLibrary config={config} />
```

## Publishing

```bash
cd C:/Projects/hazina/packages/ui-react

# Update version in package.json
npm version patch

# Build
npm run build

# Publish to NPM (if public)
npm publish --access public

# Or commit to git for submodule usage
git add .
git commit -m "feat: Add MediaLibrary component to @hazina/ui-react"
git push
```

## Estimated Time

- Steps 1-9: **2-3 hours**
- Testing & fixes: **1 hour**
- Documentation: **30 minutes**
**Total:** ~4 hours for complete extraction

## Notes

- Start with a working branch: `git checkout -b feature/extract-media-library`
- Test thoroughly before merging
- Update both client-manager and SEO God to use extracted component
- Remove duplicate code from both apps after extraction
- Document any breaking changes in CHANGELOG.md
