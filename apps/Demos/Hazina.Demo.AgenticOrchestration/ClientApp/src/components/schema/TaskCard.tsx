import type { CSSProperties } from 'react'
import type { TaskCardData } from '../SchemaRenderer'

const RISK_COLORS: Record<string, string> = {
  read: '#4caf50',
  write: '#ff9800',
  execute: '#f44336',
  admin: '#9c27b0',
}

const ACTION_ICONS: Record<string, string> = {
  read: '📖',
  write: '✏️',
  execute: '⚡',
  analyze: '🔍',
  delete: '🗑️',
}

interface Props {
  data: TaskCardData
  onAction?: (action: string, data?: unknown) => void
}

export function TaskCard({ data, onAction }: Props) {
  const { title, steps, estimatedDuration, requiresApproval } = data

  return (
    <div style={styles.card}>
      <div style={styles.header}>
        <span style={styles.title}>{title}</span>
        <div style={styles.meta}>
          {estimatedDuration && (
            <span style={styles.badge}>⏱ {estimatedDuration}</span>
          )}
          {requiresApproval && (
            <span style={{ ...styles.badge, background: '#f44336' }}>⚠ Approval required</span>
          )}
        </div>
      </div>

      <div style={styles.steps}>
        {steps.map((step, i) => (
          <div key={i} style={styles.step}>
            <span style={styles.stepIcon}>{ACTION_ICONS[step.action] ?? '•'}</span>
            <div style={styles.stepContent}>
              <span style={styles.stepDescription}>{step.description}</span>
              <div style={styles.stepTags}>
                <span style={{ ...styles.tag, borderColor: RISK_COLORS[step.riskLevel] ?? '#888', color: RISK_COLORS[step.riskLevel] ?? '#888' }}>
                  {step.action}
                </span>
                <span style={{ ...styles.tag, borderColor: RISK_COLORS[step.riskLevel] ?? '#888', color: RISK_COLORS[step.riskLevel] ?? '#888' }}>
                  {step.riskLevel}
                </span>
              </div>
            </div>
          </div>
        ))}
      </div>

      {requiresApproval && onAction && (
        <div style={styles.actions}>
          <button style={styles.btnApprove} onClick={() => onAction('approve', data)}>Approve</button>
          <button style={styles.btnDeny} onClick={() => onAction('deny', data)}>Deny</button>
        </div>
      )}
    </div>
  )
}

const styles: Record<string, CSSProperties> = {
  card: { background: '#1e1e2e', border: '1px solid #333', borderRadius: 6, padding: 12, fontFamily: 'system-ui, sans-serif', fontSize: 13, color: '#cdd6f4' },
  header: { display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 10, gap: 8, flexWrap: 'wrap' },
  title: { fontWeight: 600, fontSize: 14, color: '#cba6f7' },
  meta: { display: 'flex', gap: 6, flexWrap: 'wrap' },
  badge: { background: '#313244', borderRadius: 4, padding: '2px 8px', fontSize: 11, color: '#cdd6f4' },
  steps: { display: 'flex', flexDirection: 'column', gap: 6 },
  step: { display: 'flex', alignItems: 'flex-start', gap: 8 },
  stepIcon: { fontSize: 14, minWidth: 20, textAlign: 'center' },
  stepContent: { flex: 1 },
  stepDescription: { color: '#cdd6f4', display: 'block', marginBottom: 3 },
  stepTags: { display: 'flex', gap: 4, flexWrap: 'wrap' },
  tag: { border: '1px solid', borderRadius: 3, padding: '1px 5px', fontSize: 10 },
  actions: { display: 'flex', gap: 8, marginTop: 12 },
  btnApprove: { padding: '5px 14px', background: '#4caf50', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer', fontSize: 12 },
  btnDeny: { padding: '5px 14px', background: '#f44336', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer', fontSize: 12 },
}
