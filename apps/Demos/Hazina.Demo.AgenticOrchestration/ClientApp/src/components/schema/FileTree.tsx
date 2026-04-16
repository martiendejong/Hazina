import { useState } from 'react'
import type { CSSProperties } from 'react'
import type { FileTreeData, FileTreeItem } from '../SchemaRenderer'

interface Props {
  data: FileTreeData
  onAction?: (action: string, data?: unknown) => void
}

export function FileTree({ data, onAction }: Props) {
  const [expanded, setExpanded] = useState<Set<string>>(new Set(data.expandedPaths ?? []))
  const [selected, setSelected] = useState<string | undefined>(data.selectedPath)

  const toggle = (path: string) => {
    setExpanded(prev => {
      const next = new Set(prev)
      next.has(path) ? next.delete(path) : next.add(path)
      return next
    })
  }

  const select = (path: string) => {
    setSelected(path)
    onAction?.('select', { path })
  }

  return (
    <div style={styles.container}>
      <div style={styles.header}>
        <span style={styles.icon}>📁</span>
        <span style={styles.rootPath}>{data.rootPath}</span>
      </div>
      <div style={styles.tree}>
        {data.items.map(item => (
          <TreeNode key={item.path} item={item} depth={0} expanded={expanded} selected={selected} onToggle={toggle} onSelect={select} />
        ))}
      </div>
    </div>
  )
}

function TreeNode({ item, depth, expanded, selected, onToggle, onSelect }: {
  item: FileTreeItem
  depth: number
  expanded: Set<string>
  selected?: string
  onToggle: (path: string) => void
  onSelect: (path: string) => void
}) {
  const isDir = item.type === 'directory'
  const isExpanded = expanded.has(item.path)
  const isSelected = selected === item.path
  const indent = depth * 16

  return (
    <div>
      <div
        style={{ ...styles.node, paddingLeft: indent + 4, background: isSelected ? '#313244' : 'transparent' }}
        onClick={() => isDir ? onToggle(item.path) : onSelect(item.path)}
      >
        <span style={styles.nodeIcon}>
          {isDir ? (isExpanded ? '📂' : '📁') : getFileIcon(item.extension)}
        </span>
        <span style={styles.nodeName}>{item.name}</span>
        {!isDir && item.size !== undefined && (
          <span style={styles.nodeSize}>{formatSize(item.size)}</span>
        )}
      </div>
      {isDir && isExpanded && item.children?.map(child => (
        <TreeNode key={child.path} item={child} depth={depth + 1} expanded={expanded} selected={selected} onToggle={onToggle} onSelect={onSelect} />
      ))}
    </div>
  )
}

function getFileIcon(ext?: string): string {
  const map: Record<string, string> = {
    ts: '📘', tsx: '📘', js: '📙', jsx: '📙', json: '📋',
    cs: '💎', csproj: '🔧', yaml: '📄', yml: '📄',
    md: '📝', txt: '📝', css: '🎨', html: '🌐',
  }
  return map[ext ?? ''] ?? '📄'
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes}B`
  if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)}KB`
  return `${(bytes / 1048576).toFixed(1)}MB`
}

const styles: Record<string, CSSProperties> = {
  container: { background: '#1e1e2e', border: '1px solid #333', borderRadius: 6, fontFamily: 'monospace', fontSize: 12, color: '#cdd6f4', overflow: 'hidden' },
  header: { display: 'flex', alignItems: 'center', gap: 6, padding: '8px 10px', background: '#181825', borderBottom: '1px solid #333' },
  icon: { fontSize: 14 },
  rootPath: { color: '#89b4fa', fontWeight: 600 },
  tree: { padding: '4px 0', maxHeight: 320, overflowY: 'auto' },
  node: { display: 'flex', alignItems: 'center', gap: 6, padding: '3px 8px', cursor: 'pointer', userSelect: 'none' },
  nodeIcon: { fontSize: 13 },
  nodeName: { flex: 1, color: '#cdd6f4' },
  nodeSize: { color: '#6c7086', fontSize: 10 },
}
