import type { CSSProperties } from 'react'
import type { ApprovalPanelData } from '../SchemaRenderer'

const RISK_CONFIG: Record<string, { color: string; label: string; bg: string }> = {
  read:    { color: '#4caf50', label: 'Read',    bg: '#1b2e1b' },
  write:   { color: '#ff9800', label: 'Write',   bg: '#2e2010' },
  execute: { color: '#f44336', label: 'Execute', bg: '#2e1010' },
  admin:   { color: '#9c27b0', label: 'Admin',   bg: '#1e0f2e' },
}

const STATE_CONFIG: Record<string, { color: string; icon: string }> = {
  pending:  { color: '#ff9800', icon: '⏳' },
  approved: { color: '#4caf50', icon: '✅' },
  denied:   { color: '#f44336', icon: '❌' },
}

interface Props {
  data: ApprovalPanelData
  onAction?: (action: string, data?: unknown) => void
}

export function ApprovalPanel({ data, onAction }: Props) {
  const { taskId, action, riskLevel, details, approvalState, approvedBy, approvalTimestamp } = data
  const risk = RISK_CONFIG[riskLevel] ?? RISK_CONFIG.read
  const state = STATE_CONFIG[approvalState] ?? STATE_CONFIG.pending
  const isPending = approvalState === 'pending'

  return (
    <div style={{ ...styles.container, borderColor: risk.color, background: risk.bg }}>
      <div style={styles.header}>
        <div style={styles.titleRow}>
          <span style={{ ...styles.riskBadge, background: risk.color }}>{risk.label}</span>
          <span style={styles.taskId}>#{taskId}</span>
        </div>
        <span style={{ ...styles.stateIcon, color: state.color }}>{state.icon} {approvalState}</span>
      </div>

      <p style={styles.action}>{action}</p>

      {details && (
        <div style={styles.details}>
          {details.affectedResources && details.affectedResources.length > 0 && (
            <div style={styles.detailRow}>
              <span style={styles.detailLabel}>Affected:</span>
              <div style={styles.resourceList}>
                {details.affectedResources.map((r, i) => (
                  <span key={i} style={styles.resource}>{r}</span>
                ))}
              </div>
            </div>
          )}
          {details.reversible !== undefined && (
            <div style={styles.detailRow}>
              <span style={styles.detailLabel}>Reversible:</span>
              <span style={{ color: details.reversible ? '#4caf50' : '#f44336' }}>
                {details.reversible ? 'Yes' : 'No'}
              </span>
            </div>
          )}
          {details.estimatedDuration && (
            <div style={styles.detailRow}>
              <span style={styles.detailLabel}>Duration:</span>
              <span style={styles.detailValue}>{details.estimatedDuration}</span>
            </div>
          )}
        </div>
      )}

      {!isPending && approvedBy && (
        <div style={styles.decision}>
          <span style={{ color: state.color }}>{approvalState} by {approvedBy}</span>
          {approvalTimestamp && <span style={styles.timestamp}> · {new Date(approvalTimestamp).toLocaleString()}</span>}
        </div>
      )}

      {isPending && onAction && (
        <div style={styles.actions}>
          <button style={styles.btnApprove} onClick={() => onAction('approve', { taskId })}>Approve</button>
          <button style={styles.btnDeny} onClick={() => onAction('deny', { taskId })}>Deny</button>
        </div>
      )}
    </div>
  )
}

const styles: Record<string, CSSProperties> = {
  container: { border: '1px solid', borderRadius: 6, padding: 12, fontFamily: 'system-ui, sans-serif', fontSize: 13, color: '#cdd6f4' },
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 },
  titleRow: { display: 'flex', alignItems: 'center', gap: 8 },
  riskBadge: { borderRadius: 4, padding: '2px 8px', color: '#fff', fontWeight: 700, fontSize: 11 },
  taskId: { color: '#6c7086', fontSize: 11 },
  stateIcon: { fontWeight: 600, textTransform: 'capitalize', fontSize: 12 } as CSSProperties,
  action: { margin: '0 0 10px', color: '#cdd6f4', lineHeight: 1.5 },
  details: { display: 'flex', flexDirection: 'column', gap: 5, marginBottom: 10 },
  detailRow: { display: 'flex', alignItems: 'flex-start', gap: 8 },
  detailLabel: { color: '#6c7086', minWidth: 70, fontSize: 12 },
  detailValue: { color: '#a6adc8' },
  resourceList: { display: 'flex', flexWrap: 'wrap', gap: 4 },
  resource: { background: '#313244', borderRadius: 3, padding: '1px 6px', fontSize: 11, fontFamily: 'monospace', color: '#89b4fa' },
  decision: { fontSize: 12, color: '#a6adc8', marginBottom: 8 },
  timestamp: { color: '#6c7086' },
  actions: { display: 'flex', gap: 8, marginTop: 4 },
  btnApprove: { padding: '5px 14px', background: '#4caf50', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer', fontSize: 12 },
  btnDeny: { padding: '5px 14px', background: '#f44336', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer', fontSize: 12 },
}
