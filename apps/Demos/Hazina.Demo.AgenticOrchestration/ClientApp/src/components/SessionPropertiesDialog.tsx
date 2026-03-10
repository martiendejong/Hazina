import { useState, useEffect, useRef } from 'react'
import type { SessionProperties } from '../types'
import './SessionPropertiesDialog.css'

const PRESET_COLORS = [
  { name: 'Default', value: '' },
  { name: 'Blue', value: '#1f6feb' },
  { name: 'Green', value: '#238636' },
  { name: 'Purple', value: '#8957e5' },
  { name: 'Orange', value: '#d29922' },
  { name: 'Red', value: '#da3633' },
  { name: 'Pink', value: '#db61a2' },
  { name: 'Teal', value: '#3fb950' },
  { name: 'Cyan', value: '#39d2c0' },
  { name: 'Yellow', value: '#e3b341' },
]

interface SessionPropertiesDialogProps {
  isOpen: boolean
  sessionId: string
  currentTitle: string
  properties: SessionProperties
  onSave: (sessionId: string, properties: SessionProperties) => void
  onClose: () => void
}

export function SessionPropertiesDialog({
  isOpen,
  sessionId,
  currentTitle,
  properties,
  onSave,
  onClose
}: SessionPropertiesDialogProps) {
  const [name, setName] = useState(properties.customName ?? '')
  const [color, setColor] = useState(properties.cardColor ?? '')
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (isOpen) {
      setName(properties.customName ?? '')
      setColor(properties.cardColor ?? '')
      setTimeout(() => inputRef.current?.focus(), 100)
    }
  }, [isOpen, properties])

  useEffect(() => {
    if (!isOpen) return
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault()
        onClose()
      } else if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault()
        handleSave()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [isOpen, name, color])

  const handleSave = () => {
    onSave(sessionId, {
      customName: name.trim() || undefined,
      cardColor: color || undefined
    })
    onClose()
  }

  if (!isOpen) return null

  return (
    <div className="session-props-overlay" onClick={onClose}>
      <div className="session-props-dialog" onClick={e => e.stopPropagation()}>
        <div className="session-props-header">
          <h3>Session Properties</h3>
          <button className="session-props-close" onClick={onClose} title="Close">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div className="session-props-body">
          <div className="session-props-field">
            <label htmlFor="session-name">Display Name</label>
            <input
              ref={inputRef}
              id="session-name"
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder={currentTitle || 'Enter a custom name...'}
              maxLength={100}
            />
            <span className="session-props-hint">
              Leave empty to use the auto-detected title
            </span>
          </div>

          <div className="session-props-field">
            <label>Card Color</label>
            <div className="color-picker-grid">
              {PRESET_COLORS.map(preset => (
                <button
                  key={preset.value}
                  className={`color-swatch ${color === preset.value ? 'selected' : ''}`}
                  style={{
                    background: preset.value || '#21262d',
                    borderColor: preset.value || '#30363d'
                  }}
                  onClick={() => setColor(preset.value)}
                  title={preset.name}
                  aria-label={`Color: ${preset.name}`}
                >
                  {color === preset.value && (
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3">
                      <path d="M5 13l4 4L19 7" />
                    </svg>
                  )}
                </button>
              ))}
            </div>
          </div>

          {/* Preview */}
          <div className="session-props-field">
            <label>Preview</label>
            <div
              className="session-props-preview"
              style={{
                borderLeftColor: color || '#30363d',
                borderLeftWidth: color ? '3px' : '1px'
              }}
            >
              <span className="preview-name">{name.trim() || currentTitle || 'Session'}</span>
              <span className="preview-id">{sessionId.slice(0, 12)}...</span>
            </div>
          </div>
        </div>

        <div className="session-props-footer">
          <button className="btn-cancel" onClick={onClose}>Cancel</button>
          <button className="btn-save" onClick={handleSave}>Save</button>
        </div>
      </div>
    </div>
  )
}
