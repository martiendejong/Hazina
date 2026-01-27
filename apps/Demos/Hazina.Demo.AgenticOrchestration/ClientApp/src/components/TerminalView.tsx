import { useEffect, useRef, useState } from 'react'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import { WebLinksAddon } from '@xterm/addon-web-links'
import * as signalR from '@microsoft/signalr'
import '@xterm/xterm/css/xterm.css'

interface TerminalViewProps {
  sessionId: string
  onClose: () => void
}

export function TerminalView({ sessionId, onClose }: TerminalViewProps) {
  const terminalRef = useRef<HTMLDivElement>(null)
  const terminalInstance = useRef<Terminal | null>(null)
  const fitAddon = useRef<FitAddon | null>(null)
  const connection = useRef<signalR.HubConnection | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!terminalRef.current) return

    // Create terminal
    const terminal = new Terminal({
      cursorBlink: true,
      fontSize: 14,
      fontFamily: 'Cascadia Code, Consolas, Monaco, monospace',
      theme: {
        background: '#1e1e1e',
        foreground: '#d4d4d4',
        cursor: '#ffffff',
        selectionBackground: '#264f78',
      },
      scrollback: 10000,
      convertEol: true,
    })

    // Add addons
    const fit = new FitAddon()
    terminal.loadAddon(fit)
    terminal.loadAddon(new WebLinksAddon())

    // Open terminal
    terminal.open(terminalRef.current)
    fit.fit()

    terminalInstance.current = terminal
    fitAddon.current = fit

    // Handle resize
    const handleResize = () => {
      fit.fit()
      if (connection.current?.state === signalR.HubConnectionState.Connected) {
        connection.current.invoke('Resize', sessionId, terminal.cols, terminal.rows)
          .catch(err => console.error('Resize failed:', err))
      }
    }
    window.addEventListener('resize', handleResize)

    // Create SignalR connection
    const hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/terminal')
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build()

    // Handle output
    hubConnection.on('OnOutput', (sid: string, data: number[]) => {
      if (sid === sessionId) {
        const bytes = new Uint8Array(data)
        terminal.write(bytes)
      }
    })

    // Handle exit
    hubConnection.on('OnExit', (sid: string, exitCode: number) => {
      if (sid === sessionId) {
        terminal.writeln(`\r\n\x1b[90m[Process exited with code ${exitCode}]\x1b[0m`)
      }
    })

    // Handle error
    hubConnection.on('OnError', (sid: string, errorMsg: string) => {
      if (sid === sessionId) {
        terminal.writeln(`\r\n\x1b[31m[Error: ${errorMsg}]\x1b[0m`)
      }
    })

    // Handle user input
    terminal.onData(data => {
      if (hubConnection.state === signalR.HubConnectionState.Connected) {
        const encoder = new TextEncoder()
        const bytes = Array.from(encoder.encode(data))
        hubConnection.invoke('SendInput', sessionId, bytes)
          .catch(err => console.error('SendInput failed:', err))
      }
    })

    // Connection state handlers
    hubConnection.onreconnecting(() => {
      terminal.writeln('\r\n\x1b[33m[Reconnecting...]\x1b[0m')
      setIsConnected(false)
    })

    hubConnection.onreconnected(() => {
      terminal.writeln('\r\n\x1b[32m[Reconnected]\x1b[0m')
      setIsConnected(true)
      hubConnection.invoke('JoinSession', sessionId)
    })

    hubConnection.onclose(() => {
      setIsConnected(false)
    })

    // Connect and join session
    hubConnection.start()
      .then(() => {
        setIsConnected(true)
        terminal.writeln('\x1b[32m✓ Connected to terminal\x1b[0m\r\n')
        return hubConnection.invoke('JoinSession', sessionId)
      })
      .then(() => {
        terminal.writeln(`\x1b[90m[Joined session ${sessionId}]\x1b[0m\r\n`)
      })
      .catch(err => {
        console.error('Connection failed:', err)
        setError('Failed to connect to terminal hub')
        terminal.writeln('\x1b[31m✗ Failed to connect to terminal hub\x1b[0m')
      })

    connection.current = hubConnection

    // Cleanup
    return () => {
      window.removeEventListener('resize', handleResize)
      if (hubConnection.state === signalR.HubConnectionState.Connected) {
        hubConnection.invoke('LeaveSession', sessionId).catch(() => {})
        hubConnection.stop()
      }
      terminal.dispose()
    }
  }, [sessionId])

  const handleInterrupt = async () => {
    if (connection.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.current.invoke('SendSignal', sessionId, 'interrupt')
        terminalInstance.current?.writeln('\r\n\x1b[33m[Sent Ctrl+C]\x1b[0m')
      } catch (err) {
        console.error('SendSignal failed:', err)
      }
    }
  }

  const handleTerminate = async () => {
    if (connection.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await connection.current.invoke('Terminate', sessionId)
        terminalInstance.current?.writeln('\r\n\x1b[31m[Session terminated]\x1b[0m')
      } catch (err) {
        console.error('Terminate failed:', err)
      }
    }
  }

  return (
    <div className="terminal-view">
      <div className="terminal-toolbar">
        <div className="terminal-info">
          <span className="session-label">Session: {sessionId.slice(0, 12)}...</span>
          <span className={`connection-status ${isConnected ? 'connected' : 'disconnected'}`}>
            {isConnected ? 'Connected' : 'Disconnected'}
          </span>
        </div>
        <div className="terminal-actions">
          <button className="btn-interrupt" onClick={handleInterrupt} title="Send Ctrl+C">
            Ctrl+C
          </button>
          <button className="btn-terminate" onClick={handleTerminate} title="Terminate process">
            Terminate
          </button>
          <button className="btn-close" onClick={onClose} title="Close view">
            Close
          </button>
        </div>
      </div>
      {error && <div className="terminal-error">{error}</div>}
      <div className="terminal-container" ref={terminalRef} />
    </div>
  )
}
