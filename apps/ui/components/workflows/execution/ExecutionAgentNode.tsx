import React from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { Bot, CheckCircle2, XCircle, Loader2, Clock } from 'lucide-react';
import { WorkflowStep } from '../../../types';
import { INDIGO, EMERALD, RED } from '../../../utils/workflowGraphLayout';

export type NodeExecutionState = 'pending' | 'running' | 'completed' | 'failed' | 'cancelled' | 'skipped';

export interface ExecAgentNodeData {
  step: WorkflowStep;
  state?: NodeExecutionState;
  jobId?: string;
  durationMs?: number;
  [key: string]: unknown;
}

const STATE_STYLES: Record<NodeExecutionState, React.CSSProperties> = {
  pending:   { border: `1.5px solid rgba(156,163,175,0.4)`,  boxShadow: '0 2px 8px rgba(0,0,0,0.08)', opacity: 0.75 },
  running:   { border: `1.5px solid ${INDIGO}`,              boxShadow: `0 0 0 3px rgba(99,102,241,0.2), 0 4px 16px rgba(0,0,0,0.12)` },
  completed: { border: `1.5px solid ${EMERALD}`,             boxShadow: `0 0 0 2px rgba(16,185,129,0.15), 0 4px 16px rgba(0,0,0,0.12)` },
  failed:    { border: `1.5px solid ${RED}`,                 boxShadow: `0 0 0 2px rgba(239,68,68,0.15), 0 4px 16px rgba(0,0,0,0.12)` },
  cancelled: { border: '1.5px solid rgba(156,163,175,0.4)',  boxShadow: '0 2px 8px rgba(0,0,0,0.08)', opacity: 0.6 },
  skipped:   { border: '1.5px dashed rgba(156,163,175,0.3)', boxShadow: 'none', opacity: 0.3 },
};

const ACCENT_COLOR: Record<NodeExecutionState, string> = {
  pending:   'rgba(156,163,175,0.5)',
  running:   INDIGO,
  completed: EMERALD,
  failed:    RED,
  cancelled: 'rgba(156,163,175,0.5)',
  skipped:   'rgba(156,163,175,0.3)',
};

function StatusIcon({ state }: { state: NodeExecutionState }) {
  switch (state) {
    case 'running':   return <Loader2 size={13} color={INDIGO}  className="animate-spin shrink-0" />;
    case 'completed': return <CheckCircle2 size={13} color={EMERALD} className="shrink-0" />;
    case 'failed':    return <XCircle size={13} color={RED}     className="shrink-0" />;
    case 'cancelled': return <XCircle size={13} color="rgba(156,163,175,0.7)" className="shrink-0" />;
    case 'pending':   return <Clock size={13} color="rgba(156,163,175,0.6)"   className="shrink-0" />;
    default:          return null;
  }
}

const t = {
  surface: 'rgb(var(--surface))',
  text: 'rgb(var(--text))',
  textSec: 'rgb(var(--text-secondary))',
  textTert: 'rgb(var(--text-tertiary))',
};

const HANDLE_STYLE: React.CSSProperties = {
  width: 10, height: 10,
  background: 'white',
  borderRadius: '50%',
  border: `2.5px solid ${INDIGO}`,
};

const ExecutionAgentNode: React.FC<NodeProps> = ({ data }) => {
  const d = data as ExecAgentNodeData;
  const state: NodeExecutionState = d.state ?? 'pending';
  const step = d.step;

  const durationSec = d.durationMs != null
    ? (d.durationMs / 1000).toFixed(1) + 's'
    : null;

  return (
    <div style={{
      width: 240,
      background: t.surface,
      borderRadius: 10,
      overflow: 'visible',
      position: 'relative',
      cursor: state !== 'skipped' ? 'pointer' : 'default',
      transition: 'border-color .15s, box-shadow .15s, opacity .15s',
      ...STATE_STYLES[state],
    }}>
      <div style={{
        position: 'absolute', left: 0, top: 0, bottom: 0, width: 4,
        background: ACCENT_COLOR[state],
        borderRadius: '10px 0 0 10px',
        transition: 'background .15s',
      }} />

      <Handle type="target" position={Position.Top}    style={{ ...HANDLE_STYLE, top: -5,    borderColor: ACCENT_COLOR[state] }} />
      <Handle type="source" position={Position.Bottom} style={{ ...HANDLE_STYLE, bottom: -5, borderColor: ACCENT_COLOR[state] }} />

      <div style={{ padding: '10px 12px 10px 18px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <div style={{
            width: 28, height: 28, borderRadius: 7, flexShrink: 0,
            background: state === 'running' ? 'rgba(99,102,241,0.15)' : 'rgba(99,102,241,0.08)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <Bot size={14} color={ACCENT_COLOR[state]} />
          </div>

          <div style={{ flex: 1, minWidth: 0 }}>
            <p style={{
              margin: 0, fontSize: 13, fontWeight: 600,
              color: state === 'skipped' ? t.textTert : t.text,
              lineHeight: 1.3,
              overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
              textDecoration: state === 'skipped' ? 'line-through' : 'none',
            }}>
              {step.agentName || 'Agent'}
            </p>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexShrink: 0 }}>
            {durationSec && state === 'completed' && (
              <span style={{ fontSize: 10, color: t.textTert }}>{durationSec}</span>
            )}
            <StatusIcon state={state} />
          </div>
        </div>

        {step.passPreviousOutput && state !== 'skipped' && (
          <div style={{ marginTop: 6 }}>
            <span style={{ fontSize: 9, fontWeight: 700, color: 'rgb(var(--accent-purple))', background: 'rgba(168,85,247,0.1)', padding: '1px 6px', borderRadius: 20 }}>↻ chain</span>
          </div>
        )}
      </div>
    </div>
  );
};

export default ExecutionAgentNode;
