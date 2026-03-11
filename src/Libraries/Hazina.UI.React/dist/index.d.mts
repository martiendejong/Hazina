import * as react_jsx_runtime from 'react/jsx-runtime';

interface MediaAsset {
    id: string;
    fileName: string;
    filePath: string;
    url: string;
    thumbnailUrl?: string;
    mimeType: string;
    fileSize: number;
    width?: number;
    height?: number;
    uploadedAt: string;
    metadata?: Record<string, any>;
}
interface MediaVariant {
    id: string;
    platform: string;
    width: number;
    height: number;
    url: string;
    thumbnailUrl?: string;
}
interface MediaListResponse {
    items: MediaAsset[];
    total: number;
    page: number;
    pageSize: number;
    totalPages: number;
}
interface MediaUploadOptions {
    onProgress?: (percentage: number) => void;
    metadata?: Record<string, any>;
}
interface MediaListOptions {
    search?: string;
    type?: 'all' | 'image' | 'video' | 'document';
    page?: number;
    pageSize?: number;
}
interface MediaLibraryConfig {
    contextType: 'project' | 'website' | 'custom';
    contextId: string | (() => string);
    service: MediaServiceAdapter;
    apiUrl: string;
    uploadEndpoint?: string;
    listEndpoint?: string;
    deleteEndpoint?: string;
    variantsEndpoint?: string;
    getAuthToken?: () => string | null;
    features?: {
        upload?: boolean;
        delete?: boolean;
        multiSelect?: boolean;
        variants?: boolean;
        search?: boolean;
        pagination?: boolean;
    };
    ai?: {
        generateTitle?: (asset: MediaAsset) => Promise<string>;
        generateAltText?: (asset: MediaAsset) => Promise<string>;
        generateSummary?: (asset: MediaAsset) => Promise<string>;
        enabled?: boolean;
    };
    ui?: {
        defaultView?: 'grid' | 'list';
        pageSize?: number;
        allowedFileTypes?: string[];
        maxFileSize?: number;
        platformPresets?: Array<{
            platform: string;
            width: number;
            height: number;
            label: string;
        }>;
    };
    onSelect?: (asset: MediaAsset) => void;
    onUpload?: (assets: MediaAsset[]) => void;
    onDelete?: (assetIds: string[]) => void;
    onError?: (error: Error) => void;
}
interface MediaServiceAdapter {
    list: (contextId: string, options?: MediaListOptions) => Promise<MediaListResponse>;
    upload: (contextId: string, files: File[], options?: MediaUploadOptions) => Promise<MediaAsset[]>;
    delete: (contextId: string, assetIds: string[]) => Promise<void>;
    getVariants: (contextId: string, assetId: string) => Promise<MediaVariant[]>;
    generateVariants: (contextId: string, assetId: string, platforms: string[]) => Promise<MediaVariant[]>;
}

interface MediaLibraryProps {
    config: MediaLibraryConfig;
}
declare function MediaLibrary({ config }: MediaLibraryProps): react_jsx_runtime.JSX.Element;

export { type MediaAsset, MediaLibrary, type MediaLibraryConfig, type MediaListOptions, type MediaListResponse, type MediaServiceAdapter, type MediaUploadOptions, type MediaVariant };
