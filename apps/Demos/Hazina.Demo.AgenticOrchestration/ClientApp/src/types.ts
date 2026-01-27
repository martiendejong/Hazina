export interface TerminalSession {
  sessionId: string
  command: string
  workingDirectory?: string
  startedAt: string
  isRunning: boolean
  exitCode?: number
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
