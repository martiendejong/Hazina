import { useState, useRef, useEffect, useCallback } from 'react'
import { authFetch } from '../auth'
import { useVoiceControl } from '../hooks/useVoiceControl'
import type { ChatMessage, ToolCall } from '../types'

interface ChatViewProps {
  onClose: () => void
}

export function ChatView({ onClose }: ChatViewProps) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [input, setInput] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [conversationId, setConversationId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
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

  const sendMessage = async () => {
    const trimmedInput = input.trim()
    if (!trimmedInput || isLoading) return

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
    const assistantMessageId = crypto.randomUUID()
    setMessages(prev => [...prev, {
      id: assistantMessageId,
      role: 'assistant',
      content: '',
      timestamp: new Date().toISOString(),
      isStreaming: true
    }])

    try {
      const response = await authFetch('/api/chat/messages', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: trimmedInput,
          conversationId
        })
      })

      if (!response.ok) {
        throw new Error(`Chat request failed: ${response.statusText}`)
      }

      // Handle streaming response
      const reader = response.body?.getReader()
      if (!reader) {
        throw new Error('No response body')
      }

      const decoder = new TextDecoder()
      let fullContent = ''
      let toolCalls: ToolCall[] = []
      let newConversationId = conversationId

      while (true) {
        const { done, value } = await reader.read()
        if (done) break

        const chunk = decoder.decode(value, { stream: true })
        const lines = chunk.split('\n')

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6)
            if (data === '[DONE]') continue

            try {
              const parsed = JSON.parse(data)

              if (parsed.conversationId) {
                newConversationId = parsed.conversationId
              }

              if (parsed.type === 'content') {
                fullContent += parsed.content
                setMessages(prev => prev.map(m =>
                  m.id === assistantMessageId
                    ? { ...m, content: fullContent, isStreaming: true }
                    : m
                ))
              } else if (parsed.type === 'tool_call') {
                toolCalls = [...toolCalls, parsed.toolCall]
                setMessages(prev => prev.map(m =>
                  m.id === assistantMessageId
                    ? { ...m, toolCalls, isStreaming: true }
                    : m
                ))
              } else if (parsed.type === 'tool_result') {
                const updatedToolCalls = toolCalls.map(tc =>
                  tc.id === parsed.toolCallId
                    ? { ...tc, result: parsed.result }
                    : tc
                )
                toolCalls = updatedToolCalls
                setMessages(prev => prev.map(m =>
                  m.id === assistantMessageId
                    ? { ...m, toolCalls: updatedToolCalls, isStreaming: true }
                    : m
                ))
              }
            } catch {
              // Skip malformed JSON
            }
          }
        }
      }

      // Mark message as complete
      setMessages(prev => prev.map(m =>
        m.id === assistantMessageId
          ? { ...m, isStreaming: false }
          : m
      ))

      if (newConversationId) {
        setConversationId(newConversationId)
      }

    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to send message')
      // Remove the placeholder assistant message
      setMessages(prev => prev.filter(m => m.id !== assistantMessageId))
    } finally {
      setIsLoading(false)
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      sendMessage()
    }
  }

  const clearChat = () => {
    setMessages([])
    setConversationId(null)
    setError(null)
  }

  const formatToolCall = (toolCall: ToolCall) => {
    const args = JSON.stringify(toolCall.arguments, null, 2)
    return (
      <div className="tool-call" key={toolCall.id}>
        <div className="tool-call-header">
          <span className="tool-icon">🔧</span>
          <span className="tool-name">{toolCall.name}</span>
        </div>
        <pre className="tool-args">{args}</pre>
        {toolCall.result && (
          <div className="tool-result">
            <span className="tool-result-label">Result:</span>
            <pre>{toolCall.result}</pre>
          </div>
        )}
      </div>
    )
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
              <li onClick={() => setInput("What sessions are currently running?")}>
                "What sessions are currently running?"
              </li>
              <li onClick={() => setInput("Start a new Claude agent to work on ClickUp tasks")}>
                "Start a new Claude agent to work on ClickUp tasks"
              </li>
              <li onClick={() => setInput("Send Ctrl+C to the first session")}>
                "Send Ctrl+C to the first session"
              </li>
              <li onClick={() => setInput("Terminate all stopped sessions")}>
                "Terminate all stopped sessions"
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
              {message.toolCalls && message.toolCalls.length > 0 && (
                <div className="tool-calls">
                  {message.toolCalls.map(formatToolCall)}
                </div>
              )}
              <div className="message-text">
                {message.content}
                {message.isStreaming && <span className="cursor-blink">▊</span>}
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
          disabled={!input.trim() || isLoading}
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
