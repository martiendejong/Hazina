import { useEffect, useRef, useState } from 'react'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import { WebLinksAddon } from '@xterm/addon-web-links'
import { Unicode11Addon } from '@xterm/addon-unicode11'
import * as signalR from '@microsoft/signalr'
import '@xterm/xterm/css/xterm.css'

interface TerminalViewProps {
  sessionId: string
  onClose: () => void
  onStateChanged?: (sessionId: string, isRunning: boolean, waitingForInput: boolean) => void
  onTitleChanged?: (sessionId: string, title: string) => void
}

export function TerminalView({ sessionId, onClose, onStateChanged, onTitleChanged }: TerminalViewProps) {
  const terminalRef = useRef<HTMLDivElement>(null)
  const terminalInstance = useRef<Terminal | null>(null)
  const fitAddon = useRef<FitAddon | null>(null)
  const connection = useRef<signalR.HubConnection | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!terminalRef.current) return

    // Create terminal with proper settings for ConPTY
    const terminal = new Terminal({
      cursorBlink: true,
      fontSize: 14,
      fontFamily: 'Cascadia Code, Consolas, Monaco, monospace',
      theme: {
        background: '#1e1e1e',
        foreground: '#d4d4d4',
        cursor: '#ffffff',
        cursorAccent: '#1e1e1e',
        selectionBackground: '#264f78',
        black: '#000000',
        red: '#cd3131',
        green: '#0dbc79',
        yellow: '#e5e510',
        blue: '#2472c8',
        magenta: '#bc3fbc',
        cyan: '#11a8cd',
        white: '#e5e5e5',
        brightBlack: '#666666',
        brightRed: '#f14c4c',
        brightGreen: '#23d18b',
        brightYellow: '#f5f543',
        brightBlue: '#3b8eea',
        brightMagenta: '#d670d6',
        brightCyan: '#29b8db',
        brightWhite: '#ffffff',
      },
      scrollback: 10000,
      allowProposedApi: true,
      // Don't convert EOL - ConPTY sends proper \r\n sequences
      convertEol: false,
    })

    // Add addons
    const fit = new FitAddon()
    terminal.loadAddon(fit)
    terminal.loadAddon(new WebLinksAddon())

    // Add Unicode 11 support for proper emoji and symbol rendering
    const unicode11 = new Unicode11Addon()
    terminal.loadAddon(unicode11)
    terminal.unicode.activeVersion = '11'

    // Open terminal
    terminal.open(terminalRef.current)

    // Initial fit after DOM is ready
    requestAnimationFrame(() => {
      fit.fit()
    })

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

    // Use ResizeObserver for more reliable resize detection
    const resizeObserver = new ResizeObserver(() => {
      // Debounce resize
      setTimeout(() => {
        fit.fit()
        if (connection.current?.state === signalR.HubConnectionState.Connected) {
          connection.current.invoke('Resize', sessionId, terminal.cols, terminal.rows)
            .catch(err => console.error('Resize failed:', err))
        }
      }, 50)
    })
    resizeObserver.observe(terminalRef.current)

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

    // Handle state changes (Running/Question)
    hubConnection.on('OnStateChanged', (sid: string, isRunning: boolean, waitingForInput: boolean) => {
      if (sid === sessionId && onStateChanged) {
        onStateChanged(sid, isRunning, waitingForInput)
      }
    })

    // Handle title changes (detected from "STATUS: Title" in output)
    hubConnection.on('OnTitleChanged', (sid: string, title: string) => {
      if (sid === sessionId && onTitleChanged) {
        onTitleChanged(sid, title)
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

    // Connect, resize, then load history
    const connectAndLoadHistory = async () => {
      // First connect to SignalR
      try {
        await hubConnection.start()
        setIsConnected(true)
        await hubConnection.invoke('JoinSession', sessionId)

        // IMPORTANT: Sync terminal size BEFORE loading history
        // This ensures ConPTY uses the correct dimensions
        fit.fit()
        await hubConnection.invoke('Resize', sessionId, terminal.cols, terminal.rows)
        console.log(`Synced terminal size: ${terminal.cols}x${terminal.rows}`)

        // Small delay to let ConPTY process the resize
        await new Promise(resolve => setTimeout(resolve, 100))

        // Now fetch and display historical output
        try {
          const historyResponse = await fetch(`/api/terminal/sessions/${sessionId}/history`)
          if (historyResponse.ok) {
            const historyData = await historyResponse.json()
            if (historyData.data && historyData.data.length > 0) {
              const bytes = new Uint8Array(historyData.data)
              terminal.write(bytes)
            }
          }
        } catch (err) {
          console.warn('Failed to load history:', err)
        }
      } catch (err) {
        console.error('Connection failed:', err)
        setError('Failed to connect to terminal hub')
        terminal.writeln('\x1b[31m✗ Failed to connect to terminal hub\x1b[0m')
      }
    }

    connectAndLoadHistory()

    connection.current = hubConnection

    // Cleanup
    return () => {
      window.removeEventListener('resize', handleResize)
      resizeObserver.disconnect()
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
