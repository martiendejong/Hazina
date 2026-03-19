// UI Component Schema types
export interface UIComponentDefinition {
  id: string;
  type: 'visualization' | 'input' | 'action';
  category: string;
  description: string;
  schema: {
    type: string;
    required?: string[];
    properties: Record<string, any>;
  };
  validation?: {
    rules: Array<{
      name: string;
      condition: string;
      requires: string;
      message: string;
    }>;
  };
  security?: {
    requiresApproval: boolean;
    allowedOperations: string[];
    riskLevel: 'read' | 'write' | 'execute' | 'admin';
  };
}

// Task card component data
export interface TaskCardData {
  title: string;
  description?: string;
  steps: Array<{
    action: 'read' | 'write' | 'execute' | 'analyze' | 'delete';
    target: string;
    riskLevel?: 'read' | 'write' | 'execute' | 'admin';
    description?: string;
  }>;
  estimatedDuration?: string;
  requiresApproval?: boolean;
}

// File tree component data
export interface FileTreeNode {
  name: string;
  path: string;
  type: 'file' | 'directory';
  size?: number;
  modified?: string;
  children?: FileTreeNode[];
}

export interface FileTreeData {
  root: FileTreeNode;
  filter?: {
    extension?: string;
    pattern?: string;
    modifiedAfter?: string;
  };
}

// Approval panel component data
export interface ApprovalPanelData {
  requestId: string;
  taskId: string;
  actionType: string;
  actionTarget?: string;
  context: string;
  riskLevel: 'read' | 'write' | 'execute' | 'admin';
  estimatedImpact?: string;
}

// Progress tracker component data
export interface ProgressTrackerData {
  taskId: string;
  steps: Array<{
    id: string;
    label: string;
    status: 'pending' | 'running' | 'completed' | 'failed';
    progress?: number;
    message?: string;
  }>;
  overallProgress: number;
}

// Command preview component data
export interface CommandPreviewData {
  command: string;
  args?: string[];
  workingDirectory?: string;
  environment?: Record<string, string>;
  expectedOutput?: string;
  riskAssessment?: {
    level: 'read' | 'write' | 'execute' | 'admin';
    warnings: string[];
  };
}
