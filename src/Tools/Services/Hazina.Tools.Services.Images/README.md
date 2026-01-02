# Hazina.Tools.Services.Images

Provider-agnostic image modification tool for the Hazina ecosystem.

## Purpose

This module provides a flexible, extensible image editing pipeline that can use multiple providers for different operations. Local operations (crop, resize, text, filters) use SixLabors.ImageSharp, while AI-based operations (inpainting, mask replacement) can use external AI services.

## Features

- **Provider-agnostic**: Operations are routed to the appropriate provider automatically
- **Operation pipeline**: Chain multiple operations in a single request
- **Local processing**: Deterministic operations using ImageSharp (no external dependencies)
- **AI-ready**: Stub for AI-based operations (mask replacement, inpainting)
- **Extensible**: Add custom providers and operations easily
- **Configuration-driven**: Enable/disable providers via configuration

## Installation

Add project reference:

```bash
dotnet add <your>.csproj reference src/Tools/Services/Hazina.Tools.Services.Images/Hazina.Tools.Services.Images.csproj
```

## Usage

### Registration

```csharp
// Basic registration (LocalImageProvider only)
services.AddHazinaImages();

// With configuration
services.AddHazinaImages(options =>
{
    options.DefaultProvider = "Local";
    options.EnableLocalProvider = true;
    options.EnableAiProvider = false;
});

// From configuration file
services.AddHazinaImages(configuration);

// Add AI provider with custom handler
services.AddAiImageProvider(builder => builder
    .WithMaskReplaceHandler(async (source, mask, prompt, options, ct) =>
    {
        // Your AI integration here
        return await YourAiService.EditImageAsync(source, mask, prompt, ct);
    }));
```

### Configuration (appsettings.json)

```json
{
  "Hazina": {
    "Images": {
      "DefaultProvider": "Local",
      "EnableLocalProvider": true,
      "EnableAiProvider": false,
      "AI": {
        "Provider": "OpenAI",
        "Model": "dall-e-3"
      }
    }
  }
}
```

### Editing Images

```csharp
public class MyService
{
    private readonly IImageTool _imageTool;

    public MyService(IImageTool imageTool)
    {
        _imageTool = imageTool;
    }

    public async Task<Stream> ProcessImageAsync(Stream inputImage)
    {
        var result = await _imageTool.EditAsync(new ImageEditRequest
        {
            InputImage = inputImage,
            Operations = new ImageEditOperation[]
            {
                new CropOperation(X: 0, Y: 0, Width: 800, Height: 600),
                new ResizeOperation(Width: 400, Height: 300),
                new AddTextOperation(
                    Text: "Hello World",
                    X: 10,
                    Y: 10,
                    FontSize: 24,
                    ColorHex: "#FF0000"
                ),
                new ApplyFilterOperation(ImageFilter.Grayscale)
            },
            OutputFormat = ImageOutputFormat.Png
        });

        return result.Image;
    }
}
```

## Supported Operations

### Local Provider (ImageSharp)

| Operation | Description |
|-----------|-------------|
| `AddTextOperation` | Adds text at specified coordinates |
| `CropOperation` | Crops to a rectangular region |
| `ResizeOperation` | Resizes with optional aspect ratio preservation |
| `RotateOperation` | Rotates by specified degrees |
| `AdjustBrightnessOperation` | Adjusts brightness (-1.0 to 1.0) |
| `AdjustContrastOperation` | Adjusts contrast (-1.0 to 1.0) |
| `ApplyFilterOperation` | Applies filters (Grayscale, Sepia, Blur, Sharpen, Invert) |

### AI Provider

| Operation | Description |
|-----------|-------------|
| `MaskReplaceOperation` | AI-based inpainting using a mask and prompt |

## Provider Model

The provider system allows routing different operations to different implementations:

```
ImageEditRequest
    │
    ▼
┌─────────────┐
│  ImageTool  │  (Orchestrates operation pipeline)
└─────────────┘
    │
    ▼
┌─────────────────────┐
│ ImageProviderResolver│  (Selects provider per operation)
└─────────────────────┘
    │
    ├──► LocalImageProvider (Crop, Resize, Text, Filters)
    │
    └──► AiImageProvider (MaskReplace - requires configuration)
```

## Why AI is Optional

AI-based image editing (like inpainting) requires:
- External API credentials
- Network connectivity
- Cost management

By keeping AI optional:
- The module works offline with local operations
- No mandatory external dependencies
- Users opt-in to AI features with explicit configuration
- Different AI providers can be swapped without code changes

## Creating Custom Providers

```csharp
public class MyCustomProvider : IImageProvider
{
    public string Name => "Custom";

    public bool CanHandle(ImageEditOperation operation)
        => operation is MyCustomOperation;

    public async Task<Stream> ApplyAsync(
        Stream image,
        ImageEditOperation operation,
        ImageProviderOptions options,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}

// Register
services.AddImageProvider<MyCustomProvider>();
```

## API Reference

XML documentation is generated on build: `bin/Debug/net8.0/Hazina.Tools.Services.Images.xml`

## Dependencies

- `SixLabors.ImageSharp` - Local image processing
- `SixLabors.ImageSharp.Drawing` - Text rendering
- `Hazina.Tools.Core` - Configuration support
- `Hazina.Tools.Models` - Shared models
- `Hazina.Tools.Data` - Data access patterns
