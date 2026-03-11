import { useEffect, useRef, useState, useCallback } from 'react'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import { WebLinksAddon } from '@xterm/addon-web-links'
import { Unicode11Addon } from '@xterm/addon-unicode11'
import * as signalR from '@microsoft/signalr'
import { authFetch, getAuthHeader } from '../auth'
import { useVoiceControl } from '../hooks/useVoiceControl'
import type { PendingRestore } from '../types'
import '@xterm/xterm/css/xterm.css'

interface TerminalViewProps {
  sessionId: string
  onClose: () => void
  onStateChanged?: (sessionId: string, isRunning: boolean, waitingForInput: boolean) => void
  onTitleChanged?: (sessionId: string, title: string) => void
  pendingRestore?: PendingRestore
  onRestoreComplete?: () => void
  onEditProperties?: (sessionId: string) => void
}

export function TerminalView({ sessionId, onClose, onStateChanged, onTitleChanged, pendingRestore, onRestoreComplete, onEditProperties }: TerminalViewProps) {
  console.log('[DEBUG TerminalView] Component rendered with props:', {
    sessionId,
    hasPendingRestore: !!pendingRestore,
    pendingRestoreSessionId: pendingRestore?.sessionId,
    contentLength: pendingRestore?.content?.length
  })

  const terminalRef = useRef<HTMLDivElement>(null)
  const terminalInstance = useRef<Terminal | null>(null)
  const fitAddon = useRef<FitAddon | null>(null)
  const connection = useRef<signalR.HubConnection | null>(null)
  const resizeTimeoutRef = useRef<number | null>(null)
  const lastDimensionsRef = useRef<{ cols: number; rows: number } | null>(null)
  const [isConnected, setIsConnected] = useState(false)
  const [displayConnected, setDisplayConnected] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isWaitingForInput, setIsWaitingForInput] = useState(false)
  const restoreHandledRef = useRef(false)
  const disconnectTimeoutRef = useRef<number | null>(null)
  const connectionStableTimeRef = useRef<number | null>(null)
  const [mobileInput, setMobileInput] = useState('')
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [showSpecialKeys, setShowSpecialKeys] = useState(false)

  // Voice control - send transcribed speech to terminal
  const handleVoiceTranscript = useCallback((text: string) => {
    if (connection.current?.state === signalR.HubConnectionState.Connected) {
      // Sanitize: trim whitespace, limit length, remove control characters
      const sanitized = text
        .trim()
        .slice(0, 1000) // Reasonable max length
        .replace(/[\x00-\x1F\x7F]/g, '') // Remove control chars except those we add

      if (!sanitized) return

      // Send the text as input with a carriage return to execute
      // ConPTY expects \r (carriage return) for Enter, not \n (line feed)
      const encoder = new TextEncoder()
      const bytes = Array.from(encoder.encode(sanitized + '\r'))
      connection.current.invoke('SendInput', sessionId, bytes)
        .catch(err => console.error('SendInput (voice) failed:', err))

      // Also show indicator in terminal (escape for display)
      const displayText = sanitized.length > 50 ? sanitized.slice(0, 50) + '...' : sanitized
      terminalInstance.current?.writeln(`\r\n\x1b[36m[Voice: "${displayText}"]\x1b[0m`)
    }
  }, [sessionId])

  const [voiceState, voiceActions] = useVoiceControl(handleVoiceTranscript)

  // Mobile input handler - send whole message at once
  const handleSendMobileInput = useCallback(() => {
    if (connection.current?.state === signalR.HubConnectionState.Connected && mobileInput.trim()) {
      const encoder = new TextEncoder()
      // ConPTY expects \r (carriage return) for Enter, not \n (line feed)
      const bytes = Array.from(encoder.encode(mobileInput + '\r'))
      connection.current.invoke('SendInput', sessionId, bytes)
        .catch(err => console.error('SendInput (mobile) failed:', err))
      setMobileInput('')
    }
  }, [sessionId, mobileInput])

  // File upload handler
  const handleFileUpload = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return

    const formData = new FormData()
    formData.append('file', file)

    try {
      const response = await authFetch('/api/terminal/upload', {
        method: 'POST',
        body: formData
      })

      if (response.ok) {
        const result = await response.json()
        terminalInstance.current?.writeln(`\r\n\x1b[32m[Uploaded: ${result.originalName} (${(result.size / 1024).toFixed(1)} KB)]\x1b[0m`)
        terminalInstance.current?.writeln(`\x1b[90m[File: ${result.fileName}]\x1b[0m`)
        // Pre-fill the input with the filename for easy reference
        setMobileInput(result.fileName)
      } else {
        try {
          const err = await response.json()
          terminalInstance.current?.writeln(`\r\n\x1b[31m[Upload failed: ${err.error}]\x1b[0m`)
        } catch {
          terminalInstance.current?.writeln(`\r\n\x1b[31m[Upload failed: ${response.statusText}]\x1b[0m`)
        }
      }
    } catch (err) {
      console.error('Upload failed:', err)
      terminalInstance.current?.writeln(`\r\n\x1b[31m[Upload failed: network error]\x1b[0m`)
    }

    // Reset file input so same file can be selected again
    if (fileInputRef.current) fileInputRef.current.value = ''
  }, [])

  // Send special key sequence (escape codes) to terminal
  const sendSpecialKey = useCallback((sequence: number[]) => {
    if (connection.current?.state === signalR.HubConnectionState.Connected) {
      connection.current.invoke('SendInput', sessionId, sequence)
        .catch(err => console.error('SendSpecialKey failed:', err))
    }
  }, [sessionId])

  // Keyboard shortcut: Ctrl+M to toggle voice
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.ctrlKey && e.key === 'm') {
        e.preventDefault()
        voiceActions.toggle()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [voiceActions])

  // Debounce connection status display to prevent flickering
  useEffect(() => {
    if (isConnected) {
      // Immediately show connected
      if (disconnectTimeoutRef.current) {
        clearTimeout(disconnectTimeoutRef.current)
        disconnectTimeoutRef.current = null
      }
      setDisplayConnected(true)
      // Mark when connection became stable
      connectionStableTimeRef.current = Date.now()
    } else {
      // Wait 1000ms before showing disconnected
      disconnectTimeoutRef.current = window.setTimeout(() => {
        setDisplayConnected(false)
      }, 1000)
      // Connection is not stable
      connectionStableTimeRef.current = null
    }

    return () => {
      if (disconnectTimeoutRef.current) {
        clearTimeout(disconnectTimeoutRef.current)
      }
    }
  }, [isConnected])

  // Maintain terminal focus after state changes to prevent focus loss
  useEffect(() => {
    if (terminalInstance.current && isConnected) {
      // Focus the terminal when state changes
      terminalInstance.current.focus()
    }
  }, [isWaitingForInput, isConnected])

  // Detect if we're on a mobile device
  const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)

  useEffect(() => {
    if (!terminalRef.current) return

    // Create terminal with proper settings for ConPTY
    const terminal = new Terminal({
      cursorBlink: true,
      fontSize: isMobile ? 12 : 14,  // Smaller font on mobile
      fontFamily: 'Cascadia Code, Consolas, Monaco, monospace',
      theme: {
        background: '#1e1e1e',
        foreground: '#d4d4d4',
        cursor: '#ffffff',
        cursorAccent: '#1e1e1e',
        selectionBackground: '#264f78',
        selectionForeground: '#ffffff',
        selectionInactiveBackground: '#264f78',  // Same as active to prevent grey
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
      scrollback: isMobile ? 1000 : 10000,  // Less scrollback on mobile for performance
      allowProposedApi: true,
      // Don't convert EOL - ConPTY sends proper \r\n sequences
      convertEol: false,
      // Mobile optimizations
      scrollOnUserInput: true,
      fastScrollSensitivity: isMobile ? 1 : 5,
      smoothScrollDuration: isMobile ? 0 : 125,  // Disable smooth scroll on mobile
      // Clipboard support
      rightClickSelectsWord: true,  // Right-click selects word for easy copy
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
    // Establish positioning context for toast overlays (clipboard denied, etc.)
    terminalRef.current.style.position = 'relative'

    // Initial fit after DOM is ready
    requestAnimationFrame(() => {
      fit.fit()
    })

    terminalInstance.current = terminal
    fitAddon.current = fit

    // Debounced resize handler that prevents rapid resizing on mobile
    const doResize = () => {
      if (!fitAddon.current || !terminalInstance.current) return

      try {
        fitAddon.current.fit()
        const newCols = terminalInstance.current.cols
        const newRows = terminalInstance.current.rows

        // Only send resize to server if dimensions actually changed
        const last = lastDimensionsRef.current
        if (!last || last.cols !== newCols || last.rows !== newRows) {
          lastDimensionsRef.current = { cols: newCols, rows: newRows }

          if (connection.current?.state === signalR.HubConnectionState.Connected) {
            connection.current.invoke('Resize', sessionId, newCols, newRows)
              .catch(err => console.error('Resize failed:', err))
          }
        }
      } catch (err) {
        console.error('Fit error:', err)
      }
    }

    // Heavily debounced resize for mobile (keyboard open/close causes many events)
    const debouncedResize = () => {
      if (resizeTimeoutRef.current) {
        clearTimeout(resizeTimeoutRef.current)
      }
      // Use longer debounce on mobile to avoid jumping
      const debounceMs = isMobile ? 300 : 100
      resizeTimeoutRef.current = window.setTimeout(doResize, debounceMs)
    }

    // Handle window resize
    const handleResize = () => {
      debouncedResize()
    }
    window.addEventListener('resize', handleResize)

    // Use ResizeObserver for container size changes (but not on mobile to avoid keyboard issues)
    let resizeObserver: ResizeObserver | null = null
    if (!isMobile) {
      resizeObserver = new ResizeObserver(() => {
        debouncedResize()
      })
      resizeObserver.observe(terminalRef.current)
    }

    // Create SignalR connection with auth headers
    const authHeader = getAuthHeader()
    const hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/terminal', {
        headers: authHeader ? { 'Authorization': authHeader } : undefined
      })
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
      if (sid === sessionId) {
        // Only update state if value actually changed to prevent unnecessary re-renders
        setIsWaitingForInput(prev => prev === waitingForInput ? prev : waitingForInput)
        if (onStateChanged) {
          onStateChanged(sid, isRunning, waitingForInput)
        }
      }
    })

    // Handle title changes (detected from "STATUS: Title" in output)
    hubConnection.on('OnTitleChanged', (sid: string, title: string) => {
      if (sid === sessionId && onTitleChanged) {
        onTitleChanged(sid, title)
      }
    })

    // Toast helper for clipboard/connection errors (non-intrusive, won't corrupt TUI apps)
    const showToast = (message: string, color = '#d29922') => {
      if (!terminalRef.current) return
      const toast = document.createElement('div')
      toast.className = 'terminal-toast'
      toast.textContent = message
      toast.style.cssText = `
        position: absolute; bottom: 8px; left: 50%; transform: translateX(-50%);
        background: ${color}; color: #1e1e1e; padding: 6px 16px; border-radius: 4px;
        font-size: 13px; z-index: 1001; pointer-events: none;
      `
      terminalRef.current.appendChild(toast)
      setTimeout(() => toast.remove(), 3000)
    }

    // Chunked send: splits large input into 4KB chunks to avoid SignalR message size limits.
    // Uses a lock to prevent interleaving when user types during a multi-chunk paste.
    const PASTE_CHUNK_SIZE = 4096
    let sendingChunks = false
    const pendingInputs: Uint8Array[] = []

    const sendBytes = async (bytes: Uint8Array) => {
      if (hubConnection.state !== signalR.HubConnectionState.Connected) return

      if (bytes.length <= PASTE_CHUNK_SIZE) {
        // Small input - send directly
        try {
          await hubConnection.invoke('SendInput', sessionId, Array.from(bytes))
        } catch {
          // Show error for paste-sized input (>10 bytes), ignore single keystroke failures
          if (bytes.length > 10) showToast('Send failed - connection error', '#f14c4c')
        }
        return
      }

      // Large input - send in chunks with delays. Retry each chunk once on failure.
      for (let offset = 0; offset < bytes.length; offset += PASTE_CHUNK_SIZE) {
        const chunk = Array.from(bytes.slice(offset, offset + PASTE_CHUNK_SIZE))
        try {
          await hubConnection.invoke('SendInput', sessionId, chunk)
        } catch {
          // Retry once after a short delay
          await new Promise(resolve => setTimeout(resolve, 50))
          try {
            await hubConnection.invoke('SendInput', sessionId, chunk)
          } catch {
            // Chunk permanently failed - remaining chunks would arrive out of context
            showToast('Paste incomplete - connection error', '#f14c4c')
            return
          }
        }
        if (offset + PASTE_CHUNK_SIZE < bytes.length) {
          await new Promise(resolve => setTimeout(resolve, 10))
        }
      }
    }

    const enqueueInput = (bytes: Uint8Array) => {
      pendingInputs.push(bytes)
      if (sendingChunks) return // Already processing queue
      const processQueue = async () => {
        sendingChunks = true
        while (pendingInputs.length > 0) {
          const next = pendingInputs.shift()!
          try {
            await sendBytes(next)
          } catch (err) {
            console.error('SendInput failed:', err)
          }
        }
        sendingChunks = false
      }
      processQueue()
    }

    // Convenience wrapper for string input (used by context menu paste)
    const sendPasteChunked = (text: string) => {
      if (!text) return
      enqueueInput(new TextEncoder().encode(text))
    }

    // Handle user input - all input goes through the queue to prevent interleaving
    terminal.onData(data => {
      if (hubConnection.state === signalR.HubConnectionState.Connected) {
        enqueueInput(new TextEncoder().encode(data))
      }
    })

    // Custom keyboard handler for copy/paste
    // Strategy: Use browser-native paste flow (reliable, no permission issues)
    // instead of navigator.clipboard.readText() which silently fails on permission denial.
    // Native flow: Ctrl+V keydown -> browser paste event -> xterm textarea -> onData -> chunked send
    // NOTE: This relies on @xterm/xterm 6.x firing onData from native paste events independently
    // of attachCustomKeyEventHandler. Retest paste on xterm major version upgrades.
    terminal.attachCustomKeyEventHandler((event) => {
      // Handle Ctrl+C - copy if selection exists, otherwise send interrupt
      if (event.ctrlKey && !event.shiftKey && event.key === 'c' && event.type === 'keydown') {
        const selection = terminal.getSelection()
        if (selection && selection.length > 0) {
          event.preventDefault() // Stop native copy from overwriting our clipboard write
          navigator.clipboard.writeText(selection).catch(err => {
            console.error('Copy failed:', err)
          })
          return false // Prevent xterm from processing
        }
        // No selection - let it through as interrupt signal (sends \x03)
        return true
      }

      // Handle Ctrl+V - let browser native paste work
      // Return false to prevent xterm sending \x16 control code,
      // but do NOT preventDefault so the browser paste event fires normally.
      // Xterm catches the paste via its hidden textarea and fires onData.
      if (event.ctrlKey && !event.shiftKey && event.key === 'v' && event.type === 'keydown') {
        return false
      }

      // Handle Ctrl+Shift+C - always copy (alternative shortcut)
      if (event.ctrlKey && event.shiftKey && event.key === 'C' && event.type === 'keydown') {
        event.preventDefault()
        const selection = terminal.getSelection()
        if (selection && selection.length > 0) {
          navigator.clipboard.writeText(selection).catch(err => {
            console.error('Copy failed:', err)
          })
        }
        return false
      }

      // Handle Ctrl+Shift+V - explicit clipboard read with preventDefault to avoid double paste.
      // Chrome fires native paste for Ctrl+Shift+V, so without preventDefault both readText
      // AND native paste would fire, sending the text twice through the queue.
      if (event.ctrlKey && event.shiftKey && event.key === 'V' && event.type === 'keydown') {
        event.preventDefault()
        navigator.clipboard.readText()
          .then(text => text && sendPasteChunked(text))
          .catch(() => showToast('Clipboard access denied. Use Ctrl+V to paste.'))
        return false
      }

      return true // Allow all other keys
    })

    // Document-level paste fallback: catches paste when xterm's textarea doesn't have focus
    // but the terminal container is visible. Fixes "need to press space first" issue.
    const handleDocumentPaste = (e: ClipboardEvent) => {
      // Only handle if terminal container is visible and no other input is focused
      const activeEl = document.activeElement
      const isInputFocused = activeEl instanceof HTMLInputElement ||
        activeEl instanceof HTMLTextAreaElement ||
        (activeEl as HTMLElement)?.isContentEditable
      // Skip if user is typing in an input/textarea (e.g. mobile input box)
      if (isInputFocused) return
      // Skip if terminal container is not in the DOM
      if (!terminalRef.current) return

      e.preventDefault()
      e.stopPropagation()
      const text = e.clipboardData?.getData('text')
      if (text) {
        // Focus terminal so subsequent keystrokes go there
        terminal.focus()
        sendPasteChunked(text)
      }
    }
    document.addEventListener('paste', handleDocumentPaste)

    // Click-to-focus: ensure clicking terminal area always focuses xterm
    const handleContainerClick = () => {
      terminal.focus()
    }
    terminalRef.current.addEventListener('click', handleContainerClick)

    // Right-click context menu for copy/paste
    const handleContextMenu = (e: MouseEvent) => {
      e.preventDefault()
      const selection = terminal.getSelection()

      // Create context menu
      const existingMenu = document.querySelector('.terminal-context-menu')
      if (existingMenu) existingMenu.remove()

      const menu = document.createElement('div')
      menu.className = 'terminal-context-menu'
      menu.style.cssText = `
        position: fixed;
        left: ${e.clientX}px;
        top: ${e.clientY}px;
        background: #21262d;
        border: 1px solid #30363d;
        border-radius: 6px;
        padding: 4px 0;
        z-index: 1000;
        box-shadow: 0 8px 24px rgba(0,0,0,0.4);
        min-width: 120px;
      `

      const createMenuItem = (text: string, action: () => void, disabled = false) => {
        const item = document.createElement('div')
        item.textContent = text
        item.style.cssText = `
          padding: 8px 16px;
          cursor: ${disabled ? 'default' : 'pointer'};
          color: ${disabled ? '#6e7681' : '#c9d1d9'};
          font-size: 14px;
        `
        if (!disabled) {
          item.onmouseenter = () => item.style.background = '#30363d'
          item.onmouseleave = () => item.style.background = 'transparent'
          item.onclick = () => {
            action()
            menu.remove()
          }
        }
        return item
      }

      // Copy option
      menu.appendChild(createMenuItem(
        'Copy',
        () => {
          if (selection) {
            navigator.clipboard.writeText(selection)
          }
        },
        !selection || selection.length === 0
      ))

      // Paste option - uses Clipboard API (requires permission)
      menu.appendChild(createMenuItem(
        'Paste',
        () => {
          navigator.clipboard.readText()
            .then(text => {
              if (text) {
                terminal.focus()
                sendPasteChunked(text)
              }
            })
            .catch(() => {
              terminal.focus()
              showToast('Clipboard access denied. Use Ctrl+V to paste.')
            })
        }
      ))

      // Select All option
      menu.appendChild(createMenuItem(
        'Select All',
        () => terminal.selectAll()
      ))

      document.body.appendChild(menu)

      // Close menu on click outside
      const closeMenu = (ev: MouseEvent) => {
        if (!menu.contains(ev.target as Node)) {
          menu.remove()
          document.removeEventListener('click', closeMenu)
        }
      }
      setTimeout(() => document.addEventListener('click', closeMenu), 0)
    }

    terminalRef.current.addEventListener('contextmenu', handleContextMenu)

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
          const historyResponse = await authFetch(`/api/terminal/sessions/${sessionId}/history`)
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
      document.removeEventListener('paste', handleDocumentPaste)
      if (resizeObserver) {
        resizeObserver.disconnect()
      }
      if (resizeTimeoutRef.current) {
        clearTimeout(resizeTimeoutRef.current)
      }
      // Remove context menu and click handlers
      if (terminalRef.current) {
        terminalRef.current.removeEventListener('contextmenu', handleContextMenu)
        terminalRef.current.removeEventListener('click', handleContainerClick)
      }
      // Remove any lingering context menu
      const existingMenu = document.querySelector('.terminal-context-menu')
      if (existingMenu) existingMenu.remove()

      // Drain input queue to stop stale async processing
      pendingInputs.length = 0
      sendingChunks = false

      if (hubConnection.state === signalR.HubConnectionState.Connected) {
        hubConnection.invoke('LeaveSession', sessionId).catch(() => {})
        hubConnection.stop()
      }
      terminal.dispose()
    }
  }, [sessionId, isMobile])

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

  // Handle pending restore - paste content when Claude is ready (waiting for input)
  useEffect(() => {
    console.log('[DEBUG TerminalView] Restore effect triggered:', {
      hasPendingRestore: !!pendingRestore,
      pendingRestoreSessionId: pendingRestore?.sessionId,
      currentSessionId: sessionId,
      isConnected,
      restoreHandled: restoreHandledRef.current,
      hubConnectionState: connection.current?.state,
      contentLength: pendingRestore?.content?.length,
      hasTerminalInstance: !!terminalInstance.current
    })

    // Only proceed if:
    // 1. We have pending restore content for this session
    // 2. We're connected
    // 3. We haven't already handled this restore
    // 4. Either Claude is waiting for input OR 5 seconds have passed
    if (
      pendingRestore &&
      pendingRestore.sessionId === sessionId &&
      isConnected &&
      !restoreHandledRef.current &&
      connection.current?.state === signalR.HubConnectionState.Connected
    ) {
      const performRestore = async () => {
        console.log('[DEBUG TerminalView] Performing restore with content length:', pendingRestore.content.length)
        restoreHandledRef.current = true

        // Show restore message in terminal
        terminalInstance.current?.writeln('\r\n\x1b[36m[Restoring archived session...]\x1b[0m\r\n')

        // Write the archived content directly to terminal output (not as input)
        // This makes the previous conversation visible
        terminalInstance.current?.write(pendingRestore.content)

        // Add separator
        terminalInstance.current?.writeln('\r\n\r\n\x1b[36m' + '─'.repeat(80) + '\x1b[0m')
        terminalInstance.current?.writeln('\x1b[33m[Sending context to Claude...]\x1b[0m\r\n')

        // IMPORTANT: Also send the context to Claude so it understands the conversation history
        // We send it as input with a clear instruction that this is restored context
        try {
          const contextInstruction = `This is a RESTORED SESSION. The conversation history above should be understood as prior context. Please briefly acknowledge that you understand this context and are ready to continue helping with the same task.\r`

          const encoder = new TextEncoder()
          const bytes = Array.from(encoder.encode(contextInstruction))

          if (connection.current?.state === signalR.HubConnectionState.Connected) {
            await connection.current.invoke('SendInput', sessionId, bytes)
            console.log('[DEBUG TerminalView] Context instruction sent to Claude')
            terminalInstance.current?.writeln('\x1b[32m[Context sent - Claude will acknowledge shortly]\x1b[0m\r\n')
          }
        } catch (err) {
          console.error('[DEBUG TerminalView] Failed to send context to Claude:', err)
          terminalInstance.current?.writeln('\x1b[31m[Warning: Failed to send context to Claude]\x1b[0m\r\n')
        }

        console.log('[DEBUG TerminalView] Session content displayed successfully')

        if (onRestoreComplete) {
          onRestoreComplete()
        }
      }

      // Display content immediately when terminal is ready
      // No need to wait for waitingForInput - we're just showing output
      if (terminalInstance.current) {
        performRestore()
      } else {
        console.log('[DEBUG TerminalView] Cannot perform restore - no terminal instance')
      }
    }
  }, [pendingRestore, sessionId, isConnected, isWaitingForInput, onRestoreComplete])

  // Reset restore handled flag when pendingRestore changes
  useEffect(() => {
    if (!pendingRestore) {
      restoreHandledRef.current = false
    }
  }, [pendingRestore])

  return (
    <div className="terminal-view">
      <div className="terminal-toolbar">
        <div className="terminal-info">
          <span className="session-label">Session: {sessionId}</span>
          <span className={`connection-status ${displayConnected ? 'connected' : 'disconnected'}`}>
            {displayConnected ? 'Connected' : 'Disconnected'}
          </span>
        </div>
        <div className="terminal-actions">
          {voiceState.isSupported && (
            <button
              className={`btn-voice ${voiceState.isListening ? 'recording' : ''}`}
              onClick={voiceActions.toggle}
              title={voiceState.isListening ? 'Stop voice input (Ctrl+M)' : 'Start voice input (Ctrl+M)'}
              aria-pressed={voiceState.isListening}
              aria-label={voiceState.isListening ? 'Stop voice input' : 'Start voice input'}
            >
              <svg className="mic-icon" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
                <path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1-9c0-.55.45-1 1-1s1 .45 1 1v6c0 .55-.45 1-1 1s-1-.45-1-1V5zm6 6c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z"/>
              </svg>
              {voiceState.isListening ? 'Listening...' : 'Voice'}
            </button>
          )}
          {/* Accessible live region for voice status announcements */}
          <span
            className="voice-status-live"
            role="status"
            aria-live="polite"
            aria-atomic="true"
          >
            {voiceState.isListening && voiceState.interimTranscript
              ? `Hearing: ${voiceState.interimTranscript}`
              : voiceState.error || ''}
          </span>
          {voiceState.interimTranscript && (
            <span className="voice-interim" aria-hidden="true">{voiceState.interimTranscript}</span>
          )}
          {voiceState.error && (
            <span className="voice-error" aria-hidden="true">{voiceState.error}</span>
          )}
          {onEditProperties && (
            <button
              className="btn-edit-props"
              onClick={() => onEditProperties(sessionId)}
              title="Edit session properties"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="14" height="14">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
              Rename
            </button>
          )}
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
      <div className="terminal-input-container">
        <button
          className="btn-upload"
          onClick={() => fileInputRef.current?.click()}
          title="Upload file"
        >
          <svg viewBox="0 0 24 24" fill="currentColor" width="18" height="18">
            <path d="M16.5 6v11.5c0 2.21-1.79 4-4 4s-4-1.79-4-4V5c0-1.38 1.12-2.5 2.5-2.5s2.5 1.12 2.5 2.5v10.5c0 .55-.45 1-1 1s-1-.45-1-1V6H10v9.5c0 1.38 1.12 2.5 2.5 2.5s2.5-1.12 2.5-2.5V5c0-2.21-1.79-4-4-4S7 2.79 7 5v12.5c0 3.04 2.46 5.5 5.5 5.5s5.5-2.46 5.5-5.5V6h-1.5z"/>
          </svg>
        </button>
        <input
          type="file"
          ref={fileInputRef}
          onChange={handleFileUpload}
          style={{ display: 'none' }}
        />
        <textarea
          className="terminal-input"
          placeholder={isMobile ? 'Type command...' : 'Type command here (Enter to send)...'}
          value={mobileInput}
          onChange={(e) => setMobileInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
              e.preventDefault()
              handleSendMobileInput()
            }
          }}
        />
        {isMobile && (
          <button
            className={`btn-special-keys ${showSpecialKeys ? 'active' : ''}`}
            onClick={() => setShowSpecialKeys(prev => !prev)}
            title="Special keys"
          >
            +
          </button>
        )}
        <button className="btn-send-terminal" onClick={handleSendMobileInput}>
          <span>Send</span>
        </button>
      </div>
      {showSpecialKeys && isMobile && (
        <div className="special-keys-panel">
          <div className="special-keys-row">
            <button onClick={() => sendSpecialKey([0x1b, 0x5b, 0x41])}>Up</button>
            <button onClick={() => sendSpecialKey([0x1b, 0x5b, 0x42])}>Down</button>
            <button onClick={() => sendSpecialKey([0x1b, 0x5b, 0x44])}>Left</button>
            <button onClick={() => sendSpecialKey([0x1b, 0x5b, 0x43])}>Right</button>
          </div>
          <div className="special-keys-row">
            <button onClick={() => sendSpecialKey([0x1b])}>ESC</button>
            <button onClick={() => sendSpecialKey([0x09])}>Tab</button>
            <button onClick={() => sendSpecialKey([0x03])}>Ctrl+C</button>
          </div>
        </div>
      )}
    </div>
  )
}
