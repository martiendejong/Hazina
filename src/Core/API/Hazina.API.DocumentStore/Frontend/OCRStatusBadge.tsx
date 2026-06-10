import React, { useEffect, useState } from 'react';
import { Chip, CircularProgress, Tooltip } from '@mui/material';
import { CheckCircle, Error, HourglassEmpty } from '@mui/icons-material';

interface OCRStatusBadgeProps {
  documentId: string;
  autoRefresh?: boolean;
}

interface OCRStatus {
  status: 'Pending' | 'Processing' | 'Completed' | 'Failed';
  confidenceScore?: number;
  errorMessage?: string;
}

const OCRStatusBadge: React.FC<OCRStatusBadgeProps> = ({ documentId, autoRefresh = true }) => {
  const [status, setStatus] = useState<OCRStatus | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchStatus = async () => {
    try {
      const response = await fetch(`/api/v1/ocr-queue/document/${documentId}`, {
        headers: { Authorization: `Bearer ${getAuthToken()}` },
      });

      if (response.ok) {
        const data = await response.json();
        setStatus({
          status: data.status,
          confidenceScore: data.confidenceScore,
          errorMessage: data.errorMessage,
        });
      } else {
        setStatus(null);
      }
    } catch (error) {
      console.error('Failed to fetch OCR status:', error);
      setStatus(null);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStatus();

    if (autoRefresh && status && ['Pending', 'Processing'].includes(status.status)) {
      const interval = setInterval(fetchStatus, 10000);
      return () => clearInterval(interval);
    }
  }, [documentId, autoRefresh, status?.status]);

  if (loading) {
    return <CircularProgress size={20} />;
  }

  if (!status) {
    return null;
  }

  const getChipProps = () => {
    switch (status.status) {
      case 'Pending':
        return {
          label: 'OCR Pending',
          color: 'default' as const,
          icon: <HourglassEmpty />,
        };
      case 'Processing':
        return {
          label: 'Processing OCR...',
          color: 'info' as const,
          icon: <CircularProgress size={16} />,
        };
      case 'Completed':
        return {
          label: `OCR ${status.confidenceScore ? `(${(status.confidenceScore * 100).toFixed(0)}%)` : 'Complete'}`,
          color: (status.confidenceScore && status.confidenceScore > 0.7 ? 'success' : 'warning') as const,
          icon: <CheckCircle />,
        };
      case 'Failed':
        return {
          label: 'OCR Failed',
          color: 'error' as const,
          icon: <Error />,
        };
      default:
        return {
          label: status.status,
          color: 'default' as const,
        };
    }
  };

  const chipProps = getChipProps();
  const tooltip =
    status.status === 'Failed' && status.errorMessage
      ? status.errorMessage
      : status.status === 'Completed' && status.confidenceScore
        ? `Confidence: ${(status.confidenceScore * 100).toFixed(1)}%`
        : '';

  return (
    <Tooltip title={tooltip} arrow>
      <Chip {...chipProps} size="small" variant="outlined" />
    </Tooltip>
  );
};

function getAuthToken(): string {
  return localStorage.getItem('authToken') || '';
}

export default OCRStatusBadge;
