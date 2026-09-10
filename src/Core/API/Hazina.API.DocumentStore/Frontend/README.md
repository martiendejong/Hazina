# OCR Queue Management Frontend

Beautiful, enterprise-grade OCR queue management interface for Hazina DocumentStore.

## Features

### OCR Queue Dashboard
- Real-time queue status monitoring
- Auto-refresh every 10 seconds when items are pending/processing
- Status filtering (Pending, Processing, Completed, Failed)
- Search by filename
- Beautiful status cards with statistics
- Color-coded status indicators

### OCR Status Badge Component
- Lightweight status indicator for document lists
- Auto-refresh for pending/processing items
- Confidence score display
- Error message tooltips

### Progress Tracking
- Real-time status updates
- Confidence score visualization with progress bars
- Processing time tracking
- Retry counter for failed items

### Metadata Extraction Display
- VAT amount extraction
- Invoice number detection
- Date extraction
- Custom metadata fields

## Installation

```bash
cd Frontend
npm install
```

## Development

```bash
npm run dev
```

## Build

```bash
npm run build
```

## Integration

### Add OCR Queue Page to Your Router

```tsx
import OCRQueue from './Frontend/OCRQueue';
import { Route } from 'react-router-dom';

// In your router configuration:
<Route path="/ocr-queue" element={<OCRQueue />} />
```

### Add OCR Status Badge to Document List

```tsx
import OCRStatusBadge from './Frontend/OCRStatusBadge';

// In your document list component:
<OCRStatusBadge documentId={document.id} autoRefresh={true} />
```

## API Endpoints Used

- `GET /api/v1/ocr-queue` - List queue items
- `GET /api/v1/ocr-queue/stats` - Get statistics
- `GET /api/v1/ocr-queue/document/{documentId}` - Get document OCR status
- `POST /api/v1/ocr-queue/document/{documentId}/reprocess` - Retry failed OCR

## Authentication

The components use `getAuthToken()` helper function that reads from `localStorage.authToken`.
Update this function to match your authentication system.

## Styling

Built with Material-UI (MUI) for beautiful, responsive design:
- Color-coded status badges
- Smooth animations
- Progress indicators
- Tooltips for additional information
- Responsive grid layout

## Status Colors

- **Pending**: Gray (default)
- **Processing**: Blue (info)
- **Completed**: Green (success)
- **Failed**: Red (error)

## Confidence Score

- **Green**: > 70% (high confidence)
- **Orange**: < 70% (low confidence, may need review)

## Auto-Refresh Behavior

- Auto-refreshes every 10 seconds when:
  - Any items are in "Pending" status
  - Any items are in "Processing" status
- Stops auto-refresh when queue is empty or all items completed/failed

## Error Handling

- Graceful error messages
- Retry buttons for failed items
- Maximum retry count display
- Detailed error message tooltips

## Performance

- Efficient polling (only when needed)
- Paginated results (20 items per page)
- Lightweight components
- Optimized re-renders

## Future Enhancements

- Bulk operations (retry all failed)
- Export queue data
- Advanced filtering (date range, confidence score)
- Real-time WebSocket updates
- Download extracted data as CSV
