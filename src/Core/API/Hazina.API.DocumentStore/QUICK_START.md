# OCR Queue Quick Start Guide

Get the OCR queue up and running in 5 minutes.

## Prerequisites

- .NET 6+ SDK
- Node.js 18+
- SQLite
- Tesseract OCR

## Installation

### 1. Install Tesseract

#### Windows
```powershell
# Download Tesseract installer
# https://github.com/UB-Mannheim/tesseract/wiki
# Install to default location: C:\Program Files\Tesseract-OCR
```

#### Linux/Mac
```bash
# Ubuntu/Debian
sudo apt-get install tesseract-ocr tesseract-ocr-eng tesseract-ocr-nld

# macOS
brew install tesseract tesseract-lang
```

### 2. Download Language Data

```bash
cd src/Core/API/Hazina.API.DocumentStore
mkdir tessdata
cd tessdata

# Download English
curl -L https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata -o eng.traineddata

# Download Dutch
curl -L https://github.com/tesseract-ocr/tessdata/raw/main/nld.traineddata -o nld.traineddata
```

### 3. Update Configuration

Edit `appsettings.json`:

```json
{
  "DocumentStoreApi": {
    "TesseractDataPath": "./tessdata",
    "MaxUploadSizeMB": 50,
    "FileStoragePath": "./data/stores",
    "EmbeddingDimensions": 1536
  },
  "ConnectionStrings": {
    "DocumentStoreDb": "Data Source=documentstore.db"
  }
}
```

### 4. Run Database Migration

```bash
# Apply migration
dotnet ef migrations add AddOCRQueue
dotnet ef database update

# Or run SQL script
sqlite3 documentstore.db < Migrations/AddOCRQueue.sql
```

### 5. Register Services

Add to `Program.cs`:

```csharp
using Hazina.API.DocumentStore.Services;

// ... existing code ...

// Register OCR services
builder.Services.AddSingleton<OCRService>();
builder.Services.AddHostedService<OCRQueueService>();
```

### 6. Build and Run Backend

```bash
cd src/Core/API/Hazina.API.DocumentStore
dotnet restore
dotnet build
dotnet run
```

Backend will start on `https://localhost:5001`

### 7. Install Frontend Dependencies

```bash
cd Frontend
npm install
```

### 8. Start Frontend Development Server

```bash
npm run dev
```

Frontend will start on `http://localhost:5173`

## Verify Installation

### Test OCR Service

Upload a test image:

```bash
curl -X POST "https://localhost:5001/api/v1/stores/{storeId}/documents/upload" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -F "File=@test_invoice.png"
```

Expected response:
```json
{
  "documentId": "guid",
  "status": "ocr_pending",
  "filename": "test_invoice.png"
}
```

### Check Queue Status

```bash
curl "https://localhost:5001/api/v1/ocr-queue" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

Expected response:
```json
{
  "items": [
    {
      "id": "guid",
      "documentId": "guid",
      "status": "Pending",
      "originalFilename": "test_invoice.png"
    }
  ]
}
```

### Wait 30 seconds, then check again

The background service polls every 30 seconds. After processing:

```json
{
  "items": [
    {
      "id": "guid",
      "documentId": "guid",
      "status": "Completed",
      "confidenceScore": 0.92,
      "extractedMetadata": {
        "vatAmount": 42.50,
        "invoiceNumber": "INV-2024-001"
      }
    }
  ]
}
```

## Access the UI

1. Open browser to `http://localhost:5173/ocr-queue`
2. You should see the OCR Queue dashboard
3. Upload some test images
4. Watch them process in real-time

## Common Issues

### "Tesseract not found"

**Problem**: OCR fails with "Unable to load library"

**Solution**:
1. Verify Tesseract is installed: `tesseract --version`
2. Check `TesseractDataPath` in `appsettings.json`
3. Ensure `eng.traineddata` exists in tessdata folder

### "No items processing"

**Problem**: Items stay in "Pending" status forever

**Solution**:
1. Check logs: `dotnet run --verbosity detailed`
2. Verify OCRQueueService is running
3. Ensure documents exist in RAG store
4. Check for errors in application logs

### "Low confidence scores"

**Problem**: Confidence scores below 70%

**Solution**:
1. Improve image quality (scan at 300 DPI)
2. Ensure good contrast
3. Try different language setting
4. Rotate image if needed
5. Clean up image (remove noise)

### "Frontend not connecting to API"

