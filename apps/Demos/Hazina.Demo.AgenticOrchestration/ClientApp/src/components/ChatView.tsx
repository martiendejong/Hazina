import { useState, useRef, useEffect, useCallback } from 'react'
import * as signalR from '@microsoft/signalr'
import { authFetch } from '../auth'
import { useVoiceControl } from '../hooks/useVoiceControl'
import type { ChatMessage } from '../types'

interface ChatViewProps {
  onClose: () => void
}

export function ChatView({ onClose }: ChatViewProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [sessionId] = useState(() => `chat-${Date.now()}`)
  const [error, setError] = useState<string | null>(null)
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLTextAreaElement>(null)

  // Voice control
  const handleVoiceTranscript = useCallback((text: string) => {
    const sanitized = text.trim().slice(0, 1000)
    if (sanitized) {
      setInput(prev => prev + (prev ? ' ' : '') + sanitized)
    }
  }, [])

  const [voiceState, voiceActions] = useVoiceControl(handleVoiceTranscript)

  // Auto-scroll to bottom
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages])

  // Focus input on mount
  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  // SignalR connection setup
  useEffect(() => {
    const baseUrl = window.location.origin
    const hubUrl = `${baseUrl}/hubs/agentic`

    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build()

    // Handle chat chunk events
    newConnection.on('ChatChunk', (data: { sessionId: string, chunk: string, timestamp: string }) => {
      if (data.sessionId === sessionId) {
        setMessages(prev => {
          const lastMessage = prev[prev.length - 1]
          if (lastMessage && lastMessage.role === 'assistant' && lastMessage.isStreaming) {
            return [
              ...prev.slice(0, -1),
              { ...lastMessage, content: lastMessage.content + data.chunk }
            ]
          }
          return prev
        })
      }
    })

    // Handle chat complete events
    newConnection.on('ChatComplete', (data: { sessionId: string, message: string, tokensUsed: number, timestamp: string }) => {
      if (data.sessionId === sessionId) {
        setMessages(prev => {
          const lastMessage = prev[prev.length - 1]
          if (lastMessage && lastMessage.role === 'assistant' && lastMessage.isStreaming) {
            return [
              ...prev.slice(0, -1),
              { ...lastMessage, content: data.message, isStreaming: false }
            ]
          }
          return prev
        })
        setIsLoading(false)
      }
    })

    // Start connection
    newConnection.start()
      .then(() => {
        console.log('SignalR connected to chat hub')
        // Join the chat session group
        return newConnection.invoke('JoinChatSession', sessionId)
      })
      .then(() => {
        console.log(`Joined chat session group: chat-${sessionId}`)
      })
      .catch(err => {
        console.error('SignalR connection error:', err)
        setError('Failed to connect to chat service')
      })

    setConnection(newConnection)

    // Cleanup
    return () => {
      newConnection.invoke('LeaveChatSession', sessionId).catch(console.error)
      newConnection.stop()
    }
  }, [sessionId])

  const sendMessage = async () => {
    const trimmedInput = input.trim()
    if (!trimmedInput || isLoading || !connection) return

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: trimmedInput,
      timestamp: new Date().toISOString()
    }

    setMessages(prev => [...prev, userMessage])
    setInput('')
    setIsLoading(true)
    setError(null)

    // Add placeholder for assistant message
    const assistantMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'assistant',
      content: '',
      timestamp: new Date().toISOString(),
      isStreaming: true
    }
    setMessages(prev => [...prev, assistantMessage])

    try {
      const response = await authFetch(`/api/chat/${sessionId}/message`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: trimmedInput
        })
      })

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}))
        throw new Error(errorData.error || `Chat request failed: ${response.statusText}`)
      }

      const result = await response.json()
      if (!result.success) {
        throw new Error(result.errorMessage || 'Chat request failed')
      }

      // SignalR will handle the streaming response via ChatChunk/ChatComplete events
      // If response arrives before SignalR events, update message directly
      if (result.message) {
        setMessages(prev => prev.map(m =>
          m.id === assistantMessage.id
            ? { ...m, content: result.message, isStreaming: false }
            : m
        ))
        setIsLoading(false)
      }

    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to send message')
      // Remove the placeholder assistant message
      setMessages(prev => prev.filter(m => m.id !== assistantMessage.id))
      setIsLoading(false)
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      sendMessage()
    }
  }

  const clearChat = async () => {
    try {
      await authFetch(`/api/chat/${sessionId}`, {
        method: 'DELETE'
      })
      setMessages([])
      setError(null)
    } catch (err) {
      console.error('Failed to clear chat:', err)
    }
  }

  return (
    <div className="chat-view">
      <div className="chat-header">
        <div className="chat-title">
          <h2>Hazina Agent</h2>
          <span className="chat-subtitle">Terminal orchestration assistant</span>
        </div>
        <div className="chat-actions">
          <button className="btn-clear" onClick={clearChat} title="Clear chat">
            Clear
          </button>
          <button className="btn-close" onClick={onClose} title="Close chat">
            Close
          </button>
        </div>
      </div>

      {error && (
        <div className="chat-error">
          {error}
          <button onClick={() => setError(null)}>Dismiss</button>
        </div>
      )}

      <div className="chat-messages">
        {messages.length === 0 && (
          <div className="chat-welcome">
            <div className="welcome-icon">🤖</div>
            <h3>Welcome to Hazina Agent</h3>
            <p>I can help you manage terminal sessions. Try asking:</p>
            <ul className="welcome-suggestions">
              <li onClick={() => setInput("How many sessions are running?")}>
                "How many sessions are running?"
              </li>
              <li onClick={() => setInput("Show me all sessions")}>
                "Show me all sessions"
              </li>
              <li onClick={() => setInput("What's the system status?")}>
                "What's the system status?"
              </li>
              <li onClick={() => setInput("Find sessions related to claude")}>
                "Find sessions related to claude"
              </li>
            </ul>
          </div>
        )}

        {messages.map(message => (
          <div key={message.id} className={`chat-message ${message.role}`}>
            <div className="message-avatar">
              {message.role === 'user' ? '👤' : '🤖'}
            </div>
            <div className="message-content">
              <div className="message-text">
                {message.content || (message.isStreaming ? 'Thinking...' : '')}
                {message.isStreaming && message.content && <span className="cursor-blink">▊</span>}
              </div>
              <div className="message-time">
                {new Date(message.timestamp).toLocaleTimeString()}
              </div>
            </div>
          </div>
        ))}

        <div ref={messagesEndRef} />
      </div>

      <div className="chat-input-container">
        {voiceState.isSupported && (
          <button
            className={`btn-voice-chat ${voiceState.isListening ? 'recording' : ''}`}
            onClick={voiceActions.toggle}
            title={voiceState.isListening ? 'Stop listening' : 'Start voice input'}
          >
            <svg viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm-1-9c0-.55.45-1 1-1s1 .45 1 1v6c0 .55-.45 1-1 1s-1-.45-1-1V5zm6 6c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z"/>
            </svg>
          </button>
        )}
        {voiceState.isListening && voiceState.interimTranscript && (
          <div className="voice-preview">{voiceState.interimTranscript}</div>
        )}
        <textarea
          ref={inputRef}
          className="chat-input"
          placeholder="Type a message or use voice..."
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          disabled={isLoading}
          rows={1}
        />
        <button
          className="btn-send"
          onClick={sendMessage}
          disabled={!input.trim() || isLoading || !connection}
        >
          {isLoading ? (
            <span className="loading-spinner">⏳</span>
          ) : (
            <svg viewBox="0 0 24 24" fill="currentColor">
              <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
            </svg>
          )}
        </button>
      </div>
    </div>
  )
}
