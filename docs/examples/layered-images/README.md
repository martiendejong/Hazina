# LayeredImageTool - Example Definitions

This directory contains example JSON definitions for common layered image use cases.

## Quick Start

1. Choose an example JSON file
2. Customize the content, sizes, and positions
3. POST to `/api/layered-images/generate` endpoint
4. Receive generated layered image file

## Examples Overview

| File | Use Case | Vision | Complexity | Description |
|------|----------|--------|------------|-------------|
| `01-simple-social-post.json` | Social Media | ✅ ON | ⭐ Simple | Basic Instagram post with text and pattern |
| `02-landscape-composition.json` | Artwork | ✅ ON | ⭐⭐⭐ Complex | Multi-layer landscape with depth |
| `03-product-mockup.json` | Photography | ✅ ON | ⭐⭐ Medium | Professional product photography setup |
| `04-text-heavy-design.json` | Corporate | ❌ OFF | ⭐ Simple | Annual report cover with typography |
| `05-mixed-content.json` | Marketing | ✅ ON | ⭐⭐⭐ Complex | Hero banner with mixed content types |
| `06-performance-optimized.json` | Iterative | ❌ OFF | ⭐ Simple | Layer reuse for fast iteration |

## Example Categories

### 1. **Social Media Posts** (01-simple-social-post.json)
- **Format**: 1080x1080 (Instagram square)
- **Vision**: ON (for pattern harmonization)
- **Layers**: 4 (Background, Pattern, Text×2)
- **Time**: ~15-20 seconds
- **Use When**: Creating promotional social media content

### 2. **Landscape Compositions** (02-landscape-composition.json)
- **Format**: 1920x1080 (HD landscape)
- **Vision**: ON (critical for depth and atmosphere)
- **Layers**: 4 (Sky, Mountains, Forest, Haze)
- **Time**: ~40-50 seconds
- **Use When**: Creating complex artistic scenes with multiple depth layers

### 3. **Product Mockups** (03-product-mockup.json)
- **Format**: 1920x1080 (HD)
- **Vision**: ON (for realistic lighting/shadows)
- **Layers**: 5 (Studio, Pedestal, Product, Shadow, Glow)
- **Time**: ~40-50 seconds
- **Use When**: Creating professional product photography scenes

### 4. **Text-Heavy Designs** (04-text-heavy-design.json)
- **Format**: 1200x1600 (Portrait)
- **Vision**: OFF (text doesn't need visual analysis)
- **Layers**: 5 (Background, Text×3, Accent)
- **Time**: ~10-15 seconds
- **Use When**: Creating reports, posters, typography-focused designs

### 5. **Mixed Content** (05-mixed-content.json)
- **Format**: 1920x1080 (HD)
- **Vision**: ON (for cohesive composition)
- **Layers**: 8 (Background, Hero, Overlay, Logo, Text×2, CTA×2)
- **Time**: ~45-60 seconds
- **Use When**: Creating complex marketing materials, landing page heroes

### 6. **Performance Optimized** (06-performance-optimized.json)
- **Format**: 1024x1024
- **Vision**: OFF (faster iteration)
- **Layers**: 3 (2 cached, 1 new)
- **Time**: ~8-12 seconds
- **Use When**: Iterating on designs, A/B testing variations

## Customization Guide

### Adjusting Canvas Size

**Common Sizes**:
```json
// Instagram Square
{ "width": 1080, "height": 1080 }

// Instagram Story
{ "width": 1080, "height": 1920 }

// Facebook Post
{ "width": 1200, "height": 630 }

// Twitter Header
{ "width": 1500, "height": 500 }

// Desktop Wallpaper
{ "width": 1920, "height": 1080 }

// YouTube Thumbnail
{ "width": 1280, "height": 720 }
```

### Vision Context Decision

**Enable (`"useVisionContext": true`)** when:
- Multiple generated layers interact visually
- Color harmony is critical
- Spatial positioning matters
- Lighting/shadows need to be coherent
- Complex artistic composition

**Disable (`"useVisionContext": false`)** when:
- Simple layouts (text + solid background)
- Rapid iteration/prototyping phase
- Cost/speed is priority
- Layers are independent

### Layer Prompt Tips

**Generated Layers**:
```json
// ✅ Good
"content": "Snow-capped mountain peaks at sunset, dramatic lighting, photorealistic style"

// ❌ Too vague
"content": "mountain"
```

**Specify Background**:
```json
// For transparent layers (logos, icons)
"content": "Company logo, flat design, transparent background"

// For opaque layers
"content": "Forest scene, solid background, photorealistic"
```

**Style Consistency**:
```json
// First layer
"content": "Sky background, watercolor painting style, soft colors"

// Subsequent layers (maintain style)
"content": "Mountain landscape, watercolor painting style matching the soft sky"
```

## Testing Strategy

### Phase 1: Prototype (Vision OFF)
```json
{
  "useVisionContext": false,
  "layers": [ /* simplified layers */ ]
}
```
- Fast iteration
- Test composition and layout
- Verify text readability
- Check layer positioning

### Phase 2: Refinement (Vision ON)
```json
{
  "useVisionContext": true,
  "layers": [ /* full detailed layers */ ]
}
```
- Final quality generation
- Color harmony
- Visual coherence
- Production-ready output

## Cost Estimation

**Vision OFF**:
- Simple (2-3 layers): ~$0.08 - $0.12
- Medium (4-6 layers): ~$0.16 - $0.24
- Complex (7-10 layers): ~$0.28 - $0.40

**Vision ON** (add ~$0.04-0.08 per generated layer):
- Simple (2-3 layers): ~$0.16 - $0.24
- Medium (4-6 layers): ~$0.32 - $0.48
- Complex (7-10 layers): ~$0.56 - $0.80

**Cost Optimization**:
1. Use `shouldGenerate: false` for cached layers
2. Disable vision for simple compositions
3. Reduce layer count where possible
4. Use solid colors instead of generated backgrounds when appropriate

## Common Patterns

### Pattern 1: Background + Content + Text
```json
{
  "layers": [
    { "type": "SolidColor", /* background */ },
    { "type": "Generated", /* main visual */ },
    { "type": "Text", /* headline */ },
    { "type": "Text", /* subtitle */ }
  ]
}
```

### Pattern 2: Base + Overlay + Effects
```json
{
  "layers": [
    { "type": "Generated", /* base image */ },
    { "type": "SolidColor", "blendMode": "Overlay", /* color grade */ },
    { "type": "Generated", "blendMode": "Screen", /* light effects */ }
  ]
}
```

### Pattern 3: Multi-Depth Scene
```json
{
  "layers": [
    { "type": "Generated", /* far background */ },
    { "type": "Generated", /* middle ground */ },
    { "type": "Generated", /* foreground */ },
    { "type": "Generated", "blendMode": "Multiply", /* shadows */ }
  ]
}
```

---

**Need Help?**
- See main documentation: `../LayeredImageTool.md`
- API Reference: [Hazina API Docs]
- Report issues: [Project Repository]

**Examples Version**: 1.0
**Last Updated**: 2026-01-21
