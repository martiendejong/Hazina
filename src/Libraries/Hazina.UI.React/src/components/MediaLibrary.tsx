import { useState, useCallback, useEffect, useRef } from 'react'
import type {
  MediaLibraryConfig,
  MediaAsset,
  MediaListResponse,
  MediaVariant
} from '../types/media'
import { Upload, Search, Grid3X3, List, Trash2, Image, Video, FileText, X, Check, Layers, Download, Loader2 } from 'lucide-react'

type ViewMode = 'grid' | 'list'
type TypeFilter = 'all' | 'image' | 'video' | 'document'

interface MediaLibraryProps {
  config: MediaLibraryConfig
}

export default function MediaLibrary({ config }: MediaLibraryProps) {
  // Get contextId (supports both static and dynamic)
  const getContextId = useCallback(() => {
    return typeof config.contextId === 'function'
      ? config.contextId()
      : config.contextId
  }, [config.contextId])

  const contextId = getContextId()

  // Platform presets (configurable via config or use defaults)
  const platformPresets = config.ui?.platformPresets || [
    { platform: 'linkedin', width: 1200, height: 627, label: 'LinkedIn' },
    { platform: 'instagram', width: 1080, height: 1080, label: 'Instagram' },
    { platform: 'twitter', width: 1200, height: 675, label: 'Twitter/X' },
    { platform: 'facebook', width: 1200, height: 630, label: 'Facebook' },
    { platform: 'thumbnail', width: 300, height: 300, label: 'Thumbnail' },
  ]

  const pageSize = config.ui?.pageSize || 50
  const defaultView = config.ui?.defaultView || 'grid'

  const [assets, setAssets] = useState<MediaAsset[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState<TypeFilter>('all')
  const [viewMode, setViewMode] = useState<ViewMode>(defaultView)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [isUploading, setIsUploading] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)
  const [isDragOver, setIsDragOver] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [detailAsset, setDetailAsset] = useState<MediaAsset | null>(null)
  const [variants, setVariants] = useState<MediaVariant[]>([])
  const [selectedPlatforms, setSelectedPlatforms] = useState<Set<string>>(new Set())
  const [isGenerating, setIsGenerating] = useState(false)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const searchTimeoutRef = useRef<ReturnType<typeof setTimeout>>()

  // Feature flags
  const features = {
    upload: config.features?.upload !== false,
    delete: config.features?.delete !== false,
    multiSelect: config.features?.multiSelect !== false,
    variants: config.features?.variants !== false,
    search: config.features?.search !== false,
    pagination: config.features?.pagination !== false,
  }

  const fetchAssets = useCallback(async () => {
    if (!contextId) return
    setIsLoading(true)
    try {
      const data: MediaListResponse = await config.service.list(contextId, {
        search: search || undefined,
        type: typeFilter === 'all' ? undefined : typeFilter,
        page,
        pageSize
      })
      setAssets(data.items)
      setTotal(data.total)
      setTotalPages(data.totalPages)
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err))
      console.error('Failed to load media:', error)
      config.onError?.(error)
    } finally {
      setIsLoading(false)
    }
  }, [contextId, search, typeFilter, page, pageSize, config])

  useEffect(() => {
    fetchAssets()
  }, [fetchAssets])

  const handleSearchChange = (value: string) => {
    setSearch(value)
    if (searchTimeoutRef.current) clearTimeout(searchTimeoutRef.current)
    searchTimeoutRef.current = setTimeout(() => {
      setPage(1)
    }, 300)
  }

  const handleUpload = async (files: FileList | File[]) => {
    if (!contextId || !files.length) return
    setIsUploading(true)
    setUploadProgress(0)
    try {
      const uploaded = await config.service.upload(contextId, Array.from(files), {
        onProgress: (pct) => setUploadProgress(pct)
      })
      config.onUpload?.(uploaded)
      await fetchAssets()
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err))
      console.error('Upload failed:', error)
      config.onError?.(error)
    } finally {
      setIsUploading(false)
      setUploadProgress(0)
    }
  }

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(false)
    if (e.dataTransfer.files.length > 0) {
      handleUpload(e.dataTransfer.files)
    }
  }, [contextId])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    setIsDragOver(false)
  }, [])

  const toggleSelect = (id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const handleBulkDelete = async () => {
    if (!contextId || selectedIds.size === 0) return
    if (!confirm(`Delete ${selectedIds.size} selected item(s)?`)) return
    try {
      const ids = Array.from(selectedIds)
      await config.service.delete(contextId, ids)
      config.onDelete?.(ids)
      setSelectedIds(new Set())
      await fetchAssets()
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err))
      console.error('Bulk delete failed:', error)
      config.onError?.(error)
    }
  }

  const handleAssetClick = (asset: MediaAsset) => {
    if (config.onSelect) {
      config.onSelect(asset)
    } else if (features.multiSelect) {
      toggleSelect(asset.id)
    }
  }

  const openDetail = async (asset: MediaAsset) => {
    setDetailAsset(asset)
    setVariants([])
    setSelectedPlatforms(new Set())
    if (contextId && asset.mimeType.startsWith('image/') && features.variants) {
      try {
        const result = await config.service.getVariants(contextId, asset.id)
        setVariants(result)
      } catch (err) {
        // No variants yet - this is okay
      }
    }
  }

  const closeDetail = () => {
    setDetailAsset(null)
    setVariants([])
    setSelectedPlatforms(new Set())
  }

  const togglePlatform = (platform: string) => {
    setSelectedPlatforms(prev => {
      const next = new Set(prev)
      if (next.has(platform)) next.delete(platform)
      else next.add(platform)
      return next
    })
  }

  const handleGenerateVariants = async () => {
    if (!contextId || !detailAsset || selectedPlatforms.size === 0) return
    setIsGenerating(true)
    try {
      const result = await config.service.generateVariants(contextId, detailAsset.id, Array.from(selectedPlatforms))
      setVariants(result)
      setSelectedPlatforms(new Set())
      await fetchAssets()
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err))
      console.error('Failed to generate variants:', error)
      config.onError?.(error)
    } finally {
      setIsGenerating(false)
    }
  }

  const getTypeIcon = (mimeType: string) => {
    if (mimeType.startsWith('image/')) return <Image className="w-4 h-4" />
    if (mimeType.startsWith('video/')) return <Video className="w-4 h-4" />
    return <FileText className="w-4 h-4" />
  }

  const formatFileSize = (bytes: number) => {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  if (!contextId) {
    return (
      <div className="p-8 text-center text-muted-foreground">
        No {config.contextType} selected. Please select a {config.contextType} to view media.
      </div>
    )
  }

  return (
    <div
      className="flex flex-col h-full"
      onDrop={features.upload ? handleDrop : undefined}
      onDragOver={features.upload ? handleDragOver : undefined}
      onDragLeave={features.upload ? handleDragLeave : undefined}
    >
      {/* Header */}
      <div className="flex items-center justify-between gap-4 p-4 border-b border-border">
        <h2 className="text-lg font-semibold">Media Library</h2>
        <div className="flex items-center gap-2">
          {features.delete && features.multiSelect && selectedIds.size > 0 && (
            <button
              onClick={handleBulkDelete}
              className="flex items-center gap-1 px-3 py-1.5 text-sm bg-red-500/10 text-red-500 rounded-lg hover:bg-red-500/20 transition"
            >
              <Trash2 className="w-4 h-4" />
              Delete ({selectedIds.size})
            </button>
          )}
          {features.upload && (
            <>
              <button
                onClick={() => fileInputRef.current?.click()}
                className="flex items-center gap-1 px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-lg hover:opacity-90 transition"
              >
                <Upload className="w-4 h-4" />
                Upload
              </button>
              <input
                ref={fileInputRef}
                type="file"
                multiple
                accept={config.ui?.allowedFileTypes?.join(',')}
                className="hidden"
                onChange={(e) => e.target.files && handleUpload(e.target.files)}
              />
            </>
          )}
        </div>
      </div>

      {/* Toolbar */}
      <div className="flex items-center gap-3 px-4 py-2 border-b border-border">
        {features.search && (
          <div className="relative flex-1 max-w-sm">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
            <input
              type="text"
              placeholder="Search media..."
              value={search}
              onChange={(e) => handleSearchChange(e.target.value)}
              className="w-full pl-9 pr-3 py-1.5 text-sm rounded-lg bg-muted border border-border focus:outline-none focus:ring-2 focus:ring-primary/50"
            />
          </div>
        )}

        <div className="flex items-center gap-1 rounded-lg border border-border p-0.5">
          {(['all', 'image', 'video', 'document'] as TypeFilter[]).map((t) => (
            <button
              key={t}
              onClick={() => { setTypeFilter(t); setPage(1) }}
              className={`px-2.5 py-1 text-xs rounded-md transition ${
                typeFilter === t
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
            >
              {t.charAt(0).toUpperCase() + t.slice(1)}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-1 ml-auto">
          <button
            onClick={() => setViewMode('grid')}
            className={`p-1.5 rounded ${viewMode === 'grid' ? 'text-primary' : 'text-muted-foreground'}`}
          >
            <Grid3X3 className="w-4 h-4" />
          </button>
          <button
            onClick={() => setViewMode('list')}
            className={`p-1.5 rounded ${viewMode === 'list' ? 'text-primary' : 'text-muted-foreground'}`}
          >
            <List className="w-4 h-4" />
          </button>
          <span className="text-xs text-muted-foreground ml-2">{total} items</span>
        </div>
      </div>

      {/* Upload progress */}
      {isUploading && (
        <div className="px-4 py-2 border-b border-border">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <div className="flex-1 h-1.5 bg-muted rounded-full overflow-hidden">
              <div
                className="h-full bg-primary rounded-full transition-all"
                style={{ width: `${uploadProgress}%` }}
              />
            </div>
            <span>{uploadProgress}%</span>
          </div>
        </div>
      )}

      {/* Drag overlay */}
      {features.upload && isDragOver && (
        <div className="absolute inset-0 z-50 flex items-center justify-center bg-primary/10 border-2 border-dashed border-primary rounded-lg pointer-events-none">
          <div className="flex flex-col items-center gap-2">
            <Upload className="w-8 h-8 text-primary" />
            <span className="text-sm font-medium text-primary">Drop files to upload</span>
          </div>
        </div>
      )}

      {/* Content */}
      <div className="flex-1 overflow-auto p-4">
        {isLoading ? (
          <div className="flex items-center justify-center h-32">
            <div className="w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin" />
          </div>
        ) : assets.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-48 text-muted-foreground">
            <Image className="w-12 h-12 mb-3 opacity-30" />
            <p className="text-sm">No media found</p>
            <p className="text-xs mt-1">Upload files or adjust your search</p>
          </div>
        ) : viewMode === 'grid' ? (
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3">
            {assets.map((asset) => (
              <div
                key={asset.id}
                onClick={() => handleAssetClick(asset)}
                className={`group relative rounded-lg overflow-hidden border cursor-pointer transition ${
                  selectedIds.has(asset.id)
                    ? 'border-primary ring-2 ring-primary/30'
                    : 'border-border hover:border-primary/50'
                }`}
              >
                {/* Thumbnail */}
                <div className="aspect-square bg-muted flex items-center justify-center overflow-hidden">
                  {asset.mimeType.startsWith('image/') ? (
                    <img
                      src={asset.thumbnailUrl || asset.url}
                      alt={asset.metadata?.altText || asset.fileName}
                      className="w-full h-full object-cover"
                      loading="lazy"
                    />
                  ) : (
                    <div className="flex flex-col items-center gap-1 text-muted-foreground">
                      {getTypeIcon(asset.mimeType)}
                      <span className="text-xs">{asset.mimeType.split('/')[1]}</span>
                    </div>
                  )}
                </div>

                {/* Selection indicator */}
                {features.multiSelect && selectedIds.has(asset.id) && (
                  <div className="absolute top-2 right-2 w-5 h-5 rounded-full bg-primary flex items-center justify-center">
                    <Check className="w-3 h-3 text-primary-foreground" />
                  </div>
                )}

                {/* Variants button (images only) */}
                {features.variants && asset.mimeType.startsWith('image/') && (
                  <button
                    onClick={(e) => { e.stopPropagation(); openDetail(asset) }}
                    className="absolute top-2 left-2 w-6 h-6 rounded bg-black/60 flex items-center justify-center opacity-0 group-hover:opacity-100 transition"
                    title="Manage variants"
                  >
                    <Layers className="w-3.5 h-3.5 text-white" />
                  </button>
                )}

                {/* Info */}
                <div className="p-2">
                  <p className="text-xs font-medium truncate">{asset.fileName}</p>
                  <p className="text-xs text-muted-foreground">{formatFileSize(asset.fileSize)}</p>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="space-y-1">
            {assets.map((asset) => (
              <div
                key={asset.id}
                onClick={() => handleAssetClick(asset)}
                className={`flex items-center gap-3 px-3 py-2 rounded-lg cursor-pointer transition ${
                  selectedIds.has(asset.id)
                    ? 'bg-primary/10 border border-primary/30'
                    : 'hover:bg-muted border border-transparent'
                }`}
              >
                {/* Thumbnail */}
                <div className="w-10 h-10 rounded bg-muted flex items-center justify-center overflow-hidden flex-shrink-0">
                  {asset.mimeType.startsWith('image/') ? (
                    <img src={asset.thumbnailUrl || asset.url} alt="" className="w-full h-full object-cover" loading="lazy" />
                  ) : (
                    getTypeIcon(asset.mimeType)
                  )}
                </div>

                <div className="flex-1 min-w-0">
                  <p className="text-sm truncate">{asset.fileName}</p>
                  <p className="text-xs text-muted-foreground">
                    {formatFileSize(asset.fileSize)}
                    {asset.width && asset.height && ` · ${asset.width}×${asset.height}`}
                  </p>
                </div>

                {features.variants && asset.mimeType.startsWith('image/') && (
                  <button
                    onClick={(e) => { e.stopPropagation(); openDetail(asset) }}
                    className="p-1.5 rounded hover:bg-muted-foreground/10 text-muted-foreground hover:text-foreground transition flex-shrink-0"
                    title="Manage variants"
                  >
                    <Layers className="w-4 h-4" />
                  </button>
                )}

                {features.multiSelect && selectedIds.has(asset.id) && (
                  <Check className="w-4 h-4 text-primary flex-shrink-0" />
                )}
              </div>
            ))}
          </div>
        )}

        {/* Pagination */}
        {features.pagination && totalPages > 1 && (
          <div className="flex items-center justify-center gap-2 mt-4">
            <button
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-3 py-1 text-sm rounded border border-border disabled:opacity-30"
            >
              Prev
            </button>
            <span className="text-sm text-muted-foreground">
              Page {page} of {totalPages}
            </span>
            <button
              onClick={() => setPage(p => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="px-3 py-1 text-sm rounded border border-border disabled:opacity-30"
            >
              Next
            </button>
          </div>
        )}
      </div>

      {/* Variant Detail Panel */}
      {features.variants && detailAsset && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" onClick={closeDetail}>
          <div
            className="bg-background rounded-xl shadow-2xl w-full max-w-2xl max-h-[80vh] overflow-auto border border-border"
            onClick={(e) => e.stopPropagation()}
          >
            {/* Panel Header */}
            <div className="flex items-center justify-between p-4 border-b border-border">
              <div className="flex items-center gap-3">
                <Layers className="w-5 h-5 text-primary" />
                <div>
                  <h3 className="font-semibold text-sm">Platform Variants</h3>
                  <p className="text-xs text-muted-foreground truncate max-w-xs">{detailAsset.fileName}</p>
                </div>
              </div>
              <button onClick={closeDetail} className="p-1.5 rounded hover:bg-muted transition">
                <X className="w-4 h-4" />
              </button>
            </div>

            {/* Original Preview */}
            <div className="p-4 border-b border-border">
              <div className="flex gap-4">
                <div className="w-32 h-32 rounded-lg overflow-hidden bg-muted flex-shrink-0">
                  <img src={detailAsset.url} alt={detailAsset.metadata?.altText || ''} className="w-full h-full object-cover" />
                </div>
                <div className="flex-1 text-sm space-y-1">
                  <p><span className="text-muted-foreground">Size:</span> {formatFileSize(detailAsset.fileSize)}</p>
                  {detailAsset.width && detailAsset.height && (
                    <p><span className="text-muted-foreground">Dimensions:</span> {detailAsset.width} x {detailAsset.height}</p>
                  )}
                  <p><span className="text-muted-foreground">Type:</span> {detailAsset.mimeType}</p>
                </div>
              </div>
            </div>

            {/* Existing Variants */}
            {variants.length > 0 && (
              <div className="p-4 border-b border-border">
                <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-3">Existing Variants</h4>
                <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                  {variants.map((v) => (
                    <div key={v.platform} className="rounded-lg border border-border p-2 bg-muted/30">
                      <div className="aspect-video rounded overflow-hidden bg-muted mb-2">
                        <img src={v.url} alt={`${v.platform} variant`} className="w-full h-full object-cover" />
                      </div>
                      <div className="flex items-center justify-between">
                        <div>
                          <p className="text-xs font-medium">{v.platform}</p>
                          <p className="text-xs text-muted-foreground">{v.width} x {v.height}</p>
                        </div>
                        <a
                          href={v.url}
                          download
                          className="p-1 rounded hover:bg-muted transition"
                          title="Download variant"
                        >
                          <Download className="w-3.5 h-3.5 text-muted-foreground" />
                        </a>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Generate New Variants */}
            <div className="p-4">
              <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider mb-3">Generate Variants</h4>
              <div className="space-y-2 mb-4">
                {platformPresets.map((preset) => {
                  const exists = variants.some(v => v.platform === preset.platform)
                  return (
                    <label
                      key={preset.platform}
                      className={`flex items-center gap-3 px-3 py-2 rounded-lg border cursor-pointer transition ${
                        selectedPlatforms.has(preset.platform)
                          ? 'border-primary bg-primary/5'
                          : 'border-border hover:border-primary/30'
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={selectedPlatforms.has(preset.platform)}
                        onChange={() => togglePlatform(preset.platform)}
                        className="rounded border-border"
                      />
                      <div className="flex-1">
                        <span className="text-sm font-medium">{preset.label}</span>
                        <span className="text-xs text-muted-foreground ml-2">{preset.width} x {preset.height}</span>
                      </div>
                      {exists && (
                        <span className="text-xs px-1.5 py-0.5 rounded bg-green-500/10 text-green-500">exists</span>
                      )}
                    </label>
                  )
                })}
              </div>
              <button
                onClick={handleGenerateVariants}
                disabled={selectedPlatforms.size === 0 || isGenerating}
                className="w-full flex items-center justify-center gap-2 px-4 py-2 text-sm bg-primary text-primary-foreground rounded-lg hover:opacity-90 transition disabled:opacity-50"
              >
                {isGenerating ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    Generating...
                  </>
                ) : (
                  <>
                    <Layers className="w-4 h-4" />
                    Generate {selectedPlatforms.size > 0 ? `(${selectedPlatforms.size})` : ''}
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
