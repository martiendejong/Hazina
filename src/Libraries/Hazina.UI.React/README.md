# @hazina/ui-react

Reusable React UI components for the Hazina ecosystem.

## Components

### MediaLibrary

A production-tested 567-line media management component extracted from client-manager.

**Features:**
- Upload (drag & drop, file browser)
- Grid/List view modes
- Search & filtering (all/image/video/document)
- Multi-select & bulk delete
- Platform-specific variant generation (LinkedIn, Instagram, Twitter, Facebook)
- Detail panel with metadata
- Pagination

## Installation

### Option 1: NPM Package (Recommended)

```bash
npm install @hazina/ui-react
# or
yarn add @hazina/ui-react
```

### Option 2: Git Submodule

```bash
cd your-project
git submodule add https://github.com/your-org/hazina.git packages/hazina
cd packages/hazina/packages/ui-react
npm install
npm run build
```

## Usage

### Basic Setup

```typescript
import { MediaLibrary } from '@hazina/ui-react'
import type { MediaLibraryConfig, MediaServiceAdapter } from '@hazina/ui-react/types'

// 1. Create service adapter for your API
const mediaService: MediaServiceAdapter = {
  list: async (contextId, options) => {
    const res = await fetch(`/api/media/${contextId}?${new URLSearchParams(options)}`)
    return res.json()
  },

  upload: async (contextId, files, options) => {
    const formData = new FormData()
    files.forEach(f => formData.append('files', f))

    const res = await fetch(`/api/media/${contextId}/upload`, {
      method: 'POST',
      body: formData
    })
    return res.json()
  },

  delete: async (contextId, assetIds) => {
    await fetch(`/api/media/${contextId}/bulk-delete`, {
      method: 'POST',
      body: JSON.stringify({ ids: assetIds }),
      headers: { 'Content-Type': 'application/json' }
    })
  },

  getVariants: async (contextId, assetId) => {
    const res = await fetch(`/api/media/${contextId}/${assetId}/variants`)
    return res.json()
  },

  generateVariants: async (contextId, assetId, platforms) => {
    const res = await fetch(`/api/media/${contextId}/${assetId}/variants`, {
      method: 'POST',
      body: JSON.stringify({ platforms }),
      headers: { 'Content-Type': 'application/json' }
    })
    return res.json()
  }
}

// 2. Configure MediaLibrary
const config: MediaLibraryConfig = {
  contextType: 'project',  // or 'website'
  contextId: projectId,    // from your app state

  apiUrl: '/api/media',
  getAuthToken: () => localStorage.getItem('token'),

  service: mediaService,   // inject your adapter

  features: {
    upload: true,
    delete: true,
    multiSelect: true,
    variants: true,
    search: true,
    pagination: true
  },

  ui: {
    defaultView: 'grid',
    pageSize: 50,
    platformPresets: [
      { platform: 'linkedin', width: 1200, height: 627, label: 'LinkedIn' },
      { platform: 'instagram', width: 1080, height: 1080, label: 'Instagram' },
      { platform: 'twitter', width: 1200, height: 675, label: 'Twitter/X' },
      { platform: 'facebook', width: 1200, height: 630, label: 'Facebook' },
    ]
  },

  onSelect: (asset) => console.log('Selected:', asset),
  onUpload: (assets) => console.log('Uploaded:', assets),
  onError: (error) => console.error('Error:', error)
}

// 3. Use component
function MyApp() {
  return <MediaLibrary config={config} />
}
```

### SEO God Integration Example

```typescript
// SEO God uses websiteId instead of projectId
const config: MediaLibraryConfig = {
  contextType: 'website',
  contextId: () => websiteStore.selectedWebsiteId,  // dynamic getter

  apiUrl: '/api/media',
  uploadEndpoint: '/api/media/:contextId/upload',  // custom endpoints
  listEndpoint: '/api/media/:contextId',

  // AI features (optional)
  ai: {
    enabled: true,
    generateTitle: async (asset) => {
      const res = await fetch(`/api/ai-metadata/${asset.id}/title`)
      return res.json()
    },
    generateAltText: async (asset) => {
      const res = await fetch(`/api/ai-metadata/${asset.id}/alt`)
      return res.json()
    }
  }
}
```

### Client-Manager Integration Example

```typescript
// Client-manager uses projectId (original implementation)
import { useProject } from './stores/projectStore'
import mediaService from './services/media'

function MediaPanel() {
  const { project } = useProject()

  const config: MediaLibraryConfig = {
    contextType: 'project',
    contextId: project?.id || '',

    service: mediaService,  // reuse existing service

    onSelect: (asset) => {
      // Handle asset selection for panels
    }
  }

  return <MediaLibrary config={config} />
}
```

## API Requirements

Your backend must implement these endpoints:

```
GET    /api/media/:contextId?search=...&type=...&page=...&pageSize=...
POST   /api/media/:contextId/upload (multipart/form-data)
DELETE /api/media/:contextId/bulk-delete (JSON: { ids: string[] })
GET    /api/media/:contextId/:assetId/variants
POST   /api/media/:contextId/:assetId/variants (JSON: { platforms: string[] })
```

## Development

```bash
# Install dependencies
npm install

# Build package
npm run build

# Watch mode
npm run dev
```

## Migration from client-manager

If you have an existing MediaLibrary component:

1. Install @hazina/ui-react
2. Replace hardcoded projectId with config.contextId
3. Inject mediaService via config.service
4. Update imports: `import { MediaLibrary } from '@hazina/ui-react'`
5. Pass config prop instead of onSelect/selectable

## License

MIT
