import React, { useEffect, useMemo } from 'react';
import {
  ReactFlow,
  ReactFlowProvider,
  Background,
  BackgroundVariant,
  Controls,
  Handle,
  Position,
  useReactFlow,
  MarkerType,
  type Node,
  type Edge,
  type NodeProps,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { WorkflowDefinition, WorkflowExecution, WorkflowStep } from '../../../types';
import { buildExecutionGraph, PILL_W, PILL_X_OFF, EMERALD, RED, INDIGO } from '../../../utils/workflowGraphLayout';
import ExecutionAgentNode, { type NodeExecutionState, type ExecAgentNodeData } from './ExecutionAgentNode';
import ExecutionConditionNode, { type ExecConditionNodeData } from './ExecutionConditionNode';

// ── Canvas CSS ────────────────────────────────────────────────────────────────

const CANVAS_CSS = `
@keyframes exec-running-pulse {
  0%   { box-shadow: 0 0 0 0 rgba(99,102,241,0.5), 0 4px 16px rgba(0,0,0,0.12); }
  70%  { box-shadow: 0 0 0 8px rgba(99,102,241,0), 0 4px 16px rgba(0,0,0,0.12); }
  100% { box-shadow: 0 0 0 0 rgba(99,102,241,0), 0 4px 16px rgba(0,0,0,0.12); }
}
.exec-node-running { animation: exec-running-pulse 1.6s ease-out infinite; }
.react-flow__node:focus { outline: none; }
.react-flow__controls-button { background: rgb(var(--surface)) !important; border-color: rgb(var(--border)) !important; }
.react-flow__controls-button:hover { background: rgb(var(--surface-highlight)) !important; }
.react-flow__controls-button svg { fill: rgb(var(--text)) !important; }
.react-flow__edge-path { transition: stroke .2s, stroke-opacity .2s, stroke-dasharray .2s; }
`;

function injectCss() {
  if (document.getElementById('exec-canvas-css')) return;
  const el = document.createElement('style');
  el.id = 'exec-canvas-css';
  el.textContent = CANVAS_CSS;
  document.head.appendChild(el);
}

// ── Static start/end nodes ────────────────────────────────────────────────────

const ExecStartNode: React.FC<NodeProps> = () => (
  <div style={{
    width: PILL_W, padding: '10px 16px',
    background: 'rgb(var(--surface))',
    border: `2px solid ${EMERALD}`,
    borderRadius: 50,
    textAlign: 'center',
    boxShadow: `0 0 16px rgba(16,185,129,0.2)`,
  }}>
    <span style={{ fontSize: 13, fontWeight: 700, color: 'rgb(var(--text))', letterSpacing: '0.04em' }}>Start</span>
    <Handle type="source" position={Position.Bottom} style={{ width: 10, height: 10, background: 'white', border: `2.5px solid ${EMERALD}`, bottom: -5 }} />
  </div>
);

const ExecEndNode: React.FC<NodeProps> = () => (
  <div style={{
    width: PILL_W, padding: '10px 16px',
    background: 'rgb(var(--surface))',
    border: `2px solid ${RED}`,
    borderRadius: 50,
    textAlign: 'center',
    boxShadow: `0 0 16px rgba(239,68,68,0.2)`,
  }}>
    <Handle type="target" position={Position.Top} style={{ width: 10, height: 10, background: 'white', border: `2.5px solid ${RED}`, top: -5 }} />
    <span style={{ fontSize: 13, fontWeight: 700, color: 'rgb(var(--text))', letterSpacing: '0.04em' }}>End</span>
  </div>
);

// ── Node types ────────────────────────────────────────────────────────────────

const NODE_TYPES = {
  startNode:     ExecStartNode,
  endNode:       ExecEndNode,
  execAgent:     ExecutionAgentNode,
  execCondition: ExecutionConditionNode,
};

// ── Execution state computation ───────────────────────────────────────────────

type StepStateMap = Map<string, { state: NodeExecutionState; jobId?: string; conditionResult?: boolean; durationMs?: number }>;

function computeStepStates(steps: WorkflowStep[], execution: WorkflowExecution): StepStateMap {
  const stateMap: StepStateMap = new Map();
  const stepByOrder = new Map(steps.map(s => [s.order, s]));

  for (const se of execution.stepExecutions) {
    const step = stepByOrder.get(se.stepIndex);
    if (!step) continue;

    let state: NodeExecutionState;
    switch (se.status) {
      case 'Running':          state = 'running';   break;
      case 'Completed':        state = 'completed'; break;
      case 'Failed':           state = 'failed';    break;
      case 'Cancelled':        state = 'cancelled'; break;
      default:                 state = 'pending';
    }

    const conditionResult = se.output === 'true' ? true
                          : se.output === 'false' ? false
                          : undefined;

    const durationMs = se.startedAt && se.completedAt
      ? new Date(se.completedAt).getTime() - new Date(se.startedAt).getTime()
      : undefined;

    stateMap.set(step.id, {
      state,
      jobId: se.jobId ?? undefined,
      conditionResult,
      durationMs,
    });
  }

  return stateMap;
}

function computeSkippedSteps(steps: WorkflowStep[], stateMap: StepStateMap): Set<string> {
  const skipped = new Set<string>();
  const stepById = new Map(steps.map(s => [s.id, s]));

  for (const [stepId, info] of stateMap.entries()) {
    const step = stepById.get(stepId);
    if (!step || step.stepType !== 'Condition' || info.state !== 'completed' || info.conditionResult === undefined) continue;

    // The non-taken branch root
    const skippedRootId = info.conditionResult ? step.falseNextStepId : step.trueNextStepId;
    if (!skippedRootId) continue;

    // BFS to mark all reachable steps from skipped root as skipped
    const queue = [skippedRootId];
    const visited = new Set<string>();
    while (queue.length > 0) {
      const id = queue.shift()!;
      if (visited.has(id)) continue;
      visited.add(id);
      skipped.add(id);
      const s = stepById.get(id);
      if (!s) continue;
      if (s.trueNextStepId)  queue.push(s.trueNextStepId);
      if (s.falseNextStepId) queue.push(s.falseNextStepId);
    }
  }

  return skipped;
}

function applyExecutionState(
  nodes: Node[],
  edges: Edge[],
  steps: WorkflowStep[],
  execution: WorkflowExecution,
): { nodes: Node[]; edges: Edge[] } {
  const stateMap = computeStepStates(steps, execution);
  const skipped  = computeSkippedSteps(steps, stateMap);

  const updatedNodes = nodes.map(node => {
    if (!node.id.startsWith('step-')) return node;
    const stepId = node.id.slice(5);
    const info   = stateMap.get(stepId);
    const isSkipped = skipped.has(stepId);
    const state: NodeExecutionState = isSkipped ? 'skipped' : (info?.state ?? 'pending');

    if (node.type === 'execAgent') {
      return {
        ...node,
        className: state === 'running' ? 'exec-node-running' : '',
        data: { ...node.data, state, jobId: info?.jobId, durationMs: info?.durationMs } as ExecAgentNodeData,
      };
    }
    if (node.type === 'execCondition') {
      return {
        ...node,
        data: { ...node.data, state, conditionResult: info?.conditionResult } as ExecConditionNodeData,
      };
    }
    return node;
  });

  // Build set of "active" edges (edges on the taken execution path)
  const takenEdges = new Set<string>();
  for (const [stepId, info] of stateMap.entries()) {
    if (info.state !== 'completed' && info.state !== 'running') continue;
    // outgoing edges from this step are "active"
    edges.forEach(e => {
      if (e.source === `step-${stepId}`) {
        // For condition: only the taken handle
        const data = e.data as { conditionHandle?: 'true' | 'false' } | undefined;
        if (data?.conditionHandle) {
          const taken = info.conditionResult === true ? 'true' : 'false';
          if (data.conditionHandle === taken) takenEdges.add(e.id);
        } else {
          takenEdges.add(e.id);
        }
      }
    });
    // Also mark the incoming edge as active
    edges.forEach(e => {
      if (e.target === `step-${stepId}`) takenEdges.add(e.id);
    });
  }
  // start → first node
  edges.forEach(e => { if (e.source === 'start') takenEdges.add(e.id); });

  const updatedEdges = edges.map(e => {
    const data = e.data as { conditionHandle?: 'true' | 'false' } | undefined;
    const isActive  = takenEdges.has(e.id);
    const isTrueCondition  = data?.conditionHandle === 'true';
    const isFalseCondition = data?.conditionHandle === 'false';

    if (isActive) {
      let color = INDIGO;
      let label: string | undefined;
      if (isTrueCondition)  { color = EMERALD; label = '✓ True';  }
      if (isFalseCondition) { color = '#f59e0b'; label = '✓ False'; }
      return {
        ...e,
        label,
        labelStyle: { fontSize: 10, fontWeight: 700, fill: color },
        labelBgStyle: { fill: 'rgb(var(--surface))', fillOpacity: 0.9 },
        markerEnd: { type: MarkerType.ArrowClosed, width: 16, height: 16, color },
        style: { stroke: color, strokeWidth: 2.5 },
        animated: false,
      };
    } else {
      // Skipped or not-yet-reached edge
      const isSkippedEdge = e.source.startsWith('step-') && skipped.has(e.source.slice(5));
      return {
        ...e,
        label: undefined,
        markerEnd: { type: MarkerType.ArrowClosed, width: 14, height: 14, color: 'rgba(156,163,175,0.4)' },
        style: {
          stroke: 'rgba(156,163,175,0.4)',
          strokeWidth: 1.5,
          strokeDasharray: isSkippedEdge ? '4 3' : undefined,
          opacity: isSkippedEdge ? 0.4 : 0.5,
        },
        animated: false,
      };
    }
  });

  return { nodes: updatedNodes, edges: updatedEdges };
}

// ── FitView on mount ──────────────────────────────────────────────────────────

const AutoFitView: React.FC = () => {
  const { fitView } = useReactFlow();
  useEffect(() => { setTimeout(() => fitView({ padding: 0.2 }), 50); }, []); // eslint-disable-line react-hooks/exhaustive-deps
  return null;
};

// ── Main canvas ───────────────────────────────────────────────────────────────

interface WorkflowExecutionCanvasProps {
  definition: WorkflowDefinition;
  execution: WorkflowExecution;
  onNodeClick?: (stepId: string, step: WorkflowStep, jobId?: string) => void;
}

const CanvasInner: React.FC<WorkflowExecutionCanvasProps> = ({ definition, execution, onNodeClick }) => {
  useEffect(() => { injectCss(); }, []);

  const { nodes: baseNodes, edges: baseEdges } = useMemo(
    () => buildExecutionGraph(definition.steps),
    [definition.steps],
  );

  const { nodes, edges } = useMemo(
    () => applyExecutionState(baseNodes, baseEdges, definition.steps, execution),
    [baseNodes, baseEdges, definition.steps, execution],
  );

  return (
    <ReactFlow
      nodes={nodes}
      edges={edges}
      nodeTypes={NODE_TYPES}
      nodesDraggable={false}
      nodesConnectable={false}
      elementsSelectable={true}
      zoomOnDoubleClick={false}
      deleteKeyCode={null}
      onNodeClick={(_, node) => {
        if (!node.id.startsWith('step-')) return;
        const stepId = node.id.slice(5);
        const step = definition.steps.find(s => s.id === stepId);
        if (!step) return;
        const jobId = (node.data as { jobId?: string }).jobId;
        onNodeClick?.(stepId, step, jobId);
      }}
      fitView
      fitViewOptions={{ padding: 0.2 }}
      proOptions={{ hideAttribution: true }}
      style={{ background: 'rgb(var(--background))' }}
    >
      <Background variant={BackgroundVariant.Dots} gap={20} size={1} color="rgba(var(--border), 0.4)" />
      <Controls showInteractive={false} />
      <AutoFitView />
    </ReactFlow>
  );
};

const WorkflowExecutionCanvas: React.FC<WorkflowExecutionCanvasProps> = (props) => (
  <ReactFlowProvider>
    <CanvasInner {...props} />
  </ReactFlowProvider>
);

export default WorkflowExecutionCanvas;
