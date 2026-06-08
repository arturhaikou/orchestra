import React from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';
import { GitBranch, CheckCircle2, XCircle, Loader2, Clock } from 'lucide-react';
import { WorkflowStep } from '../../../types';
import { AMBER, EMERALD, RED, INDIGO } from '../../../utils/workflowGraphLayout';
import { type NodeExecutionState } from './ExecutionAgentNode';

export interface ExecConditionNodeData {
  step: WorkflowStep;
  state?: NodeExecutionState;
  conditionResult?: boolean;
  [key: string]: unknown;
}

const STATE_BORDER: Record<NodeExecutionState, string> = {
  pending:   'rgba(245,158,11,0.3)',
  running:   AMBER,
  completed: EMERALD,
  failed:    RED,
  cancelled: 'rgba(156,163,175,0.4)',
  skipped:   'rgba(156,163,175,0.3)',
};

function StatusIcon({ state }: { state: NodeExecutionState }) {
  switch (state) {
    case 'running':   return <Loader2    size={12} color={AMBER}   className="animate-spin shrink-0" />;
    case 'completed': return <CheckCircle2 size={12} color={EMERALD} className="shrink-0" />;
    case 'failed':    return <XCircle   size={12} color={RED}     className="shrink-0" />;
    case 'cancelled': return <XCircle   size={12} color="rgba(156,163,175,0.7)" className="shrink-0" />;
    case 'pending':   return <Clock     size={12} color="rgba(156,163,175,0.6)" className="shrink-0" />;
    default:          return null;
  }
}

const t = {
  surface: 'rgb(var(--surface))',
  text: 'rgb(var(--text))',
  textSec: 'rgb(var(--text-secondary))',
  textTert: 'rgb(var(--text-tertiary))',
};

const ExecutionConditionNode: React.FC<NodeProps> = ({ data }) => {
  const d = data as ExecConditionNodeData;
  const state: NodeExecutionState = d.state ?? 'pending';
  const step = d.step;
  const borderColor = STATE_BORDER[state];

  const handleStyle: React.CSSProperties = {
    width: 10, height: 10,
    background: 'white',
    borderRadius: '50%',
    border: `2.5px solid ${AMBER}`,
  };

  return (
    <div style={{
      width: 210,
      background: t.surface,
      borderRadius: 10,
      border: `1.5px solid ${borderColor}`,
      boxShadow: state === 'running'
        ? `0 0 0 3px rgba(245,158,11,0.2), 0 4px 16px rgba(0,0,0,0.12)`
        : state === 'completed' ? `0 0 0 2px rgba(16,185,129,0.12)` : '0 2px 8px rgba(0,0,0,0.08)',
      overflow: 'visible',
      position: 'relative',
      opacity: state === 'skipped' ? 0.3 : 1,
      transition: 'border-color .15s, box-shadow .15s, opacity .15s',
    }}>
      <div style={{
        position: 'absolute', left: 0, top: 0, bottom: 0, width: 4,
        background: borderColor,
        borderRadius: '10px 0 0 10px',
      }} />

      <Handle type="target" position={Position.Top} style={{ ...handleStyle, top: -5 }} />

      <div style={{ padding: '10px 12px 6px 18px' }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8 }}>
          <div style={{
            width: 28, height: 28, borderRadius: 7, flexShrink: 0,
            background: 'rgba(245,158,11,0.12)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <GitBranch size={14} color={AMBER} />
          </div>

          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 5 }}>
              <p style={{ margin: 0, fontSize: 12, fontWeight: 700, color: t.text }}>Condition</p>
              <StatusIcon state={state} />
            </div>
            <p style={{
              margin: '3px 0 0', fontSize: 10, lineHeight: 1.3,
              color: step.condition ? t.textSec : t.textTert,
              fontStyle: step.condition ? 'normal' : 'italic',
              overflow: 'hidden', textOverflow: 'ellipsis',
              display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical',
            }}>
              {step.condition || 'No condition set'}
            </p>
          </div>
        </div>

        {state === 'completed' && d.conditionResult !== undefined && (
          <div style={{ marginTop: 6, display: 'flex', justifyContent: 'flex-end' }}>
            <span style={{
              fontSize: 10, fontWeight: 700, padding: '2px 8px', borderRadius: 20,
              background: d.conditionResult ? 'rgba(16,185,129,0.15)' : 'rgba(239,68,68,0.15)',
              color: d.conditionResult ? EMERALD : RED,
            }}>
              {d.conditionResult ? '✓ TRUE' : '✗ FALSE'}
            </span>
          </div>
        )}
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', padding: '2px 20px 8px', fontSize: 9, fontWeight: 600 }}>
        <span style={{ color: RED }}>False</span>
        <span style={{ color: EMERALD }}>True</span>
      </div>

      <Handle
        type="source" position={Position.Bottom} id="false"
        style={{ ...handleStyle, bottom: -5, left: '30%', borderColor: RED }}
      />
      <Handle
        type="source" position={Position.Bottom} id="true"
        style={{ ...handleStyle, bottom: -5, left: '70%', borderColor: EMERALD }}
      />
    </div>
  );
};

export default ExecutionConditionNode;
