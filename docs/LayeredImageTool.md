# LayeredImageTool - Multi-Layer AI Image Generation

## Overview

The LayeredImageTool is a sophisticated image generation system that creates complex, multi-layer images by combining AI-generated content, uploaded images, solid colors, and text. Each layer is generated sequentially with full awareness of previously generated layers, enabling coherent and harmonious compositions.

## Table of Contents

1. [Key Features](#key-features)
2. [Architecture](#architecture)
3. [Layer Types](#layer-types)
4. [Configuration](#configuration)
5. [Sequential Generation](#sequential-generation)
6. [Vision-Enhanced Context](#vision-enhanced-context)
7. [Usage Examples](#usage-examples)
8. [API Reference](#api-reference)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)

---

## Key Features

### ✅ **Per-Layer Configuration**
- **Control generation behavior**: Decide which layers to AI-generate vs. use existing images
- **Fine-grained control**: Set `shouldGenerate` per layer
- **Flexible workflow**: Mix generated and uploaded content seamlessly

### ✅ **Sequential Generation (Bottom-to-Top)**
- **Context-aware**: Each layer sees all previously generated layers
- **Coherent composition**: Layers harmonize with existing content
- **Progressive refinement**: Build complex images incrementally

### ✅ **Vision-Enhanced Context** (NEW!)
- **GPT-4 Vision analysis**: Previous layers are analyzed visually
- **Smart recommendations**: Vision AI suggests how to integrate new layers
- **6-point analysis**: Composition, layout, style, colors, lighting, cohesion
- **Optional**: Enable/disable per image (configurable cost/latency tradeoff)

### ✅ **Export Formats**
- **Paint.NET (.pdn)** - Full layer support with blend modes
- **OpenRaster (.ora)** - Cross-platform layered format
- **Photoshop (.psd)** - Industry-standard format

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│  LayeredImageDefinition (JSON Input)                │
│  - Canvas size                                      │
│  - Layer stack (bottom to top)                     │
│  - Vision context setting                          │
└─────────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────┐
│  LayeredImageService                                │
│  - Sequential generation loop                       │
│  - Context building                                 │
│  - Layer orchestration                              │
└─────────────────────────────────────────────────────┘
                        │
          ┌─────────────┴─────────────┐
          ▼                           ▼
┌──────────────────────┐    ┌──────────────────────┐
│  Layer Generation    │    │  Context Builder     │
│  - AI (GPT Image)    │    │  - Text context      │
│  - Uploaded files    │    │  - Vision analysis   │
│  - Solid colors      │    │  - Layer stack info  │
│  - Text rendering    │    └──────────────────────┘
└──────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────┐
│  ChatImageService                                    │
│  - Vision analysis (GPT-4o)                         │
│  - Image generation (GPT Image models)              │
│  - Context enhancement                               │
└─────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────┐
│  Exporter (PDN/ORA/PSD)                             │
│  - Layer composition                                 │
│  - Blend mode application                           │
│  - File format encoding                             │
└─────────────────────────────────────────────────────┘
```

---

## Layer Types

### 1. **Generated** (AI-Generated Images)
- **Type**: `"Generated"`
- **Content**: AI prompt describing what to generate
- **Options**:
  - `shouldGenerate: true` (default) - Generate new image
  - `shouldGenerate: false` - Load existing image from path in `content`

```json
{
  "name": "Mountain Landscape",
  "type": "Generated",
  "shouldGenerate": true,
  "content": "Snow-capped mountains at sunset with dramatic lighting",
  "size": { "width": 1024, "height": 1024 },
  "position": { "x": 0, "y": 0 }
}
```

### 2. **Uploaded** (Existing Files)
- **Type**: `"Uploaded"`
- **Content**: Filename from uploads folder
- **Auto-resize**: Resized to match specified dimensions

```json
{
  "name": "Company Logo",
  "type": "Uploaded",
  "content": "logo-transparent.png",
  "size": { "width": 200, "height": 200 },
  "position": { "x": 50, "y": 50 }
}
```

### 3. **SolidColor** (Color Fills)
- **Type**: `"SolidColor"`
- **Content**: Hex color code (#RRGGBB or #RRGGBBAA)
- **Use cases**: Backgrounds, overlays, masks

```json
{
  "name": "Background",
  "type": "SolidColor",
  "content": "#1E3A8A",
  "size": { "width": 1920, "height": 1080 },
  "position": { "x": 0, "y": 0 }
}
```

### 4. **Text** (Rendered Text)
- **Type**: `"Text"`
- **Content**: The text to render
- **Options**: fontFamily, fontSize, fontColor

```json
{
  "name": "Title",
  "type": "Text",
  "content": "Welcome to Brand2Boost",
  "fontFamily": "Arial",
  "fontSize": 72,
  "fontColor": "#FFFFFF",
  "size": { "width": 800, "height": 100 },
  "position": { "x": 560, "y": 50 }
}
```

---

## Configuration

### Canvas Settings

```json
{
  "canvas": {
    "width": 1920,  // Pixels
    "height": 1080  // Pixels
  }
}
```

### Layer Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `name` | string | "Layer" | Layer name (for organization) |
| `type` | string | "Generated" | Layer type: Generated, Uploaded, SolidColor, Text |
| `shouldGenerate` | bool? | true (Generated), false (others) | Whether to AI-generate this layer |
| `position` | object | { x: 0, y: 0 } | Layer position on canvas |
| `size` | object | { width: 1024, height: 1024 } | Layer dimensions |
| `content` | string | "" | Meaning depends on type (see Layer Types) |
| `opacity` | float | 1.0 | Layer opacity (0.0 - 1.0) |
| `blendMode` | string | "Normal" | Blend mode: Normal, Multiply, Screen, Overlay, Darken, Lighten |
| `visible` | bool | true | Whether layer is visible |

### Vision Context Setting

```json
{
  "useVisionContext": true  // Default: true
}
```

**When to disable:**
- ⚡ **Performance priority**: Faster generation, lower cost
- 📊 **Simple compositions**: Text-only context is sufficient
- 🔁 **Iterative experimentation**: Rapid prototyping phase

**When to enable:**
- 🎨 **Complex compositions**: Multiple visual layers
- 🌈 **Color harmony critical**: Need visual color analysis
- 🏗️ **Spatial coherence**: Layers must align precisely
- ✨ **Style consistency**: Maintain artistic style across layers

---

## Sequential Generation

### How It Works

Layers are generated **bottom-to-top** (index 0 → N). Each layer receives:

1. **Text Context**:
   - Canvas dimensions
   - Complete layer stack overview (with status markers)
   - Previously generated layers details
   - Current layer specifications

2. **Vision Context** (if enabled):
   - GPT-4o analysis of all previous layers
   - Visual composition description
   - Spatial layout analysis
   - Color palette extraction
   - Style identification
   - Integration recommendations

### Context Example

When generating Layer 3 (after Layers 1-2 are complete):

**Text Context:**
```
Canvas: 1920x1080px

Total layers: 4
Layer stack (bottom to top):
  1. [✓ Generated] Sky Background (SolidColor) - #87CEEB
  2. [✓ Generated] Mountain Range (Generated) - Majestic mountains
  3. [➤ Current] Forest (Generated) - Dense pine forest
  4. [⧖ Pending] Sunset Overlay (Generated) - Warm sunset glow

Previously generated layers (2):
  - Sky Background: #87CEEB (1920x1080px at 0,0)
  - Mountain Range: Majestic mountains... (1024x768px at 448,156)

Current layer 'Forest':
  Type: Generated
  Position: (0, 700)
  Size: 1920x380px
  Blend mode: Normal
  Opacity: 100%
```

**Vision Context (if enabled):**
```
Visual composition: The current composition shows a serene sky gradient
(light blue #87CEEB) with realistic snow-capped mountain peaks in the
middle ground. Mountains feature sharp details with white highlights
and gray-blue shadows.

Spatial layout: Mountains positioned center-right (448px, 156px),
creating natural horizon line at ~60% height. Sky occupies full canvas
behind.

Art style: Photorealistic landscape with high detail rendering. Natural
lighting suggests midday sun from upper left.

Color palette: Cool blues (#87CEEB sky, #4A5F7A mountain shadows),
whites (#FFFFFF snow caps), warm earth tones in mountain base.

Lighting and atmosphere: Bright, clear lighting. No atmospheric haze.
Strong contrast between sky and mountain silhouette.

Cohesion recommendations: Forest layer should use darker greens
(#2D5016, #1A3410) to ground the composition. Consider subtle blue
tints to harmonize with cool color palette. Position trees to frame
mountains naturally.
```

---

## Vision-Enhanced Context

### What is Vision Enhancement?

Instead of just describing layers with text, **GPT-4 Vision** actually "sees" the previously generated layers and provides detailed visual analysis.

### How It Works

```
┌──────────────────────┐
│  Previous Layer 1    │  ──┐
│  (PNG bytes)         │    │
└──────────────────────┘    │
┌──────────────────────┐    │
│  Previous Layer 2    │  ──┤───▶  GPT-4o Vision
│  (PNG bytes)         │    │      Analysis
└──────────────────────┘    │
┌──────────────────────┐    │      ▼
│  Target Prompt       │  ──┘   Detailed
│  "Dense forest..."   │        Description
└──────────────────────┘           │
                                   ▼
                        ┌──────────────────────┐
                        │  Enhanced Prompt     │
                        │  (Text + Vision)     │
                        └──────────────────────┘
                                   │
                                   ▼
                        ┌──────────────────────┐
                        │  GPT Image Model     │
                        │  Generates Layer     │
                        └──────────────────────┘
```

### Analysis Components

1. **Visual Composition**: Elements, colors, shapes, styles
2. **Spatial Layout**: Positioning and arrangement
3. **Art Style**: Technique, aesthetic, medium
4. **Color Palette**: Dominant colors and relationships
5. **Lighting & Atmosphere**: Light sources, shadows, mood
6. **Cohesion Recommendations**: How to integrate the new layer

### Cost & Performance

| Setting | Cost per layer | Latency | Quality |
|---------|---------------|---------|---------|
| Vision OFF | ~$0.04 | ~3-5s | Good (text context only) |
| Vision ON | ~$0.08 | ~8-12s | Excellent (visual + text context) |

**Note**: Vision analysis uses GPT-4o (input tokens for images + text)

---

## Usage Examples

### Example 1: Social Media Post

**Goal**: Create a branded social media post with logo, background, and text.

```json
{
  "format": "pdn",
  "canvas": {
    "width": 1080,
    "height": 1080
  },
  "useVisionContext": true,
  "layers": [
    {
      "name": "Background Gradient",
      "type": "Generated",
      "shouldGenerate": true,
      "content": "Modern gradient background from deep purple to vibrant pink, smooth transition",
      "size": { "width": 1080, "height": 1080 },
      "position": { "x": 0, "y": 0 }
    },
    {
      "name": "Geometric Shapes",
      "type": "Generated",
      "shouldGenerate": true,
      "content": "Abstract geometric shapes (circles, triangles) in complementary colors, scattered composition",
      "size": { "width": 1080, "height": 1080 },
      "position": { "x": 0, "y": 0 },
      "opacity": 0.3,
      "blendMode": "Overlay"
    },
    {
      "name": "Company Logo",
      "type": "Uploaded",
      "content": "brand-logo-white.png",
      "size": { "width": 200, "height": 200 },
      "position": { "x": 50, "y": 50 }
    },
    {
      "name": "Main Text",
      "type": "Text",
      "content": "Summer Sale 2026",
      "fontFamily": "Arial",
      "fontSize": 64,
      "fontColor": "#FFFFFF",
      "size": { "width": 800, "height": 100 },
      "position": { "x": 140, "y": 490 }
    },
    {
      "name": "Subtitle",
      "type": "Text",
      "content": "Up to 50% Off",
      "fontFamily": "Arial",
      "fontSize": 36,
      "fontColor": "#FFD700",
      "size": { "width": 600, "height": 60 },
      "position": { "x": 240, "y": 590 }
    }
  ],
  "metadata": {
    "title": "Summer Sale Social Post",
    "tags": ["social-media", "sale", "marketing"],
    "description": "Instagram post for summer campaign"
  }
}
```

**Generation Flow**:
1. **Layer 1 (Background)**: Generated with no context (first layer)
2. **Layer 2 (Shapes)**: Vision analyzes purple-pink gradient, suggests complementary colors
3. **Layer 3 (Logo)**: Uploaded image resized and positioned
4. **Layer 4 (Main Text)**: Rendered with white color for contrast
5. **Layer 5 (Subtitle)**: Rendered with gold accent color

**Vision Impact**: Layer 2 receives analysis like "background features warm tones (purple #6B2C91 to pink #E91E63), suggest using cool accent colors (teal, cyan) for geometric shapes to create visual balance."

---

### Example 2: Product Mockup

**Goal**: Create a product on environment background.

```json
{
  "format": "ora",
  "canvas": {
    "width": 1920,
    "height": 1080
  },
  "useVisionContext": true,
  "layers": [
    {
      "name": "Studio Background",
      "type": "Generated",
      "shouldGenerate": true,
      "content": "Professional photography studio with soft gray backdrop, subtle lighting gradient",
      "size": { "width": 1920, "height": 1080 },
      "position": { "x": 0, "y": 0 }
    },
    {
      "name": "Product Pedestal",
      "type": "Generated",
      "shouldGenerate": true,
      "content": "Minimalist white cylinder pedestal, modern design, centered composition",
      "size": { "width": 600, "height": 600 },
      "position": { "x": 660, "y": 300 }
    },
    {
      "name": "Product Image",
      "type": "Uploaded",
      "content": "product-photo-cutout.png",
      "size": { "width": 400, "height": 400 },
      "position": { "x": 760, "y": 200 }
    },
    {
      "name": "Shadow Layer",
      "type": "Generated",
      "shouldGenerate": true,
      "content": "Soft realistic shadow beneath product, matching studio lighting direction",
      "size": { "width": 500, "height": 200 },
      "position": { "x": 710, "y": 600 },
      "opacity": 0.4,
      "blendMode": "Multiply"
    }
  ]
}
```

**Vision Impact**: Layer 4 (Shadow) receives analysis of the studio lighting direction from Layers 1-2, and positioning of the product from Layer 3, enabling it to generate a shadow that perfectly matches the scene's lighting and product placement.

---

### Example 3: Layer Reuse Pattern

**Goal**: Use existing generated layers, only regenerate specific parts.

```json
{
  "format": "pdn",
  "canvas": {
    "width": 1600,
    "height": 900
  },
  "useVisionContext": false,
  "layers": [
    {
      "name": "Base Landscape",
      "type": "Generated",
      "shouldGenerate": false,
      "content": "existing-landscape-layer.png",
      "size": { "width": 1600, "height": 900 },
      "position": { "x": 0, "y": 0 }
    },
    {
      "name": "Character Layer",
      "type": "Generated",
      "shouldGenerate": true,
      "content": "Fantasy knight character with armor, standing pose, transparent background",
      "size": { "width": 400, "height": 600 },
      "position": { "x": 600, "y": 200 }
    },
    {
      "name": "Effects Overlay",
      "type": "Generated",
      "shouldGenerate": false,
      "content": "existing-effects-layer.png",
      "size": { "width": 1600, "height": 900 },
      "position": { "x": 0, "y": 0 },
      "blendMode": "Screen"
    }
  ]
}
```

**Use Case**: Iterative workflow where you're happy with background/effects but want to try different character designs. Only Layer 2 regenerates.

---

### Example 4: Text-Only Simple Composition

**Goal**: Fast generation without vision analysis overhead.

```json
{
  "format": "pdn",
  "canvas": {
    "width": 1024,
    "height": 1024
  },
  "useVisionContext": false,
  "layers": [
    {
      "name": "Background",
      "type": "SolidColor",
      "content": "#F0F0F0",
      "size": { "width": 1024, "height": 1024 },
      "position": { "x": 0, "y": 0 }
    },
    {
      "name": "Icon",
      "type": "Generated",
      "shouldGenerate": true,
      "content": "Simple email icon, flat design, blue color",
      "size": { "width": 512, "height": 512 },
      "position": { "x": 256, "y": 256 }
    }
  ]
}
```

**Why vision OFF**: Simple 2-layer composition, solid color background doesn't need visual analysis, text context is sufficient.

---

## API Reference

### Endpoint

```http
POST /api/layered-images/generate
Content-Type: application/json
```

### Request Body

```json
{
  "projectId": "string",
  "userId": "string (optional)",
  "definition": {
    "format": "pdn | ora | psd",
    "canvas": { "width": number, "height": number },
    "useVisionContext": boolean,
    "layers": [ /* LayerDefinition[] */ ],
    "metadata": { /* ImageMetadata (optional) */ }
  }
}
```

### Response

```json
{
  "success": boolean,
  "fileName": "string (if success)",
  "fileUrl": "string (if success)",
  "format": "pdn | ora | psd",
  "layers": [
    {
      "name": "string",
      "width": number,
      "height": number,
      "success": boolean,
      "errorMessage": "string (if failed)"
    }
  ],
  "errorMessage": "string (if failed)"
}
```

---

## Best Practices

### 1. **Layer Ordering**
- ✅ **Background first**: Solid colors or large images at index 0
- ✅ **Midground next**: Main content/subjects
- ✅ **Foreground last**: Text, logos, overlays

### 2. **Vision Context Usage**
- ✅ **Enable for**: Complex compositions, color-critical work, spatial coherence
- ❌ **Disable for**: Simple layouts, solid color backgrounds, rapid iteration

### 3. **Layer Size Optimization**
- ✅ **Match canvas size**: Backgrounds should be canvas-sized
- ✅ **Appropriate sizing**: Don't generate 4K images for small UI elements
- ✅ **Power of 2**: Use sizes like 512, 1024, 2048 for best AI results

### 4. **Prompt Engineering**
- ✅ **Be specific**: "Snow-capped mountain at sunset" > "mountain"
- ✅ **Describe style**: "Photorealistic", "Flat design", "Watercolor painting"
- ✅ **Mention composition**: "Centered", "Left-aligned", "Close-up"
- ✅ **Specify background**: "Transparent background", "Solid white background"

### 5. **Blend Modes**
- **Normal**: Default, full opacity layer
- **Multiply**: Darkening effect, great for shadows
- **Screen**: Brightening effect, good for highlights/glows
- **Overlay**: Contrast enhancement
- **Darken**: Keep darkest pixels
- **Lighten**: Keep lightest pixels

### 6. **Performance**
- ⚡ **Parallel not applicable**: Layers MUST be sequential (by design)
- ⚡ **Caching**: Reuse existing layers with `shouldGenerate: false`
- ⚡ **Batch requests**: Group multiple unrelated images into separate API calls

---

## Troubleshooting

### Layer Generation Failed

**Symptom**: `layer.success = false` in response

**Causes & Solutions**:
1. **Invalid prompt**: Check layer `content` for clarity
2. **Unsupported size**: Use standard sizes (512x512, 1024x1024, etc.)
3. **File not found**: Verify uploaded file exists in uploads folder
4. **API timeout**: Large images may timeout; reduce size or complexity

### Vision Analysis Error

**Symptom**: Console log shows "Vision analysis failed"

**Causes & Solutions**:
1. **API key missing**: Ensure OpenAI API key is configured
2. **Network error**: Check connectivity to OpenAI API
3. **Rate limit**: GPT-4o may have rate limits; retry with delay
4. **Fallback**: System automatically falls back to text-only context

### Color Inconsistency

**Symptom**: Generated layers don't harmonize

**Solutions**:
1. ✅ **Enable vision context**: `useVisionContext: true`
2. ✅ **Specify palette**: Mention existing colors in prompts
3. ✅ **Style consistency**: Use same style descriptors across layers

### Position Misalignment

**Symptom**: Layers don't align as expected

**Solutions**:
1. ✅ **Check coordinates**: Verify position { x, y } values
2. ✅ **Canvas size**: Ensure layer fits within canvas bounds
3. ✅ **Content-aware prompts**: Mention spatial relationships in prompts

---

## Technical Notes

### Supported Image Formats (Input)
- PNG (recommended for transparency)
- JPEG
- GIF
- WebP

### Supported Export Formats
- **PDN** (Paint.NET): Full feature support, Windows-native
- **ORA** (OpenRaster): Cross-platform, GIMP/Krita compatible
- **PSD** (Photoshop): Industry standard (limited blend mode support)

### AI Models Used
- **Image Generation**: GPT Image (gpt-image-1)
- **Vision Analysis**: GPT-4o (multimodal)

### Limitations
- **DALL-E 3 deprecated**: May 12, 2026 - use GPT Image models
- **Vision API**: Requires OpenAI API access with GPT-4o
- **File size**: Large images (>10MB per layer) may impact performance
- **Layer count**: Recommended max 10-15 layers for performance

---

## Changelog

### 2026-01-21 - Vision Enhancement Update
- ✨ **NEW**: Vision-enhanced context generation using GPT-4o
- ✨ **NEW**: `useVisionContext` configuration option
- ✨ **NEW**: `shouldGenerate` per-layer control
- ✨ **NEW**: Sequential generation with full context passing
- 🔧 **Improved**: Layer context descriptions with status markers
- 📚 **Added**: Comprehensive documentation with examples

### 2026-01-20 - Initial Release
- 🎉 First version with basic layered image generation
- ✅ Multiple layer types support
- ✅ Export to PDN/ORA/PSD formats

---

## Support & Feedback

For issues, questions, or feature requests, contact the development team or file an issue in the project repository.

**Documentation Version**: 1.1
**Last Updated**: 2026-01-21
**Maintained By**: Hazina Framework Team