**Problem**: CORS errors or 401 Unauthorized

**Solution**:
1. Check CORS settings in `Program.cs`
2. Verify Bearer token is valid
3. Check API URL in frontend code
4. Ensure backend is running

## Next Steps

### Add Custom Metadata Extraction

Edit `OCRService.cs`:

```csharp
// Add your custom pattern
private static readonly Regex CustomPattern =
    new Regex(@"YOUR_PATTERN_HERE", RegexOptions.IgnoreCase);

// In ExtractMetadata method:
var match = CustomPattern.Match(text);
if (match.Success)
{
    metadata["customField"] = match.Groups[1].Value;
}
```

### Customize UI Theme

Edit `OCRQueue.tsx`:

```tsx
// Change status colors
const statusColors = {
  Pending: '#ffa726',    // Orange
  Processing: '#42a5f5', // Blue
  Completed: '#66bb6a',  // Green
  Failed: '#ef5350'      // Red
};
```

### Adjust Processing Intervals

Edit `OCRQueueService.cs`:

```csharp
// Change poll interval
private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(15); // Faster

// Change retry delay
private readonly TimeSpan _retryDelay = TimeSpan.FromMinutes(5); // Longer
```

### Add Webhooks

Create `WebhookService.cs`:

```csharp
public class WebhookService
{
    public async Task NotifyCompletion(OCRQueue item)
    {
        var payload = new {
            documentId = item.DocumentId,
            status = item.Status,
            confidence = item.ConfidenceScore
        };

        await _httpClient.PostAsJsonAsync(
            "https://your-webhook-url.com/ocr-complete",
            payload);
    }
}
```

Register and use in `OCRQueueService.cs`.

## Development Tips

### Hot Reload (Backend)

```bash
dotnet watch run
```

Backend will auto-reload on file changes.

### Hot Reload (Frontend)

Vite automatically hot-reloads. Just save files.

### Debug Logging

Enable verbose logging in `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Hazina.API.DocumentStore.Services.OCRQueueService": "Debug",
      "Hazina.API.DocumentStore.Services.OCRService": "Debug"
    }
  }
}
```

### Test Different Languages

```bash
# Upload with language parameter
curl -X POST ".../reprocess" \
  -H "Content-Type: application/json" \
  -d '{"language": "fra+eng", "priority": 1}'
```

Download French language data first:
```bash
cd tessdata
curl -L https://github.com/tesseract-ocr/tessdata/raw/main/fra.traineddata -o fra.traineddata
```

## Production Deployment

### Environment Variables

```bash
export DocumentStoreApi__TesseractDataPath="/app/tessdata"
export DocumentStoreApi__FileStoragePath="/data/stores"
export ConnectionStrings__DocumentStoreDb="Data Source=/data/documentstore.db"
```

### Docker

Create `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:6.0
RUN apt-get update && apt-get install -y tesseract-ocr tesseract-ocr-eng tesseract-ocr-nld
WORKDIR /app
COPY . .
EXPOSE 5000
ENTRYPOINT ["dotnet", "Hazina.API.DocumentStore.dll"]
```

Build and run:

```bash
docker build -t hazina-documentstore .
docker run -p 5000:5000 hazina-documentstore
```

### Health Check Endpoint

Add to `Program.cs`:

```csharp
app.MapGet("/health", () => Results.Ok(new {
    status = "healthy",
    timestamp = DateTime.UtcNow
}));
```

### Monitor Queue Size

```bash
# Alert if queue grows too large
curl "https://your-api.com/api/v1/ocr-queue/stats" | \
  jq '.totalPending' | \
  awk '{if ($1 > 100) print "Queue too large!"}'
```

## Support

- Documentation: `OCR_QUEUE_IMPLEMENTATION_GUIDE.md`
- Architecture: `ARCHITECTURE_DIAGRAM.md`
- Frontend: `Frontend/README.md`

## Checklist

- [ ] Tesseract installed
- [ ] Language data downloaded
- [ ] Configuration updated
- [ ] Database migrated
- [ ] Services registered
- [ ] Backend running
- [ ] Frontend running
- [ ] Test upload successful
- [ ] Queue processing
- [ ] UI accessible

## Congratulations!

You now have a fully functional OCR queue system. Upload some documents and watch them process automatically.

---

**Estimated Setup Time**: 5-10 minutes
**Difficulty**: Easy
**Last Updated**: 2026-05-03
