// API request/response types
export interface ParsedIntent {
  type: string;
  target?: string;
  filters?: Record<string, any>;
  confidence: number;
  originalInput?: string;
}

export interface SubmitIntentRequest {
  userInput: string;
}

export interface SubmitIntentResponse {
  intentId: string;
  parsed: ParsedIntent;
  timestamp: string;
}

export interface TaskInfo {
  taskId: string;
  intentId: string;
  taskType: string;
  status: 'pending' | 'running' | 'completed' | 'failed';
  parameters: Record<string, any>;
  createdAt: string;
  updatedAt?: string;
  result?: any;
  error?: string;
}

export interface GetTasksResponse {
  tasks: TaskInfo[];
}

export interface ApprovalRequest {
  requestId: string;
  taskId: string;
  actionType: string;
  actionTarget?: string;
  context: string;
}

export interface ApproveActionRequest {
  requestId: string;
  approved: boolean;
  reason?: string;
}

export interface ApproveActionResponse {
  success: boolean;
  message?: string;
}
