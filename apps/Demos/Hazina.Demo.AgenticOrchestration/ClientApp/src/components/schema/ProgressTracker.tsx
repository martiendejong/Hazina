import type { CSSProperties } from 'react'
import type { ProgressTrackerData, ProgressStep } from '../SchemaRenderer'

const STATUS_CONFIG: Record<string, { color: string; icon: string }> = {
  'pending':     { color: '#6c7086', icon: '○' },
  'in-progress': { color: '#89b4fa', icon: '◉' },
  'completed':   { color: '#4caf50', icon: '●' },
  'failed':      { color: '#f44336', icon: '✕' },
  'skipped':     { color: '#a6adc8', icon: '–' },
}

const OVERALL_COLORS: Record<string, string> = {
  'not-started': '#6c7086',
  'in-progress': '#89b4fa',
  'completed':   '#4caf50',
  'failed':      '#f44336',
  'cancelled':   '#a6adc8',
}

interface Props {
  data: ProgressTrackerData
}

export function ProgressTracker({ data }: Props) {
  const { taskId, title, totalSteps, currentStep, steps, overallStatus, estimatedCompletion } = data
  const pct = totalSteps > 0 ? Math.round((currentStep / totalSteps) * 100) : 0
  const statusColor = OVERALL_COLORS[overallStatus ?? 'not-started'] ?? '#6c7086'

  return (
    <div style={styles.container}>
      <div style={styles.header}>
        <div>
          <div style={styles.title}>{title ?? `Task ${taskId}`}</div>
          <div style={{ color: statusColor, fontSize: 11, marginTop: 2, textTransform: 'capitalize' }}>
            {overallStatus ?? 'not-started'}
          </div>
        </div>
        <div style={{ ...styles.pctBadge, color: statusColor }}>{pct}%</div>
      </div>

      <div style={styles.progressBar}>
        <div style={{ ...styles.progressFill, width: `${pct}%`, background: statusColor }} />
      </div>

      <div style={styles.counter}>{currentStep} / {totalSteps} steps</div>

      {estimatedCompletion && (
        <div style={styles.eta}>ETA: {new Date(estimatedCompletion).toLocaleTimeString()}</div>
      )}

      {steps && steps.length > 0 && (
        <div style={styles.steps}>
          {steps.map(step => <StepRow key={step.index} step={step} />)}
        </div>
      )}
    </div>
  )
}

function StepRow({ step }: { step: ProgressStep }) {
  const cfg = STATUS_CONFIG[step.status] ?? STATUS_CONFIG.pending

  return (
    <div style={styles.stepRow}>
      <span style={{ color: cfg.color, minWidth: 14, textAlign: 'center', fontWeight: 700 }}>{cfg.icon}</span>
      <div style={{ flex: 1 }}>
        <span style={{ color: step.status === 'completed' ? '#a6adc8' : '#cdd6f4' }}>{step.description}</span>
        {step.error && <div style={styles.error}>{step.error}</div>}
      </div>
      {step.status === 'in-progress' && (
        <span style={styles.spinner}>⟳</span>
      )}
    </div>
  )
}

const styles: Record<string, CSSProperties> = {
  container: { background: '#1e1e2e', border: '1px solid #333', borderRadius: 6, padding: 12, fontFamily: 'system-ui, sans-serif', fontSize: 13, color: '#cdd6f4' },
  header: { display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: 8 },
  title: { fontWeight: 600, color: '#cba6f7', fontSize: 14 },
  pctBadge: { fontWeight: 700, fontSize: 18 },
  progressBar: { height: 6, background: '#313244', borderRadius: 3, overflow: 'hidden', marginBottom: 4 },
  progressFill: { height: '100%', borderRadius: 3, transition: 'width 0.3s ease' },
  counter: { color: '#6c7086', fontSize: 11, marginBottom: 4 },
  eta: { color: '#a6adc8', fontSize: 11, marginBottom: 8 },
  steps: { display: 'flex', flexDirection: 'column', gap: 5, marginTop: 8 },
  stepRow: { display: 'flex', alignItems: 'flex-start', gap: 8 },
  error: { color: '#f44336', fontSize: 11, marginTop: 2 },
  spinner: { color: '#89b4fa', animation: 'spin 1s linear infinite', fontSize: 14 },
}
