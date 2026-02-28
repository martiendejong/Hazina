export interface MediaAsset {
  id: string
  fileName: string
  filePath: string
  url: string
  thumbnailUrl?: string
  mimeType: string
  fileSize: number
  width?: number
  height?: number
  uploadedAt: string
  metadata?: Record<string, any>
}

export interface MediaVariant {
  id: string
  platform: string
  width: number
  height: number
  url: string
  thumbnailUrl?: string
}

export interface MediaListResponse {
  items: MediaAsset[]
  total: number
  page: number
  pageSize: number
  totalPages: number
}

export interface MediaUploadOptions {
  onProgress?: (percentage: number) => void
  metadata?: Record<string, any>
}

export interface MediaListOptions {
  search?: string
  type?: 'all' | 'image' | 'video' | 'document'
  page?: number
  pageSize?: number
}

export interface MediaLibraryConfig {
  // Context configuration
  contextType: 'project' | 'website' | 'custom'
  contextId: string | (() => string)

  // API configuration
  apiUrl: string
  uploadEndpoint?: string
  listEndpoint?: string
  deleteEndpoint?: string
  variantsEndpoint?: string

  // Authentication
  getAuthToken?: () => string | null

  // Optional features
  features?: {
    upload?: boolean
    delete?: boolean
    multiSelect?: boolean
    variants?: boolean
    search?: boolean
    pagination?: boolean
  }

  // AI features (optional)
  ai?: {
    generateTitle?: (asset: MediaAsset) => Promise<string>
    generateAltText?: (asset: MediaAsset) => Promise<string>
    generateSummary?: (asset: MediaAsset) => Promise<string>
    enabled?: boolean
  }

  // UI customization
  ui?: {
    defaultView?: 'grid' | 'list'
    pageSize?: number
    allowedFileTypes?: string[]
    maxFileSize?: number
    platformPresets?: Array<{
      platform: string
      width: number
      height: number
      label: string
    }>
  }

  // Callbacks
  onSelect?: (asset: MediaAsset) => void
  onUpload?: (assets: MediaAsset[]) => void
  onDelete?: (assetIds: string[]) => void
  onError?: (error: Error) => void
}

export interface MediaServiceAdapter {
  list: (contextId: string, options?: MediaListOptions) => Promise<MediaListResponse>
  upload: (contextId: string, files: File[], options?: MediaUploadOptions) => Promise<MediaAsset[]>
  delete: (contextId: string, assetIds: string[]) => Promise<void>
  getVariants: (contextId: string, assetId: string) => Promise<MediaVariant[]>
  generateVariants: (contextId: string, assetId: string, platforms: string[]) => Promise<MediaVariant[]>
}
