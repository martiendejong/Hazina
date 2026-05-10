import type { CSSProperties } from 'react'
import type { CommandPreviewData } from '../SchemaRenderer'

const RISK_CONFIG: Record<string, { color: string; label: string; bg: string }> = {
  read:    { color: '#4caf50', label: 'Read',    bg: '#1b2e1b' },
  write:   { color: '#ff9800', label: 'Write',   bg: '#2e2010' },
  execute: { color: '#f44336', label: 'Execute', bg: '#2e1010' },
  admin:   { color: '#9c27b0', label: 'Admin',   bg: '#1e0f2e' },
}

const TYPE_LABELS: Record<string, string> = {
  shell: '$ bash', powershell: '> pwsh', python: '🐍 python', sql: '🗄 sql', 'api-call': '🌐 http',
}

interface Props {
  data: CommandPreviewData
  onAction?: (action: string, data?: unknown) => void
}

export function CommandPreview({ data, onAction }: Props) {
  const { command, commandType, workingDirectory, riskLevel, safetyAnalysis, expectedOutput, timeout } = data
  const risk = RISK_CONFIG[riskLevel] ?? RISK_CONFIG.execute

  const hasWarnings = (safetyAnalysis?.destructiveOperations?.length ?? 0) > 0 || safetyAnalysis?.requiresSudo

  return (
    <div style={{ ...styles.container, borderColor: risk.color, background: risk.bg }}>
      <div style={styles.header}>
        <div style={styles.titleRow}>
          {commandType && <span style={styles.typeLabel}>{TYPE_LABELS[commandType] ?? commandType}</span>}
          <span style={{ ...styles.riskBadge, background: risk.color }}>{risk.label}</span>
          {safetyAnalysis?.reversible === false && (
            <span style={{ ...styles.riskBadge, background: '#f44336' }}>Irreversible</span>
          )}
          {safetyAnalysis?.requiresSudo && (
            <span style={{ ...styles.riskBadge, background: '#9c27b0' }}>sudo</span>
          )}
        </div>
        {timeout !== undefined && <span style={styles.timeout}>⏱ {timeout}s</span>}
      </div>

      {workingDirectory && (
        <div style={styles.cwd}>
          <span style={styles.cwdLabel}>cwd:</span>
          <span style={styles.cwdPath}>{workingDirectory}</span>
        </div>
      )}

      <pre style={styles.command}>{command}</pre>

      {hasWarnings && (
        <div style={styles.warnings}>
          {safetyAnalysis?.destructiveOperations?.map((op, i) => (
            <div key={i} style={styles.warning}>⚠ {op}</div>
          ))}
          {safetyAnalysis?.requiresSudo && (
            <div style={styles.warning}>⚠ Requires elevated privileges</div>
          )}
        </div>
      )}

      {safetyAnalysis?.affectedResources && safetyAnalysis.affectedResources.length > 0 && (
        <div style={styles.resources}>
          <span style={styles.resourcesLabel}>Affects:</span>
          {safetyAnalysis.affectedResources.map((r, i) => (
            <span key={i} style={styles.resource}>{r}</span>
          ))}
        </div>
      )}

      {expectedOutput && (
        <div style={styles.expectedOutput}>
          <div style={styles.expectedLabel}>Expected output:</div>
          <pre style={styles.expectedPre}>{expectedOutput}</pre>
        </div>
      )}

      {onAction && (
        <div style={styles.actions}>
          <button style={styles.btnApprove} onClick={() => onAction('approve', data)}>Execute</button>
          <button style={styles.btnReject} onClick={() => onAction('reject', data)}>Cancel</button>
        </div>
      )}
    </div>
  )
}

const styles: Record<string, CSSProperties> = {
  container: { border: '1px solid', borderRadius: 6, padding: 12, fontFamily: 'system-ui, sans-serif', fontSize: 13, color: '#cdd6f4' },
  header: { display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 },
  titleRow: { display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' },
  typeLabel: { color: '#89b4fa', fontFamily: 'monospace', fontSize: 12 },
  riskBadge: { borderRadius: 4, padding: '2px 7px', color: '#fff', fontWeight: 700, fontSize: 10 },
  timeout: { color: '#6c7086', fontSize: 11 },
  cwd: { display: 'flex', gap: 6, alignItems: 'center', marginBottom: 6 },
  cwdLabel: { color: '#6c7086', fontSize: 11 },
  cwdPath: { fontFamily: 'monospace', color: '#a6e3a1', fontSize: 12 },
  command: { background: '#11111b', borderRadius: 4, padding: '8px 10px', margin: '0 0 8px', fontFamily: 'monospace', fontSize: 12, color: '#cdd6f4', overflowX: 'auto', whiteSpace: 'pre-wrap', wordBreak: 'break-word' },
  warnings: { display: 'flex', flexDirection: 'column', gap: 3, marginBottom: 8 },
  warning: { color: '#f38ba8', fontSize: 12 },
  resources: { display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap', marginBottom: 8 },
  resourcesLabel: { color: '#6c7086', fontSize: 11 },
  resource: { background: '#313244', borderRadius: 3, padding: '1px 6px', fontSize: 11, fontFamily: 'monospace', color: '#89b4fa' },
  expectedOutput: { marginBottom: 8 },
  expectedLabel: { color: '#6c7086', fontSize: 11, marginBottom: 3 },
  expectedPre: { background: '#11111b', borderRadius: 4, padding: '6px 10px', margin: 0, fontFamily: 'monospace', fontSize: 11, color: '#a6adc8', overflowX: 'auto' },
  actions: { display: 'flex', gap: 8, marginTop: 4 },
  btnApprove: { padding: '5px 14px', background: '#4caf50', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer', fontSize: 12 },
  btnReject: { padding: '5px 14px', background: '#f44336', color: '#fff', border: 'none', borderRadius: 4, cursor: 'pointer', fontSize: 12 },
}
