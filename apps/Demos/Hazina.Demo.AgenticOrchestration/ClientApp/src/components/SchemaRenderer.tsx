import { TaskCard } from './schema/TaskCard'
import { FileTree } from './schema/FileTree'
import { ApprovalPanel } from './schema/ApprovalPanel'
import { ProgressTracker } from './schema/ProgressTracker'
import { CommandPreview } from './schema/CommandPreview'

export interface UIComponentPayload {
  componentId: string
  data: unknown
  onAction?: (action: string, data?: unknown) => void
}

/**
 * Dispatches UI component payloads to the appropriate renderer based on componentId.
 * Supports: ui.task-card, ui.file-tree, ui.approval-panel, ui.progress-tracker, ui.command-preview
 */
export function SchemaRenderer({ componentId, data, onAction }: UIComponentPayload) {
  switch (componentId) {
    case 'ui.task-card':
      return <TaskCard data={data as TaskCardData} onAction={onAction} />
    case 'ui.file-tree':
      return <FileTree data={data as FileTreeData} onAction={onAction} />
    case 'ui.approval-panel':
      return <ApprovalPanel data={data as ApprovalPanelData} onAction={onAction} />
    case 'ui.progress-tracker':
      return <ProgressTracker data={data as ProgressTrackerData} />
    case 'ui.command-preview':
      return <CommandPreview data={data as CommandPreviewData} onAction={onAction} />
    default:
      return (
        <div style={{ padding: '8px', border: '1px solid #555', borderRadius: 4, fontFamily: 'monospace', fontSize: 12 }}>
          <div style={{ color: '#aaa', marginBottom: 4 }}>Unknown component: {componentId}</div>
          <pre style={{ margin: 0, color: '#ccc', whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>
            {JSON.stringify(data, null, 2)}
          </pre>
        </div>
      )
  }
}

// ── Types (shared across schema renderers) ──────────────────────────────────

export interface TaskCardData {
  title: string
  steps: Array<{
    action: 'read' | 'write' | 'execute' | 'analyze' | 'delete'
    riskLevel: 'read' | 'write' | 'execute' | 'admin'
    description: string
  }>
  estimatedDuration?: string
  requiresApproval?: boolean
}

export interface FileTreeItem {
  path: string
  name: string
  type: 'file' | 'directory'
  size?: number
  modified?: string
  extension?: string
  children?: FileTreeItem[]
}

export interface FileTreeData {
  rootPath: string
  items: FileTreeItem[]
  expandedPaths?: string[]
  selectedPath?: string
}

export interface ApprovalPanelData {
  taskId: string
  action: string
  riskLevel: 'read' | 'write' | 'execute' | 'admin'
  details?: {
    affectedResources?: string[]
    reversible?: boolean
    estimatedDuration?: string
  }
  approvalState: 'pending' | 'approved' | 'denied'
  approvedBy?: string
  approvalTimestamp?: string
}

export interface ProgressStep {
  index: number
  description: string
  status: 'pending' | 'in-progress' | 'completed' | 'failed' | 'skipped'
  startTime?: string
  endTime?: string
  error?: string
}

export interface ProgressTrackerData {
  taskId: string
  title?: string
  totalSteps: number
  currentStep: number
  steps?: ProgressStep[]
  overallStatus?: 'not-started' | 'in-progress' | 'completed' | 'failed' | 'cancelled'
  startTime?: string
  endTime?: string
  estimatedCompletion?: string
}

export interface CommandPreviewData {
  command: string
  commandType?: 'shell' | 'powershell' | 'python' | 'sql' | 'api-call'
  workingDirectory?: string
  environment?: Record<string, string>
  arguments?: string[]
  riskLevel: 'read' | 'write' | 'execute' | 'admin'
  safetyAnalysis?: {
    destructiveOperations?: string[]
    affectedResources?: string[]
    reversible?: boolean
    requiresSudo?: boolean
  }
  expectedOutput?: string
  timeout?: number
}
