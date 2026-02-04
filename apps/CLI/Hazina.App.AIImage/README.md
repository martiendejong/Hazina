# Hazina AI Image Generator CLI

Command-line tool for AI image generation using Hazina's unified provider system.

## Features

- **Multiple Models**: Support for `gpt-image`, `dall-e-3`, and `dall-e-2`
- **Quality Options**: Standard or HD quality
- **Style Control**: Natural or Vivid styles
- **Flexible Sizing**: 1024x1024, 1792x1024, or 1024x1792
- **Auto API Key Loading**: From environment or appsettings.Secrets.json

## Installation

```bash
# Build the tool
cd apps/CLI/Hazina.App.AIImage
dotnet build

# Or publish as single-file executable
dotnet publish -c Release -r win-x64 --self-contained false
```

## Usage

### Basic Usage

```bash
ai-image --prompt "A beautiful sunset over mountains" --output sunset.png
```

### With Model Selection

```bash
# Use GPT-Image (recommended)
ai-image -p "Professional diagram" -o diagram.png -m gpt-image

# Use DALL-E 3
ai-image -p "Artistic portrait" -o portrait.png -m dall-e-3

# Use DALL-E 2
ai-image -p "Simple icon" -o icon.png -m dall-e-2
```

### With Quality and Style

```bash
# HD quality with natural style (recommended for reference cards)
ai-image -p "Technical flowchart" -o chart.png --quality hd --style natural

# Standard quality with vivid style
ai-image -p "Abstract art" -o art.png --quality standard --style vivid
```

### With Custom Size

```bash
# Square (default)
ai-image -p "Logo design" -o logo.png --size 1024x1024

# Landscape
ai-image -p "Banner design" -o banner.png --size 1792x1024

# Portrait
ai-image -p "Poster design" -o poster.png --size 1024x1792
```

### With API Key

```bash
# Provide API key directly
ai-image -p "Image" -o output.png --api-key "sk-..."

# Or set environment variable
$env:OPENAI_API_KEY = "sk-..."
ai-image -p "Image" -o output.png
```

## Command-Line Options

| Option | Alias | Default | Description |
|--------|-------|---------|-------------|
| `--prompt` | `-p` | **Required** | Text description of the image |
| `--output` | `-o` | **Required** | Output file path |
| `--model` | `-m` | `gpt-image` | Model: `gpt-image`, `dall-e-3`, `dall-e-2` |
| `--size` | `-s` | `1024x1024` | Image size |
| `--quality` | `-q` | `standard` | Quality: `standard`, `hd` |
| `--style` | | `natural` | Style: `natural`, `vivid` |
| `--api-key` | `-k` | | OpenAI API key (optional) |

## API Key Configuration

The tool looks for API keys in this order:

1. `--api-key` parameter
2. `OPENAI_API_KEY` environment variable
3. `C:\Projects\client-manager\ClientManagerAPI\appsettings.Secrets.json` (ApiSettings.OpenApiKey)

## Examples

### Reference Card (Best for Technical Diagrams)

```bash
ai-image \
  --prompt "Clean workflow diagram with numbered steps. Step 1: Pick task, Step 2: Create branch, Step 3: Code, Step 4: PR. White background, readable text." \
  --output workflow.png \
  --model gpt-image \
  --quality hd \
  --style natural \
  --size 1792x1024
```

### Artistic Image

```bash
ai-image \
  --prompt "A futuristic cityscape at sunset with flying cars" \
  --output city.png \
  --model dall-e-3 \
  --quality hd \
  --style vivid
```

### Simple Icon

```bash
ai-image \
  --prompt "Simple gear icon, flat design, blue color" \
  --output gear-icon.png \
  --model dall-e-2 \
  --size 1024x1024
```

## Replacing PowerShell Script

This tool is designed to replace `C:\scripts\tools\ai-image.ps1`.

**Migration:**

```bash
# Old (PowerShell)
powershell.exe -File "C:/scripts/tools/ai-image.ps1" \
  -Prompt "..." -OutputPath "..." -Quality "hd"

# New (Hazina CLI)
ai-image --prompt "..." --output "..." --quality hd
```

**Benefits:**
- ✅ Faster execution (compiled .NET)
- ✅ Better error handling
- ✅ Type safety
- ✅ Integrated with Hazina ecosystem
- ✅ Consistent with other Hazina tools

## Output

```
🎨 AI Image Generation - Powered by Hazina

Model: GptImage
Size: 1024x1024
Quality: hd
Style: natural
Output: C:\output\image.png

✓ Loaded API key from appsettings.Secrets.json
⠋ Generating image...
⠙ Downloading image...

✓ Image saved: C:\output\image.png
Size: 837 KB
```

## Notes

- **gpt-image** is mapped to DALL-E 3 (OpenAI's latest)
- **Natural style** recommended for technical diagrams
- **Vivid style** recommended for artistic/creative images
- **HD quality** provides better detail (larger file size)

## Troubleshooting

**Error: OpenAI API key not found**
- Set `OPENAI_API_KEY` environment variable
- Or provide `--api-key` parameter
- Or ensure appsettings.Secrets.json exists

**Error: Unknown model**
- Valid models: `gpt-image`, `dall-e-3`, `dall-e-2`

**Error: Invalid size**
- Valid sizes: `1024x1024`, `1792x1024`, `1024x1792`

## License

Part of the Hazina AI framework - MIT License
