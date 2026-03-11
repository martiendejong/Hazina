// src/components/MediaLibrary.tsx
import { useState, useCallback, useEffect, useRef } from "react";
import { Upload, Search, Grid3X3, List, Trash2, Image, Video, FileText, X, Check, Layers, Download, Loader2 } from "lucide-react";
import { Fragment, jsx, jsxs } from "react/jsx-runtime";
function MediaLibrary({ config }) {
  const getContextId = useCallback(() => {
    return typeof config.contextId === "function" ? config.contextId() : config.contextId;
  }, [config.contextId]);
  const contextId = getContextId();
  const platformPresets = config.ui?.platformPresets || [
    { platform: "linkedin", width: 1200, height: 627, label: "LinkedIn" },
    { platform: "instagram", width: 1080, height: 1080, label: "Instagram" },
    { platform: "twitter", width: 1200, height: 675, label: "Twitter/X" },
    { platform: "facebook", width: 1200, height: 630, label: "Facebook" },
    { platform: "thumbnail", width: 300, height: 300, label: "Thumbnail" }
  ];
  const pageSize = config.ui?.pageSize || 50;
  const defaultView = config.ui?.defaultView || "grid";
  const [assets, setAssets] = useState([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const [viewMode, setViewMode] = useState(defaultView);
  const [selectedIds, setSelectedIds] = useState(/* @__PURE__ */ new Set());
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [detailAsset, setDetailAsset] = useState(null);
  const [variants, setVariants] = useState([]);
  const [selectedPlatforms, setSelectedPlatforms] = useState(/* @__PURE__ */ new Set());
  const [isGenerating, setIsGenerating] = useState(false);
  const fileInputRef = useRef(null);
  const searchTimeoutRef = useRef();
  const features = {
    upload: config.features?.upload !== false,
    delete: config.features?.delete !== false,
    multiSelect: config.features?.multiSelect !== false,
    variants: config.features?.variants !== false,
    search: config.features?.search !== false,
    pagination: config.features?.pagination !== false
  };
  const fetchAssets = useCallback(async () => {
    if (!contextId) return;
    setIsLoading(true);
    try {
      const data = await config.service.list(contextId, {
        search: search || void 0,
        type: typeFilter === "all" ? void 0 : typeFilter,
        page,
        pageSize
      });
      setAssets(data.items);
      setTotal(data.total);
      setTotalPages(data.totalPages);
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err));
      console.error("Failed to load media:", error);
      config.onError?.(error);
    } finally {
      setIsLoading(false);
    }
  }, [contextId, search, typeFilter, page, pageSize, config]);
  useEffect(() => {
    fetchAssets();
  }, [fetchAssets]);
  const handleSearchChange = (value) => {
    setSearch(value);
    if (searchTimeoutRef.current) clearTimeout(searchTimeoutRef.current);
    searchTimeoutRef.current = setTimeout(() => {
      setPage(1);
    }, 300);
  };
  const handleUpload = async (files) => {
    if (!contextId || !files.length) return;
    setIsUploading(true);
    setUploadProgress(0);
    try {
      const uploaded = await config.service.upload(contextId, Array.from(files), {
        onProgress: (pct) => setUploadProgress(pct)
      });
      config.onUpload?.(uploaded);
      await fetchAssets();
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err));
      console.error("Upload failed:", error);
      config.onError?.(error);
    } finally {
      setIsUploading(false);
      setUploadProgress(0);
    }
  };
  const handleDrop = useCallback((e) => {
    e.preventDefault();
    setIsDragOver(false);
    if (e.dataTransfer.files.length > 0) {
      handleUpload(e.dataTransfer.files);
    }
  }, [contextId]);
  const handleDragOver = useCallback((e) => {
    e.preventDefault();
    setIsDragOver(true);
  }, []);
  const handleDragLeave = useCallback((e) => {
    e.preventDefault();
    setIsDragOver(false);
  }, []);
  const toggleSelect = (id) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };
  const handleBulkDelete = async () => {
    if (!contextId || selectedIds.size === 0) return;
    if (!confirm(`Delete ${selectedIds.size} selected item(s)?`)) return;
    try {
      const ids = Array.from(selectedIds);
      await config.service.delete(contextId, ids);
      config.onDelete?.(ids);
      setSelectedIds(/* @__PURE__ */ new Set());
      await fetchAssets();
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err));
      console.error("Bulk delete failed:", error);
      config.onError?.(error);
    }
  };
  const handleAssetClick = (asset) => {
    if (config.onSelect) {
      config.onSelect(asset);
    } else if (features.multiSelect) {
      toggleSelect(asset.id);
    }
  };
  const openDetail = async (asset) => {
    setDetailAsset(asset);
    setVariants([]);
    setSelectedPlatforms(/* @__PURE__ */ new Set());
    if (contextId && asset.mimeType.startsWith("image/") && features.variants) {
      try {
        const result = await config.service.getVariants(contextId, asset.id);
        setVariants(result);
      } catch (err) {
      }
    }
  };
  const closeDetail = () => {
    setDetailAsset(null);
    setVariants([]);
    setSelectedPlatforms(/* @__PURE__ */ new Set());
  };
  const togglePlatform = (platform) => {
    setSelectedPlatforms((prev) => {
      const next = new Set(prev);
      if (next.has(platform)) next.delete(platform);
      else next.add(platform);
      return next;
    });
  };
  const handleGenerateVariants = async () => {
    if (!contextId || !detailAsset || selectedPlatforms.size === 0) return;
    setIsGenerating(true);
    try {
      const result = await config.service.generateVariants(contextId, detailAsset.id, Array.from(selectedPlatforms));
      setVariants(result);
      setSelectedPlatforms(/* @__PURE__ */ new Set());
      await fetchAssets();
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err));
      console.error("Failed to generate variants:", error);
      config.onError?.(error);
    } finally {
      setIsGenerating(false);
    }
  };
  const getTypeIcon = (mimeType) => {
    if (mimeType.startsWith("image/")) return /* @__PURE__ */ jsx(Image, { className: "w-4 h-4" });
    if (mimeType.startsWith("video/")) return /* @__PURE__ */ jsx(Video, { className: "w-4 h-4" });
    return /* @__PURE__ */ jsx(FileText, { className: "w-4 h-4" });
  };
  const formatFileSize = (bytes) => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };
  if (!contextId) {
    return /* @__PURE__ */ jsxs("div", { className: "p-8 text-center text-muted-foreground", children: [
      "No ",
      config.contextType,
      " selected. Please select a ",
      config.contextType,
      " to view media."
    ] });
  }
  return /* @__PURE__ */ jsxs(
    "div",
    {
      className: "flex flex-col h-full",
      onDrop: features.upload ? handleDrop : void 0,
      onDragOver: features.upload ? handleDragOver : void 0,
      onDragLeave: features.upload ? handleDragLeave : void 0,
      children: [
        /* @__PURE__ */ jsxs("div", { className: "flex items-center justify-between gap-4 p-4 border-b border-border", children: [
          /* @__PURE__ */ jsx("h2", { className: "text-lg font-semibold", children: "Media Library" }),
          /* @__PURE__ */ jsxs("div", { className: "flex items-center gap-2", children: [
            features.delete && features.multiSelect && selectedIds.size > 0 && /* @__PURE__ */ jsxs(
              "button",
              {
                onClick: handleBulkDelete,
                className: "flex items-center gap-1 px-3 py-1.5 text-sm bg-red-500/10 text-red-500 rounded-lg hover:bg-red-500/20 transition",
                children: [
                  /* @__PURE__ */ jsx(Trash2, { className: "w-4 h-4" }),
                  "Delete (",
                  selectedIds.size,
                  ")"
                ]
              }
            ),
            features.upload && /* @__PURE__ */ jsxs(Fragment, { children: [
              /* @__PURE__ */ jsxs(
                "button",
                {
                  onClick: () => fileInputRef.current?.click(),
                  className: "flex items-center gap-1 px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-lg hover:opacity-90 transition",
                  children: [
                    /* @__PURE__ */ jsx(Upload, { className: "w-4 h-4" }),
                    "Upload"
                  ]
                }
              ),
              /* @__PURE__ */ jsx(
                "input",
                {
                  ref: fileInputRef,
                  type: "file",
                  multiple: true,
                  accept: config.ui?.allowedFileTypes?.join(","),
                  className: "hidden",
                  onChange: (e) => e.target.files && handleUpload(e.target.files)
                }
              )
            ] })
          ] })
        ] }),
        /* @__PURE__ */ jsxs("div", { className: "flex items-center gap-3 px-4 py-2 border-b border-border", children: [
          features.search && /* @__PURE__ */ jsxs("div", { className: "relative flex-1 max-w-sm", children: [
            /* @__PURE__ */ jsx(Search, { className: "absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" }),
            /* @__PURE__ */ jsx(
              "input",
              {
                type: "text",
                placeholder: "Search media...",
                value: search,
                onChange: (e) => handleSearchChange(e.target.value),
                className: "w-full pl-9 pr-3 py-1.5 text-sm rounded-lg bg-muted border border-border focus:outline-none focus:ring-2 focus:ring-primary/50"
              }
            )
          ] }),
          /* @__PURE__ */ jsx("div", { className: "flex items-center gap-1 rounded-lg border border-border p-0.5", children: ["all", "image", "video", "document"].map((t) => /* @__PURE__ */ jsx(
            "button",
            {
              onClick: () => {
                setTypeFilter(t);
                setPage(1);
              },
              className: `px-2.5 py-1 text-xs rounded-md transition ${typeFilter === t ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:text-foreground"}`,
              children: t.charAt(0).toUpperCase() + t.slice(1)
            },
            t
          )) }),
          /* @__PURE__ */ jsxs("div", { className: "flex items-center gap-1 ml-auto", children: [
            /* @__PURE__ */ jsx(
              "button",
              {
                onClick: () => setViewMode("grid"),
                className: `p-1.5 rounded ${viewMode === "grid" ? "text-primary" : "text-muted-foreground"}`,
                children: /* @__PURE__ */ jsx(Grid3X3, { className: "w-4 h-4" })
              }
            ),
            /* @__PURE__ */ jsx(
              "button",
              {
                onClick: () => setViewMode("list"),
                className: `p-1.5 rounded ${viewMode === "list" ? "text-primary" : "text-muted-foreground"}`,
                children: /* @__PURE__ */ jsx(List, { className: "w-4 h-4" })
              }
            ),
            /* @__PURE__ */ jsxs("span", { className: "text-xs text-muted-foreground ml-2", children: [
              total,
              " items"
            ] })
          ] })
        ] }),
        isUploading && /* @__PURE__ */ jsx("div", { className: "px-4 py-2 border-b border-border", children: /* @__PURE__ */ jsxs("div", { className: "flex items-center gap-2 text-sm text-muted-foreground", children: [
          /* @__PURE__ */ jsx("div", { className: "flex-1 h-1.5 bg-muted rounded-full overflow-hidden", children: /* @__PURE__ */ jsx(
            "div",
            {
              className: "h-full bg-primary rounded-full transition-all",
              style: { width: `${uploadProgress}%` }
            }
          ) }),
          /* @__PURE__ */ jsxs("span", { children: [
            uploadProgress,
            "%"
          ] })
        ] }) }),
        features.upload && isDragOver && /* @__PURE__ */ jsx("div", { className: "absolute inset-0 z-50 flex items-center justify-center bg-primary/10 border-2 border-dashed border-primary rounded-lg pointer-events-none", children: /* @__PURE__ */ jsxs("div", { className: "flex flex-col items-center gap-2", children: [
          /* @__PURE__ */ jsx(Upload, { className: "w-8 h-8 text-primary" }),
          /* @__PURE__ */ jsx("span", { className: "text-sm font-medium text-primary", children: "Drop files to upload" })
        ] }) }),
        /* @__PURE__ */ jsxs("div", { className: "flex-1 overflow-auto p-4", children: [
          isLoading ? /* @__PURE__ */ jsx("div", { className: "flex items-center justify-center h-32", children: /* @__PURE__ */ jsx("div", { className: "w-6 h-6 border-2 border-primary border-t-transparent rounded-full animate-spin" }) }) : assets.length === 0 ? /* @__PURE__ */ jsxs("div", { className: "flex flex-col items-center justify-center h-48 text-muted-foreground", children: [
            /* @__PURE__ */ jsx(Image, { className: "w-12 h-12 mb-3 opacity-30" }),
            /* @__PURE__ */ jsx("p", { className: "text-sm", children: "No media found" }),
            /* @__PURE__ */ jsx("p", { className: "text-xs mt-1", children: "Upload files or adjust your search" })
          ] }) : viewMode === "grid" ? /* @__PURE__ */ jsx("div", { className: "grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3", children: assets.map((asset) => /* @__PURE__ */ jsxs(
            "div",
            {
              onClick: () => handleAssetClick(asset),
              className: `group relative rounded-lg overflow-hidden border cursor-pointer transition ${selectedIds.has(asset.id) ? "border-primary ring-2 ring-primary/30" : "border-border hover:border-primary/50"}`,
              children: [
                /* @__PURE__ */ jsx("div", { className: "aspect-square bg-muted flex items-center justify-center overflow-hidden", children: asset.mimeType.startsWith("image/") ? /* @__PURE__ */ jsx(
                  "img",
                  {
                    src: asset.thumbnailUrl || asset.url,
                    alt: asset.metadata?.altText || asset.fileName,
                    className: "w-full h-full object-cover",
                    loading: "lazy"
                  }
                ) : /* @__PURE__ */ jsxs("div", { className: "flex flex-col items-center gap-1 text-muted-foreground", children: [
                  getTypeIcon(asset.mimeType),
                  /* @__PURE__ */ jsx("span", { className: "text-xs", children: asset.mimeType.split("/")[1] })
                ] }) }),
                features.multiSelect && selectedIds.has(asset.id) && /* @__PURE__ */ jsx("div", { className: "absolute top-2 right-2 w-5 h-5 rounded-full bg-primary flex items-center justify-center", children: /* @__PURE__ */ jsx(Check, { className: "w-3 h-3 text-primary-foreground" }) }),
                features.variants && asset.mimeType.startsWith("image/") && /* @__PURE__ */ jsx(
                  "button",
                  {
                    onClick: (e) => {
                      e.stopPropagation();
                      openDetail(asset);
                    },
                    className: "absolute top-2 left-2 w-6 h-6 rounded bg-black/60 flex items-center justify-center opacity-0 group-hover:opacity-100 transition",
                    title: "Manage variants",
                    children: /* @__PURE__ */ jsx(Layers, { className: "w-3.5 h-3.5 text-white" })
                  }
                ),
                /* @__PURE__ */ jsxs("div", { className: "p-2", children: [
                  /* @__PURE__ */ jsx("p", { className: "text-xs font-medium truncate", children: asset.fileName }),
                  /* @__PURE__ */ jsx("p", { className: "text-xs text-muted-foreground", children: formatFileSize(asset.fileSize) })
                ] })
              ]
            },
            asset.id
          )) }) : /* @__PURE__ */ jsx("div", { className: "space-y-1", children: assets.map((asset) => /* @__PURE__ */ jsxs(
            "div",
            {
              onClick: () => handleAssetClick(asset),
              className: `flex items-center gap-3 px-3 py-2 rounded-lg cursor-pointer transition ${selectedIds.has(asset.id) ? "bg-primary/10 border border-primary/30" : "hover:bg-muted border border-transparent"}`,
              children: [
                /* @__PURE__ */ jsx("div", { className: "w-10 h-10 rounded bg-muted flex items-center justify-center overflow-hidden flex-shrink-0", children: asset.mimeType.startsWith("image/") ? /* @__PURE__ */ jsx("img", { src: asset.thumbnailUrl || asset.url, alt: "", className: "w-full h-full object-cover", loading: "lazy" }) : getTypeIcon(asset.mimeType) }),
                /* @__PURE__ */ jsxs("div", { className: "flex-1 min-w-0", children: [
                  /* @__PURE__ */ jsx("p", { className: "text-sm truncate", children: asset.fileName }),
                  /* @__PURE__ */ jsxs("p", { className: "text-xs text-muted-foreground", children: [
                    formatFileSize(asset.fileSize),
                    asset.width && asset.height && ` \xB7 ${asset.width}\xD7${asset.height}`
                  ] })
                ] }),
                features.variants && asset.mimeType.startsWith("image/") && /* @__PURE__ */ jsx(
                  "button",
                  {
                    onClick: (e) => {
                      e.stopPropagation();
                      openDetail(asset);
                    },
                    className: "p-1.5 rounded hover:bg-muted-foreground/10 text-muted-foreground hover:text-foreground transition flex-shrink-0",
                    title: "Manage variants",
                    children: /* @__PURE__ */ jsx(Layers, { className: "w-4 h-4" })
                  }
                ),
                features.multiSelect && selectedIds.has(asset.id) && /* @__PURE__ */ jsx(Check, { className: "w-4 h-4 text-primary flex-shrink-0" })
              ]
            },
            asset.id
          )) }),
          features.pagination && totalPages > 1 && /* @__PURE__ */ jsxs("div", { className: "flex items-center justify-center gap-2 mt-4", children: [
            /* @__PURE__ */ jsx(
              "button",
              {
                onClick: () => setPage((p) => Math.max(1, p - 1)),
                disabled: page === 1,
                className: "px-3 py-1 text-sm rounded border border-border disabled:opacity-30",
                children: "Prev"
              }
            ),
            /* @__PURE__ */ jsxs("span", { className: "text-sm text-muted-foreground", children: [
              "Page ",
              page,
              " of ",
              totalPages
            ] }),
            /* @__PURE__ */ jsx(
              "button",
              {
                onClick: () => setPage((p) => Math.min(totalPages, p + 1)),
                disabled: page === totalPages,
                className: "px-3 py-1 text-sm rounded border border-border disabled:opacity-30",
                children: "Next"
              }
            )
          ] })
        ] }),
        features.variants && detailAsset && /* @__PURE__ */ jsx("div", { className: "fixed inset-0 z-50 flex items-center justify-center bg-black/50", onClick: closeDetail, children: /* @__PURE__ */ jsxs(
          "div",
          {
            className: "bg-background rounded-xl shadow-2xl w-full max-w-2xl max-h-[80vh] overflow-auto border border-border",
            onClick: (e) => e.stopPropagation(),
            children: [
              /* @__PURE__ */ jsxs("div", { className: "flex items-center justify-between p-4 border-b border-border", children: [
                /* @__PURE__ */ jsxs("div", { className: "flex items-center gap-3", children: [
                  /* @__PURE__ */ jsx(Layers, { className: "w-5 h-5 text-primary" }),
                  /* @__PURE__ */ jsxs("div", { children: [
                    /* @__PURE__ */ jsx("h3", { className: "font-semibold text-sm", children: "Platform Variants" }),
                    /* @__PURE__ */ jsx("p", { className: "text-xs text-muted-foreground truncate max-w-xs", children: detailAsset.fileName })
                  ] })
                ] }),
                /* @__PURE__ */ jsx("button", { onClick: closeDetail, className: "p-1.5 rounded hover:bg-muted transition", children: /* @__PURE__ */ jsx(X, { className: "w-4 h-4" }) })
              ] }),
              /* @__PURE__ */ jsx("div", { className: "p-4 border-b border-border", children: /* @__PURE__ */ jsxs("div", { className: "flex gap-4", children: [
                /* @__PURE__ */ jsx("div", { className: "w-32 h-32 rounded-lg overflow-hidden bg-muted flex-shrink-0", children: /* @__PURE__ */ jsx("img", { src: detailAsset.url, alt: detailAsset.metadata?.altText || "", className: "w-full h-full object-cover" }) }),
                /* @__PURE__ */ jsxs("div", { className: "flex-1 text-sm space-y-1", children: [
                  /* @__PURE__ */ jsxs("p", { children: [
                    /* @__PURE__ */ jsx("span", { className: "text-muted-foreground", children: "Size:" }),
                    " ",
                    formatFileSize(detailAsset.fileSize)
                  ] }),
                  detailAsset.width && detailAsset.height && /* @__PURE__ */ jsxs("p", { children: [
                    /* @__PURE__ */ jsx("span", { className: "text-muted-foreground", children: "Dimensions:" }),
                    " ",
                    detailAsset.width,
                    " x ",
                    detailAsset.height
                  ] }),
                  /* @__PURE__ */ jsxs("p", { children: [
                    /* @__PURE__ */ jsx("span", { className: "text-muted-foreground", children: "Type:" }),
                    " ",
                    detailAsset.mimeType
                  ] })
                ] })
              ] }) }),
              variants.length > 0 && /* @__PURE__ */ jsxs("div", { className: "p-4 border-b border-border", children: [
                /* @__PURE__ */ jsx("h4", { className: "text-xs font-medium text-muted-foreground uppercase tracking-wider mb-3", children: "Existing Variants" }),
                /* @__PURE__ */ jsx("div", { className: "grid grid-cols-2 sm:grid-cols-3 gap-2", children: variants.map((v) => /* @__PURE__ */ jsxs("div", { className: "rounded-lg border border-border p-2 bg-muted/30", children: [
                  /* @__PURE__ */ jsx("div", { className: "aspect-video rounded overflow-hidden bg-muted mb-2", children: /* @__PURE__ */ jsx("img", { src: v.url, alt: `${v.platform} variant`, className: "w-full h-full object-cover" }) }),
                  /* @__PURE__ */ jsxs("div", { className: "flex items-center justify-between", children: [
                    /* @__PURE__ */ jsxs("div", { children: [
                      /* @__PURE__ */ jsx("p", { className: "text-xs font-medium", children: v.platform }),
                      /* @__PURE__ */ jsxs("p", { className: "text-xs text-muted-foreground", children: [
                        v.width,
                        " x ",
                        v.height
                      ] })
                    ] }),
                    /* @__PURE__ */ jsx(
                      "a",
                      {
                        href: v.url,
                        download: true,
                        className: "p-1 rounded hover:bg-muted transition",
                        title: "Download variant",
                        children: /* @__PURE__ */ jsx(Download, { className: "w-3.5 h-3.5 text-muted-foreground" })
                      }
                    )
                  ] })
                ] }, v.platform)) })
              ] }),
              /* @__PURE__ */ jsxs("div", { className: "p-4", children: [
                /* @__PURE__ */ jsx("h4", { className: "text-xs font-medium text-muted-foreground uppercase tracking-wider mb-3", children: "Generate Variants" }),
                /* @__PURE__ */ jsx("div", { className: "space-y-2 mb-4", children: platformPresets.map((preset) => {
                  const exists = variants.some((v) => v.platform === preset.platform);
                  return /* @__PURE__ */ jsxs(
                    "label",
                    {
                      className: `flex items-center gap-3 px-3 py-2 rounded-lg border cursor-pointer transition ${selectedPlatforms.has(preset.platform) ? "border-primary bg-primary/5" : "border-border hover:border-primary/30"}`,
                      children: [
                        /* @__PURE__ */ jsx(
                          "input",
                          {
                            type: "checkbox",
                            checked: selectedPlatforms.has(preset.platform),
                            onChange: () => togglePlatform(preset.platform),
                            className: "rounded border-border"
                          }
                        ),
                        /* @__PURE__ */ jsxs("div", { className: "flex-1", children: [
                          /* @__PURE__ */ jsx("span", { className: "text-sm font-medium", children: preset.label }),
                          /* @__PURE__ */ jsxs("span", { className: "text-xs text-muted-foreground ml-2", children: [
                            preset.width,
                            " x ",
                            preset.height
                          ] })
                        ] }),
                        exists && /* @__PURE__ */ jsx("span", { className: "text-xs px-1.5 py-0.5 rounded bg-green-500/10 text-green-500", children: "exists" })
                      ]
                    },
                    preset.platform
                  );
                }) }),
                /* @__PURE__ */ jsx(
                  "button",
                  {
                    onClick: handleGenerateVariants,
                    disabled: selectedPlatforms.size === 0 || isGenerating,
                    className: "w-full flex items-center justify-center gap-2 px-4 py-2 text-sm bg-primary text-primary-foreground rounded-lg hover:opacity-90 transition disabled:opacity-50",
                    children: isGenerating ? /* @__PURE__ */ jsxs(Fragment, { children: [
                      /* @__PURE__ */ jsx(Loader2, { className: "w-4 h-4 animate-spin" }),
                      "Generating..."
                    ] }) : /* @__PURE__ */ jsxs(Fragment, { children: [
                      /* @__PURE__ */ jsx(Layers, { className: "w-4 h-4" }),
                      "Generate ",
                      selectedPlatforms.size > 0 ? `(${selectedPlatforms.size})` : ""
                    ] })
                  }
                )
              ] })
            ]
          }
        ) })
      ]
    }
  );
}
export {
  MediaLibrary
};
