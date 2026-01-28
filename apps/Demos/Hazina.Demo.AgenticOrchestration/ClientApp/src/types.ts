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
