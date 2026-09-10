import React, { useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  CircularProgress,
  Chip,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Button,
  LinearProgress,
  Grid,
  Paper,
  IconButton,
  Tooltip,
  Alert,
} from '@mui/material';
import {
  CheckCircle,
  Error,
  HourglassEmpty,
  Refresh,
  Search,
  Visibility,
} from '@mui/icons-material';

interface OCRQueueItem {
  id: string;
  documentId: string;
  ragStoreId: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
  priority: number;
  createdAt: string;
  processedAt?: string;
  completedAt?: string;
  errorMessage?: string;
  retryCount: number;
  maxRetries: number;
  confidenceScore?: number;
  originalFilename?: string;
  language?: string;
  extractedMetadata?: Record<string, any>;
}

interface OCRQueueStats {
  totalPending: number;
  totalProcessing: number;
  totalCompleted: number;
  totalFailed: number;
  averageProcessingTimeSeconds: number;
  averageConfidenceScore: number;
}

const OCRQueue: React.FC = () => {
  const [items, setItems] = useState<OCRQueueItem[]>([]);
  const [stats, setStats] = useState<OCRQueueStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [searchQuery, setSearchQuery] = useState('');
  const [page, setPage] = useState(1);
  const [autoRefresh, setAutoRefresh] = useState(true);

  // Fetch queue items
  const fetchQueue = async () => {
    try {
      const params = new URLSearchParams({
        page: page.toString(),
        pageSize: '20',
        ...(statusFilter && { status: statusFilter }),
        ...(searchQuery && { search: searchQuery }),
      });

      const response = await fetch(`/api/v1/ocr-queue?${params}`, {
        headers: { Authorization: `Bearer ${getAuthToken()}` },
      });

      if (response.ok) {
        const data = await response.json();
        setItems(data.items);
      }
    } catch (error) {
      console.error('Failed to fetch OCR queue:', error);
    } finally {
      setLoading(false);
    }
  };

  // Fetch statistics
  const fetchStats = async () => {
    try {
      const response = await fetch('/api/v1/ocr-queue/stats', {
        headers: { Authorization: `Bearer ${getAuthToken()}` },
      });

      if (response.ok) {
        const data = await response.json();
        setStats(data);
      }
    } catch (error) {
      console.error('Failed to fetch OCR stats:', error);
    }
  };

  // Reprocess document
  const handleReprocess = async (documentId: string) => {
    try {
      const response = await fetch(`/api/v1/ocr-queue/document/${documentId}/reprocess`, {
        method: 'POST',
        headers: {
          Authorization: `Bearer ${getAuthToken()}`,
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ priority: 1 }),
      });

      if (response.ok) {
        await fetchQueue();
        await fetchStats();
      }
    } catch (error) {
      console.error('Failed to reprocess document:', error);
    }
  };

  // Auto-refresh every 10 seconds when items are pending/processing
  useEffect(() => {
    fetchQueue();
    fetchStats();

    if (autoRefresh && (stats?.totalPending || stats?.totalProcessing)) {
      const interval = setInterval(() => {
        fetchQueue();
        fetchStats();
      }, 10000);

      return () => clearInterval(interval);
    }
  }, [page, statusFilter, searchQuery, autoRefresh, stats?.totalPending, stats?.totalProcessing]);

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'Pending':
        return <HourglassEmpty color="action" />;
      case 'Processing':
        return <CircularProgress size={20} />;
      case 'Completed':
        return <CheckCircle color="success" />;
      case 'Failed':
        return <Error color="error" />;
      default:
        return null;
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Pending':
        return 'default';
      case 'Processing':
        return 'info';
      case 'Completed':
        return 'success';
      case 'Failed':
        return 'error';
      default:
        return 'default';
    }
  };

  const formatDuration = (start: string, end?: string) => {
    if (!end) return '-';
    const duration = new Date(end).getTime() - new Date(start).getTime();
    return `${(duration / 1000).toFixed(1)}s`;
  };

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" gutterBottom>
        OCR Processing Queue
      </Typography>

      {/* Statistics Cards */}
      {stats && (
        <Grid container spacing={2} sx={{ mb: 3 }}>
          <Grid item xs={12} sm={6} md={3}>
            <Paper sx={{ p: 2, textAlign: 'center', bgcolor: '#fff3e0' }}>
              <Typography variant="h4" color="warning.main">
                {stats.totalPending}
              </Typography>
              <Typography variant="body2">Pending</Typography>
            </Paper>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Paper sx={{ p: 2, textAlign: 'center', bgcolor: '#e3f2fd' }}>
              <Typography variant="h4" color="info.main">
                {stats.totalProcessing}
              </Typography>
              <Typography variant="body2">Processing</Typography>
            </Paper>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Paper sx={{ p: 2, textAlign: 'center', bgcolor: '#e8f5e9' }}>
              <Typography variant="h4" color="success.main">
                {stats.totalCompleted}
              </Typography>
              <Typography variant="body2">Completed</Typography>
            </Paper>
          </Grid>
          <Grid item xs={12} sm={6} md={3}>
            <Paper sx={{ p: 2, textAlign: 'center', bgcolor: '#ffebee' }}>
              <Typography variant="h4" color="error.main">
                {stats.totalFailed}
              </Typography>
              <Typography variant="body2">Failed</Typography>
            </Paper>
          </Grid>
        </Grid>
      )}

      {/* Filters */}
      <Box sx={{ mb: 3, display: 'flex', gap: 2 }}>
        <TextField
          label="Search by filename"
          variant="outlined"
          size="small"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          InputProps={{
            startAdornment: <Search />,
          }}
          sx={{ flexGrow: 1 }}
        />
        <FormControl size="small" sx={{ minWidth: 150 }}>
          <InputLabel>Status</InputLabel>
          <Select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} label="Status">
            <MenuItem value="">All</MenuItem>
            <MenuItem value="Pending">Pending</MenuItem>
            <MenuItem value="Processing">Processing</MenuItem>
            <MenuItem value="Completed">Completed</MenuItem>
            <MenuItem value="Failed">Failed</MenuItem>
          </Select>
        </FormControl>
        <Button
          variant="outlined"
          startIcon={<Refresh />}
          onClick={() => {
            fetchQueue();
            fetchStats();
          }}
        >
          Refresh
        </Button>
      </Box>

      {/* Queue Items */}
      {loading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', p: 5 }}>
          <CircularProgress />
        </Box>
      ) : items.length === 0 ? (
        <Alert severity="info">No items in queue</Alert>
      ) : (
        <Grid container spacing={2}>
          {items.map((item) => (
            <Grid item xs={12} key={item.id}>
              <Card>
                <CardContent>
                  <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, flexGrow: 1 }}>
                      {getStatusIcon(item.status)}
                      <Box>
                        <Typography variant="h6">{item.originalFilename || 'Untitled'}</Typography>
                        <Typography variant="body2" color="text.secondary">
                          Created: {new Date(item.createdAt).toLocaleString()}
                        </Typography>
                      </Box>
                    </Box>

                    <Box sx={{ display: 'flex', gap: 1, alignItems: 'center' }}>
                      <Chip label={item.status} color={getStatusColor(item.status) as any} size="small" />
                      {item.priority > 0 && <Chip label={`Priority: ${item.priority}`} size="small" color="primary" />}
                      {item.status === 'Failed' && (
                        <Tooltip title="Retry OCR">
                          <IconButton size="small" onClick={() => handleReprocess(item.documentId)} color="primary">
                            <Refresh />
                          </IconButton>
                        </Tooltip>
                      )}
                    </Box>
                  </Box>

                  {/* Status Timeline */}
                  <Box sx={{ mt: 2 }}>
                    {item.status === 'Processing' && (
                      <Box>
                        <Typography variant="body2" gutterBottom>
                          Processing...
                        </Typography>
                        <LinearProgress />
                      </Box>
                    )}

                    {item.status === 'Completed' && item.confidenceScore !== undefined && (
                      <Box>
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 1 }}>
                          <Typography variant="body2">Confidence Score</Typography>
                          <Typography variant="body2" fontWeight="bold">
                            {(item.confidenceScore * 100).toFixed(1)}%
                          </Typography>
                        </Box>
                        <LinearProgress
                          variant="determinate"
                          value={item.confidenceScore * 100}
                          color={item.confidenceScore > 0.7 ? 'success' : 'warning'}
                        />
                        <Typography variant="caption" color="text.secondary">
                          Processing time: {formatDuration(item.processedAt!, item.completedAt)}
                        </Typography>
                      </Box>
                    )}

                    {item.status === 'Failed' && (
                      <Alert severity="error" sx={{ mt: 1 }}>
                        {item.errorMessage || 'OCR processing failed'}
                        {item.retryCount > 0 && ` (Attempt ${item.retryCount}/${item.maxRetries})`}
                      </Alert>
                    )}

                    {/* Extracted Metadata */}
                    {item.extractedMetadata && Object.keys(item.extractedMetadata).length > 0 && (
                      <Box sx={{ mt: 2, p: 1, bgcolor: '#f5f5f5', borderRadius: 1 }}>
                        <Typography variant="body2" fontWeight="bold" gutterBottom>
                          Extracted Data:
                        </Typography>
                        <Grid container spacing={1}>
                          {item.extractedMetadata.vatAmount && (
                            <Grid item xs={6}>
                              <Typography variant="caption">VAT Amount:</Typography>
                              <Typography variant="body2">€{item.extractedMetadata.vatAmount}</Typography>
                            </Grid>
                          )}
                          {item.extractedMetadata.invoiceNumber && (
                            <Grid item xs={6}>
                              <Typography variant="caption">Invoice #:</Typography>
                              <Typography variant="body2">{item.extractedMetadata.invoiceNumber}</Typography>
                            </Grid>
                          )}
                          {item.extractedMetadata.dateFound && (
                            <Grid item xs={6}>
                              <Typography variant="caption">Date:</Typography>
                              <Typography variant="body2">{item.extractedMetadata.dateFound}</Typography>
                            </Grid>
                          )}
                        </Grid>
                      </Box>
                    )}
                  </Box>
                </CardContent>
              </Card>
            </Grid>
          ))}
        </Grid>
      )}
    </Box>
  );
};

// Helper function to get auth token (implement based on your auth system)
function getAuthToken(): string {
  return localStorage.getItem('authToken') || '';
}

export default OCRQueue;
