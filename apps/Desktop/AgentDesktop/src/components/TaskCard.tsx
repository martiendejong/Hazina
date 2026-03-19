import React from 'react';
import { TaskCardData } from '../types/schema';

interface TaskCardProps {
  data: TaskCardData;
  onApprove?: () => void;
  onReject?: () => void;
}

export const TaskCard: React.FC<TaskCardProps> = ({ data, onApprove, onReject }) => {
  const getRiskColor = (riskLevel?: string) => {
    switch (riskLevel) {
      case 'read':
        return 'bg-green-100 text-green-800';
      case 'write':
        return 'bg-yellow-100 text-yellow-800';
      case 'execute':
        return 'bg-orange-100 text-orange-800';
      case 'admin':
        return 'bg-red-100 text-red-800';
      default:
        return 'bg-gray-100 text-gray-800';
    }
  };

  const getActionIcon = (action: string) => {
    switch (action) {
      case 'read':
        return '📖';
      case 'write':
        return '✏️';
      case 'execute':
        return '⚙️';
      case 'analyze':
        return '🔍';
      case 'delete':
        return '🗑️';
      default:
        return '📄';
    }
  };

  return (
    <div className="task-card border rounded-lg shadow-md p-4 bg-white">
      <div className="task-card-header mb-3">
        <h3 className="text-xl font-bold text-gray-800">{data.title}</h3>
        {data.description && <p className="text-sm text-gray-600 mt-1">{data.description}</p>}
      </div>

      <div className="task-card-steps space-y-2 mb-4">
        <h4 className="text-sm font-semibold text-gray-700">Steps:</h4>
        {data.steps.map((step, index) => (
          <div
            key={index}
            className="step flex items-start gap-2 p-2 rounded bg-gray-50 hover:bg-gray-100 transition"
          >
            <span className="text-2xl">{getActionIcon(step.action)}</span>
            <div className="flex-1">
              <div className="flex items-center gap-2">
                <span className="font-mono text-sm font-semibold">{step.action}</span>
                <span className={`px-2 py-0.5 rounded text-xs ${getRiskColor(step.riskLevel)}`}>
                  {step.riskLevel || 'read'}
                </span>
              </div>
              <div className="text-sm text-gray-700 mt-1">
                <code className="bg-gray-200 px-1 rounded">{step.target}</code>
              </div>
              {step.description && <p className="text-xs text-gray-500 mt-1">{step.description}</p>}
            </div>
          </div>
        ))}
      </div>

      {data.estimatedDuration && (
        <div className="text-sm text-gray-600 mb-3">
          <span className="font-semibold">Estimated Duration:</span> {data.estimatedDuration}
        </div>
      )}

      {data.requiresApproval && (
        <div className="task-card-actions flex gap-2 pt-3 border-t">
          <button
            onClick={onApprove}
            className="flex-1 bg-green-500 hover:bg-green-600 text-white font-semibold py-2 px-4 rounded transition"
          >
            ✓ Approve
          </button>
          <button
            onClick={onReject}
            className="flex-1 bg-red-500 hover:bg-red-600 text-white font-semibold py-2 px-4 rounded transition"
          >
            ✗ Reject
          </button>
        </div>
      )}
    </div>
  );
};
