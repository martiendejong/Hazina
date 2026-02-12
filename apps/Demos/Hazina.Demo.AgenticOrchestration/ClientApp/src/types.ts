export interface TerminalSession {
  sessionId: string
  command: string
  /** Dynamic title extracted from terminal output (e.g., from "STATUS: Title" pattern). If null, use command as display title. */
  title?: string
  workingDirectory?: string
  startedAt: string
  isRunning: boolean
  waitingForInput: boolean
  exitCode?: number
}

export interface ExternalClaudeInstance {
  agentId: string
  sessionId?: string
  startedAt: string
  lastHeartbeat: string
  status: string
  currentTask?: string
  worktreeSeat?: string
  isExternal: boolean
}

export interface AllSessions {
  terminalSessions: TerminalSession[]
  externalInstances: ExternalClaudeInstance[]
  totalCount: number
}

export interface CreateSessionRequest {
  command?: string
  arguments?: string[]
  workingDirectory?: string
  columns?: number
  rows?: number
  mergeStderr?: boolean
  environment?: Record<string, string>
}

export interface TerminalConfig {
  defaultCommand: string
  defaultWorkingDirectory: string | null
  defaultArguments: string[]
  defaultColumns: number
  defaultRows: number
  maxConcurrentSessions: number
  sessionTimeoutMinutes: number
  signalRHubUrl: string
}

// Chat types
export interface ChatMessage {
  id: string
  role: 'user' | 'assistant' | 'system'
  content: string
  timestamp: string
  toolCalls?: ToolCall[]
  isStreaming?: boolean
}

export interface ToolCall {
  id: string
  name: string
  arguments: Record<string, unknown>
  result?: string
}

export interface ChatRequest {
  message: string
  conversationId?: string
}

export interface ChatResponse {
  conversationId: string
  message: ChatMessage
  isComplete: boolean
}

// Archive types
export interface ArchivedSession {
  sessionId: string
  command: string
  workingDirectory?: string
  startedAt: string
  endedAt?: string
  exitCode?: number
  logFilePath: string
  logSizeBytes: number
}

export interface ArchivedSessionsResponse {
  sessions: ArchivedSession[]
  totalCount: number
  page: number
  pageSize: number
  hasMore: boolean
}

export interface SessionLogResponse {
  sessionId: string
  content: string
  totalBytes: number
}

// Pending restore request - passed to TerminalView to paste content after Claude loads
export interface PendingRestore {
  sessionId: string
  content: string
}

// Version info from /api/terminal/version
export interface VersionInfo {
  version: string
  assemblyVersion: string
}

// Upload response from /api/terminal/upload
export interface UploadResponse {
  fileName: string
  originalName: string
  size: number
}

// Uploaded file from /api/terminal/uploads
export interface UploadedFile {
  fileName: string
  size: number
  uploadedAt: string
}
