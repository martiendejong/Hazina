import { useState, useRef, useEffect, ReactNode } from 'react'
import './SplitPane.css'

interface SplitPaneProps {
  left: ReactNode
  right: ReactNode
  defaultSize?: number  // Initial left pane width in pixels
  minSize?: number      // Minimum left pane width
  maxSize?: number      // Maximum left pane width
  storageKey?: string   // LocalStorage key for persistence
}

export function SplitPane({
  left,
  right,
  defaultSize = 300,
  minSize = 200,
  maxSize = 600,
  storageKey = 'splitPaneSize'
}: SplitPaneProps) {
  // Load saved size from localStorage or use default
  const [leftSize, setLeftSize] = useState(() => {
    if (storageKey) {
      const saved = localStorage.getItem(storageKey)
      if (saved) {
        const parsed = parseInt(saved, 10)
        return Math.min(Math.max(parsed, minSize), maxSize)
      }
    }
    return defaultSize
  })

  const [isDragging, setIsDragging] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  // Handle mouse down on resize handle
  const handleMouseDown = (e: React.MouseEvent) => {
    e.preventDefault()
    setIsDragging(true)
  }

  // Handle mouse move while dragging
  useEffect(() => {
    if (!isDragging) return

    const handleMouseMove = (e: MouseEvent) => {
      if (!containerRef.current) return

      const containerRect = containerRef.current.getBoundingClientRect()
      const newSize = e.clientX - containerRect.left

      // Clamp between min and max
      const clampedSize = Math.min(Math.max(newSize, minSize), maxSize)
      setLeftSize(clampedSize)

      // Save to localStorage
      if (storageKey) {
        localStorage.setItem(storageKey, clampedSize.toString())
      }
    }

    const handleMouseUp = () => {
      setIsDragging(false)
    }

    document.addEventListener('mousemove', handleMouseMove)
    document.addEventListener('mouseup', handleMouseUp)

    // Add cursor style to body while dragging
    document.body.style.cursor = 'ew-resize'
    document.body.style.userSelect = 'none'

    return () => {
      document.removeEventListener('mousemove', handleMouseMove)
      document.removeEventListener('mouseup', handleMouseUp)
      document.body.style.cursor = ''
      document.body.style.userSelect = ''
    }
  }, [isDragging, minSize, maxSize, storageKey])

  return (
    <div className="split-pane" ref={containerRef}>
      <div className="split-pane-left" style={{ width: `${leftSize}px` }}>
        {left}
      </div>

      <div
        className={`split-pane-handle ${isDragging ? 'dragging' : ''}`}
        onMouseDown={handleMouseDown}
      >
        <div className="split-pane-handle-bar" />
      </div>

      <div className="split-pane-right">
        {right}
      </div>
    </div>
  )
}
