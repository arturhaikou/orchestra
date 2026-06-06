import React, { useCallback, useEffect, useRef, useState } from 'react';
import Dagre from '@dagrejs/dagre';
import {
  ReactFlow,
  ReactFlowProvider,
  Background,
  BackgroundVariant,
  Controls,
  MiniMap,
  Handle,
  Position,
  BaseEdge,
  EdgeLabelRenderer,
  addEdge,
  useNodesState,
  useEdgesState,
  useReactFlow,
  MarkerType,
  type Node,
  type Edge,
  type Connection,
  type NodeProps,
  type EdgeProps,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import {
  Plus, Trash2, Loader2, Save, ArrowLeft, AlertTriangle,
  ToggleLeft, ToggleRight, Bot, X, Search, GitBranch, Layout,
} from 'lucide-react';
import { WorkflowDefinition, Agent } from '../types';
import {
  getWorkflowDefinitions,
  createWorkflowDefinition,
  updateWorkflowDefinition,
  deleteWorkflowDefinition,
  getWorkflowSystemTools,
  CreateWorkflowStepPayload,
} from '../services/workflowService';
import { getAgents } from '../services/agentService';

// ── Theme ─────────────────────────────────────────────────────────────────────

const t = {
  bg:        'rgb(var(--background))',
  surface:   'rgb(var(--surface))',
  surfaceHl: 'rgb(var(--surface-highlight))',
  border:    'rgb(var(--border))',
  borderEl:  'rgb(var(--border-elevated))',
  text:      'rgb(var(--text))',
  textSec:   'rgb(var(--text-secondary))',
  textTert:  'rgb(var(--text-tertiary))',
  primary:   'rgb(var(--primary))',
  red:       'rgb(var(--accent-red))',
  yellow:    'rgb(var(--accent-yellow))',
  purple:    'rgb(var(--accent-purple))',
  emerald:   'rgb(var(--accent-emerald))',
};

const EMERALD = '#10b981';
const RED     = '#ef4444';
const INDIGO  = '#6366f1';
const AMBER   = '#f59e0b';

// ── Canvas styles (injected once) ─────────────────────────────────────────────

const CANVAS_CSS = `
@keyframes wf-enter { from { opacity:0; transform:scale(0.88); } to { opacity:1; transform:scale(1); } }
@keyframes wf-pulse { 0%{box-shadow:0 0 0 0 rgba(99,102,241,.45)} 70%{box-shadow:0 0 0 8px rgba(99,102,241,0)} 100%{box-shadow:0 0 0 0 rgba(99,102,241,0)} }
@keyframes wf-amber-pulse { 0%{box-shadow:0 0 0 0 rgba(245,158,11,.45)} 70%{box-shadow:0 0 0 8px rgba(245,158,11,0)} 100%{box-shadow:0 0 0 0 rgba(245,158,11,0)} }
.wf-handle:hover { transform:scale(1.45) !important; animation:wf-pulse .6s ease-out !important; }
.wf-handle-amber:hover { transform:scale(1.45) !important; animation:wf-amber-pulse .6s ease-out !important; }
.react-flow__edge-path { transition:stroke .15s, stroke-width .15s, stroke-opacity .15s; }
.react-flow__edge.selected .react-flow__edge-path { stroke:rgba(99,102,241,.95) !important; stroke-width:2.5 !important; }
.react-flow__edge:hover .react-flow__edge-path { stroke-opacity:0.85 !important; }
.react-flow__connection-path { stroke:rgba(99,102,241,.65); stroke-dasharray:5 4; stroke-width:2; }
.react-flow__node:focus { outline:none; }
.react-flow__controls-button { background:rgb(var(--surface)) !important; border-color:rgb(var(--border)) !important; }
.react-flow__controls-button:hover { background:rgb(var(--surface-highlight)) !important; }
.react-flow__controls-button svg { fill:rgb(var(--text)) !important; }
`;

function injectCanvasStyles() {
  if (document.getElementById('wf-canvas-css')) return;
  const el = document.createElement('style');
  el.id = 'wf-canvas-css';
  el.textContent = CANVAS_CSS;
  document.head.appendChild(el);
}

// ── Layout constants ──────────────────────────────────────────────────────────

const STEP_W    = 240;
const PILL_W    = 160;
const NODE_Y_GAP = 140;
const PILL_X_OFF = (STEP_W - PILL_W) / 2;

// ── Types ─────────────────────────────────────────────────────────────────────

interface WorkflowBuilderProps { workspaceId: string; }

interface AgentStepNodeData {
  agentId: string;
  agentName: string;
  agentRole: string;
  instructionOverride: string;
  passPreviousOutput: boolean;
  systemTools: string[];
  isNew?: boolean;
  [key: string]: unknown;
}

interface ConditionNodeData {
  condition: string;
  isNew?: boolean;
  [key: string]: unknown;
}

// ── Position persistence ──────────────────────────────────────────────────────

const posKey = (id: string) => `wf-pos:${id}`;

function loadPositions(id: string): Record<string, { x: number; y: number }> {
  try { return JSON.parse(localStorage.getItem(posKey(id)) ?? '{}'); } catch { return {}; }
}

function savePositions(id: string, nodes: Node[]) {
  const m: Record<string, { x: number; y: number }> = {};
  nodes.forEach(n => { m[n.id] = n.position; });
  localStorage.setItem(posKey(id), JSON.stringify(m));
}

// ── Edge factory ──────────────────────────────────────────────────────────────

function makeEdge(id: string, source: string, target: string, sourceHandle?: string): Edge {
  return {
    id,
    source,
    target,
    sourceHandle: sourceHandle ?? null,
    type: 'wfEdge',
    markerEnd: { type: MarkerType.ArrowClosed, width: 16, height: 16, color: INDIGO },
    style: { stroke: 'rgba(99,102,241,0.45)', strokeWidth: 2 },
    data: { midOffsetX: 0, midOffsetY: 0 },
  };
}

// ── Graph serialization ───────────────────────────────────────────────────────

function extractId(nodeId: string): string {
  const i = nodeId.indexOf('-');
  return i >= 0 ? nodeId.slice(i + 1) : nodeId;
}

function serializeGraph(nodes: Node[], edges: Edge[]): CreateWorkflowStepPayload[] {
  const adj = new Map<string, string[]>();
  const handleAdj = new Map<string, string>();

  edges.forEach(e => {
    if (!adj.has(e.source)) adj.set(e.source, []);
    adj.get(e.source)!.push(e.target);
    if (e.sourceHandle) handleAdj.set(`${e.source}__${e.sourceHandle}`, e.target);
  });

  const nodeMap = new Map<string, Node>();
  nodes.forEach(n => nodeMap.set(n.id, n));

  const visited = new Set<string>();
  const result: CreateWorkflowStepPayload[] = [];

  function visit(nodeId: string) {
    if (visited.has(nodeId)) return;
    const node = nodeMap.get(nodeId);
    if (!node || node.id.startsWith('end')) return;

    if (node.type === 'startNode') {
      visited.add(nodeId);
      (adj.get(nodeId) ?? []).forEach(visit);
      return;
    }

    visited.add(nodeId);
    const nexts = adj.get(nodeId) ?? [];

    if (node.type === 'agentStep') {
      const d = node.data as AgentStepNodeData;
      const nextNode = nexts.find(n => !n.startsWith('end'));
      result.push({
        clientId: extractId(nodeId),
        order: result.length,
        agentId: d.agentId,
        instructionOverride: d.instructionOverride || null,
        passPreviousOutput: d.passPreviousOutput,
        systemTools: d.systemTools.length > 0 ? d.systemTools : null,
        type: 'Agent',
        trueNextClientId: nextNode ? extractId(nextNode) : null,
        falseNextClientId: null,
        condition: null,
      });
      nexts.forEach(visit);
    } else if (node.type === 'condition') {
      const d = node.data as ConditionNodeData;
      const trueNext = handleAdj.get(`${nodeId}__true`);
      const falseNext = handleAdj.get(`${nodeId}__false`);
      result.push({
        clientId: extractId(nodeId),
        order: result.length,
        agentId: null,
        instructionOverride: null,
        passPreviousOutput: false,
        systemTools: null,
        type: 'Condition',
        condition: d.condition || null,
        trueNextClientId: trueNext && !trueNext.startsWith('end') ? extractId(trueNext) : null,
        falseNextClientId: falseNext && !falseNext.startsWith('end') ? extractId(falseNext) : null,
      });
      [trueNext, falseNext].forEach(n => { if (n) visit(n); });
    }
  }

  visit('start');
  return result;
}

// ── Auto-layout ───────────────────────────────────────────────────────────────

function computeAutoLayout(nodes: Node[], edges: Edge[]): Map<string, { x: number; y: number }> {
  const g = new Dagre.graphlib.Graph().setDefaultEdgeLabel(() => ({}));
  g.setGraph({ rankdir: 'TB', nodesep: 60, ranksep: 80 });

  nodes.forEach(n => {
    const w = (n.type === 'startNode' || n.type === 'endNode') ? PILL_W
             : n.type === 'condition' ? 210 : STEP_W;
    const h = (n.type === 'startNode' || n.type === 'endNode') ? 38
             : n.type === 'condition' ? 80 : 72;
    g.setNode(n.id, { width: w, height: h });
  });

  edges.forEach(e => { try { g.setEdge(e.source, e.target); } catch { /* skip invalid */ } });
  Dagre.layout(g);

  const out = new Map<string, { x: number; y: number }>();
  nodes.forEach(n => {
    const nd = g.node(n.id);
    if (nd?.x != null) out.set(n.id, { x: nd.x - nd.width / 2, y: nd.y - nd.height / 2 });
  });
  return out;
}

// ── Handle styles ─────────────────────────────────────────────────────────────

const HANDLE_INDIGO: React.CSSProperties = {
  width: 12, height: 12,
  background: 'white',
  borderRadius: '50%',
  border: `2.5px solid ${INDIGO}`,
  transition: 'transform .15s',
  cursor: 'crosshair',
};

const HANDLE_AMBER: React.CSSProperties = {
  ...HANDLE_INDIGO,
  border: `2.5px solid ${AMBER}`,
};

// ── Custom Nodes ──────────────────────────────────────────────────────────────

const StartNode: React.FC<NodeProps> = () => (
  <div style={{
    width: PILL_W, padding: '10px 16px',
    background: t.surface,
    border: `2px solid ${EMERALD}`,
    borderRadius: 50,
    textAlign: 'center',
    boxShadow: `0 0 20px rgba(16,185,129,0.28), 0 2px 8px rgba(0,0,0,0.15)`,
  }}>
    <span style={{ fontSize: 13, fontWeight: 700, color: t.text, letterSpacing: '0.04em' }}>Start</span>
    <Handle
      type="source"
      position={Position.Bottom}
      className="wf-handle"
      style={{ ...HANDLE_INDIGO, bottom: -6, border: `2.5px solid ${EMERALD}` }}
    />
  </div>
);

const EndNode: React.FC<NodeProps> = () => (
  <div style={{
    width: PILL_W, padding: '10px 16px',
    background: t.surface,
    border: `2px solid ${RED}`,
    borderRadius: 50,
    textAlign: 'center',
    boxShadow: `0 0 20px rgba(239,68,68,0.28), 0 2px 8px rgba(0,0,0,0.15)`,
  }}>
    <Handle
      type="target"
      position={Position.Top}
      className="wf-handle"
      style={{ ...HANDLE_INDIGO, top: -6, border: `2.5px solid ${RED}` }}
    />
    <span style={{ fontSize: 13, fontWeight: 700, color: t.text, letterSpacing: '0.04em' }}>End</span>
  </div>
);

const AgentStepNode: React.FC<NodeProps> = ({ id, data, selected }) => {
  const { setNodes, deleteElements } = useReactFlow();
  const d = data as AgentStepNodeData;

  useEffect(() => {
    if (!d.isNew) return;
    const timer = setTimeout(() => {
      setNodes(nds => nds.map(n => n.id === id ? { ...n, data: { ...n.data, isNew: false } } : n));
    }, 350);
    return () => clearTimeout(timer);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div style={{
      width: STEP_W,
      background: t.surface,
      borderRadius: 10,
      border: `1.5px solid ${selected ? INDIGO : 'rgba(99,102,241,0.32)'}`,
      boxShadow: selected
        ? `0 0 0 3px rgba(99,102,241,0.2), 0 8px 24px rgba(0,0,0,0.18)`
        : '0 2px 10px rgba(0,0,0,0.1)',
      overflow: 'visible',
      transition: 'border-color .15s, box-shadow .15s',
      animation: d.isNew ? 'wf-enter .22s ease-out forwards' : undefined,
      position: 'relative',
    }}>
      {/* Left accent strip */}
      <div style={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: 4, background: INDIGO, borderRadius: '10px 0 0 10px' }} />

      <Handle type="target" position={Position.Top} className="wf-handle" style={{ ...HANDLE_INDIGO, top: -6 }} />

      <div style={{ padding: '10px 12px 10px 18px' }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 8 }}>
          <div style={{
            width: 28, height: 28, borderRadius: 7, flexShrink: 0,
            background: 'rgba(99,102,241,0.12)',
            display: 'flex', alignItems: 'center', justifyContent: 'center',
          }}>
            <Bot size={14} color={INDIGO} />
          </div>

          <div style={{ flex: 1, minWidth: 0 }}>
            <p style={{
              margin: 0, fontSize: 13, fontWeight: 600,
              color: d.agentName ? t.text : t.textTert,
              fontStyle: d.agentName ? 'normal' : 'italic',
              lineHeight: 1.3, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
            }}>
              {d.agentName || 'No agent'}
            </p>
            {d.agentRole && (
              <p style={{ margin: '2px 0 0', fontSize: 10, color: t.textTert, lineHeight: 1.2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                {d.agentRole}
              </p>
            )}
          </div>

          <button
            onClick={e => { e.stopPropagation(); deleteElements({ nodes: [{ id }] }); }}
            style={{
              padding: 3, border: 'none', background: 'transparent',
              cursor: 'pointer', color: t.textTert, display: 'flex',
              borderRadius: 5, flexShrink: 0, transition: 'color .1s, background .1s',
            }}
            onMouseEnter={e => { const b = e.currentTarget; b.style.color = RED; b.style.background = 'rgba(239,68,68,0.1)'; }}
            onMouseLeave={e => { const b = e.currentTarget; b.style.color = t.textTert; b.style.background = 'transparent'; }}
          >
            <X size={11} />
          </button>
        </div>

        {(d.passPreviousOutput || !!d.instructionOverride) && (
          <div style={{ display: 'flex', gap: 4, marginTop: 8, flexWrap: 'wrap' }}>
            {d.passPreviousOutput && (
              <span style={{ fontSize: 9, fontWeight: 700, color: t.purple, background: 'rgba(168,85,247,0.1)', padding: '1px 6px', borderRadius: 20 }}>↻ chain</span>
            )}
            {!!d.instructionOverride && (
              <span style={{ fontSize: 9, color: t.yellow, background: 'rgba(234,179,8,0.1)', padding: '1px 6px', borderRadius: 20 }}>+ notes</span>
            )}
          </div>
        )}
      </div>

      <Handle type="source" position={Position.Bottom} className="wf-handle" style={{ ...HANDLE_INDIGO, bottom: -6 }} />
    </div>
  );
};

const ConditionNode: React.FC<NodeProps> = ({ id, data, selected }) => {
  const { setNodes, deleteElements } = useReactFlow();
  const d = data as ConditionNodeData;

  useEffect(() => {
    if (!d.isNew) return;
    const timer = setTimeout(() => {
      setNodes(nds => nds.map(n => n.id === id ? { ...n, data: { ...n.data, isNew: false } } : n));
    }, 350);
    return () => clearTimeout(timer);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div style={{
      width: 210,
      background: t.surface,
      borderRadius: 10,
      border: `1.5px solid ${selected ? AMBER : 'rgba(245,158,11,0.32)'}`,
      boxShadow: selected
        ? `0 0 0 3px rgba(245,158,11,0.2), 0 8px 24px rgba(0,0,0,0.18)`
        : '0 2px 10px rgba(0,0,0,0.1)',
      overflow: 'visible',
      transition: 'border-color .15s, box-shadow .15s',
      animation: d.isNew ? 'wf-enter .22s ease-out forwards' : undefined,
      position: 'relative',
    }}>
      <div style={{ position: 'absolute', left: 0, top: 0, bottom: 0, width: 4, background: AMBER, borderRadius: '10px 0 0 10px' }} />

      <Handle type="target" position={Position.Top} className="wf-handle-amber" style={{ ...HANDLE_AMBER, top: -6 }} />

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
            <p style={{ margin: 0, fontSize: 12, fontWeight: 700, color: t.text, lineHeight: 1.3 }}>Condition</p>
            <p style={{
              margin: '3px 0 0', fontSize: 10, lineHeight: 1.3,
              color: d.condition ? t.textSec : t.textTert,
              fontStyle: d.condition ? 'normal' : 'italic',
              overflow: 'hidden', textOverflow: 'ellipsis',
              display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical',
            }}>
              {d.condition || 'No condition set'}
            </p>
          </div>

          <button
            onClick={e => { e.stopPropagation(); deleteElements({ nodes: [{ id }] }); }}
            style={{
              padding: 3, border: 'none', background: 'transparent',
              cursor: 'pointer', color: t.textTert, display: 'flex',
              borderRadius: 5, flexShrink: 0, transition: 'color .1s, background .1s',
            }}
            onMouseEnter={e => { const b = e.currentTarget; b.style.color = RED; b.style.background = 'rgba(239,68,68,0.1)'; }}
            onMouseLeave={e => { const b = e.currentTarget; b.style.color = t.textTert; b.style.background = 'transparent'; }}
          >
            <X size={11} />
          </button>
        </div>
      </div>

      {/* Branch labels */}
      <div style={{ display: 'flex', justifyContent: 'space-between', padding: '2px 20px 10px', fontSize: 9, fontWeight: 600 }}>
        <span style={{ color: RED }}>False</span>
        <span style={{ color: EMERALD }}>True</span>
      </div>

      <Handle
        type="source"
        position={Position.Bottom}
        id="false"
        className="wf-handle-amber"
        style={{ ...HANDLE_AMBER, bottom: -6, left: '30%', border: `2.5px solid ${RED}` }}
      />
      <Handle
        type="source"
        position={Position.Bottom}
        id="true"
        className="wf-handle-amber"
        style={{ ...HANDLE_AMBER, bottom: -6, left: '70%', border: `2.5px solid ${EMERALD}` }}
      />
    </div>
  );
};

// ── Custom Edge ───────────────────────────────────────────────────────────────

const WorkflowEdge: React.FC<EdgeProps> = ({
  id, sourceX, sourceY, targetX, targetY, markerEnd, style, selected, data,
}) => {
  const { deleteElements, setEdges, screenToFlowPosition } = useReactFlow();
  const [hovered, setHovered] = useState(false);
  const isDragging = useRef(false);

  const midOffsetX = (data?.midOffsetX as number) ?? 0;
  const midOffsetY = (data?.midOffsetY as number) ?? 0;

  const ctrlX = (sourceX + targetX) / 2 + midOffsetX;
  const ctrlY = (sourceY + targetY) / 2 + midOffsetY;
  const edgePath = `M ${sourceX},${sourceY} Q ${ctrlX},${ctrlY} ${targetX},${targetY}`;

  // midpoint along a quadratic bezier at t=0.5
  const midX = 0.25 * sourceX + 0.5 * ctrlX + 0.25 * targetX;
  const midY = 0.25 * sourceY + 0.5 * ctrlY + 0.25 * targetY;

  const showHandle = (selected || hovered) && !isDragging.current;

  const onBendMouseDown = (e: React.MouseEvent) => {
    e.stopPropagation();
    isDragging.current = true;
    let prevX = e.clientX;
    let prevY = e.clientY;

    const onMove = (me: MouseEvent) => {
      const prev = screenToFlowPosition({ x: prevX, y: prevY });
      const curr = screenToFlowPosition({ x: me.clientX, y: me.clientY });
      const dx = curr.x - prev.x;
      const dy = curr.y - prev.y;
      prevX = me.clientX;
      prevY = me.clientY;
      setEdges(eds => eds.map(ed =>
        ed.id === id
          ? { ...ed, data: { ...ed.data, midOffsetX: ((ed.data?.midOffsetX as number) ?? 0) + dx, midOffsetY: ((ed.data?.midOffsetY as number) ?? 0) + dy } }
          : ed
      ));
    };

    const onUp = () => {
      isDragging.current = false;
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };

    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  };

  const onBendDblClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    setEdges(eds => eds.map(ed =>
      ed.id === id ? { ...ed, data: { ...ed.data, midOffsetX: 0, midOffsetY: 0 } } : ed
    ));
  };

  return (
    <>
      {/* Wider invisible hit area for hover detection */}
      <path
        d={edgePath}
        fill="none"
        stroke="transparent"
        strokeWidth={16}
        onMouseEnter={() => setHovered(true)}
        onMouseLeave={() => setHovered(false)}
        style={{ cursor: 'pointer' }}
      />
      <BaseEdge id={id} path={edgePath} markerEnd={markerEnd} style={style} />
      <EdgeLabelRenderer>
        {/* Bend handle */}
        {(selected || hovered) && (
          <div
            style={{
              position: 'absolute',
              transform: `translate(-50%,-50%) translate(${midX}px,${midY}px)`,
              pointerEvents: 'all',
              width: 10, height: 10, borderRadius: '50%',
              background: INDIGO, border: '2px solid white',
              cursor: 'grab', zIndex: 5,
              opacity: showHandle ? 1 : 0,
              transition: 'opacity .15s',
            }}
            onMouseDown={onBendMouseDown}
            onDoubleClick={onBendDblClick}
            title="Drag to bend · Double-click to straighten"
          />
        )}
        {/* Delete button */}
        {selected && (
          <button
            style={{
              position: 'absolute',
              transform: `translate(-50%,-50%) translate(${midX + 14}px,${midY - 14}px)`,
              pointerEvents: 'all',
              width: 18, height: 18, borderRadius: '50%',
              background: 'rgba(239,68,68,0.9)', border: 'none',
              cursor: 'pointer', color: 'white',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              zIndex: 10,
            }}
            onClick={e => { e.stopPropagation(); deleteElements({ edges: [{ id }] }); }}
            title="Delete connection"
          >
            <X size={8} />
          </button>
        )}
      </EdgeLabelRenderer>
    </>
  );
};

// ── Stable type registries ────────────────────────────────────────────────────

const nodeTypes = {
  startNode: StartNode,
  endNode: EndNode,
  agentStep: AgentStepNode,
  condition: ConditionNode,
};

const edgeTypes = { wfEdge: WorkflowEdge };

// ── WorkflowEditorInner (uses useReactFlow — must be inside ReactFlowProvider) ─

interface EditorProps {
  workspaceId: string;
  editingId: string | null;
  initialName: string;
  initialDescription: string;
  initialNodes: Node[];
  initialEdges: Edge[];
  agents: Agent[];
  availableSystemTools: string[];
  onSaved: (wf: WorkflowDefinition) => void;
  onBack: () => void;
  onDelete: (id: string) => void;
}

const WorkflowEditorInner: React.FC<EditorProps> = ({
  workspaceId, editingId,
  initialName, initialDescription,
  initialNodes, initialEdges,
  agents, availableSystemTools,
  onSaved, onBack, onDelete,
}) => {
  const { fitView, screenToFlowPosition, getEdges } = useReactFlow();
  const [rfNodes, setRfNodes, onNodesChange] = useNodesState(initialNodes);
  const [rfEdges, setRfEdges, onEdgesChange] = useEdgesState(initialEdges);
  const [name, setName]               = useState(initialName);
  const [description, setDescription] = useState(initialDescription);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [agentSearch, setAgentSearch] = useState('');
  const [isSaving, setIsSaving]       = useState(false);
  const [saveError, setSaveError]     = useState<string | null>(null);
  const [deleteConfirm, setDeleteConfirm] = useState(false);
  const [isDeletingWf, setIsDeletingWf]   = useState(false);
  const wrapperRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const id = setTimeout(() => fitView({ duration: 400, padding: 0.35 }), 80);
    return () => clearTimeout(id);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  const onNodeDragStop = useCallback((_: MouseEvent, _node: Node, allNodes: Node[]) => {
    savePositions(editingId ?? 'new', allNodes);
  }, [editingId]);

  const isValidConnection = useCallback((conn: Connection) => {
    if (conn.source === conn.target) return false;
    const edges = getEdges();
    return !edges.some(e => e.source === conn.source && e.sourceHandle === (conn.sourceHandle ?? null));
  }, [getEdges]);

  const handleConnect = useCallback((params: Connection) => {
    setRfEdges(prev => addEdge({
      ...params,
      type: 'wfEdge',
      markerEnd: { type: MarkerType.ArrowClosed, width: 16, height: 16, color: INDIGO },
      style: { stroke: 'rgba(99,102,241,0.45)', strokeWidth: 2 },
    }, prev));
  }, []);

  const handleDragOver = (e: React.DragEvent) => { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    const position = screenToFlowPosition({ x: e.clientX, y: e.clientY });
    const nodeType = e.dataTransfer.getData('application/node-type');
    const agentId  = e.dataTransfer.getData('application/agent-id');

    if (nodeType === 'condition') {
      const id = `cond-${crypto.randomUUID()}`;
      setRfNodes(prev => [...prev, { id, type: 'condition', position, data: { condition: '', isNew: true } as ConditionNodeData }]);
    } else if (nodeType === 'end') {
      const id = `end-${crypto.randomUUID()}`;
      setRfNodes(prev => [...prev, { id, type: 'endNode', position, data: {} }]);
    } else if (agentId) {
      const agent = agents.find(a => a.id === agentId);
      if (!agent) return;
      const id = `step-${crypto.randomUUID()}`;
      setRfNodes(prev => [...prev, {
        id, type: 'agentStep', position,
        data: {
          agentId: agent.id, agentName: agent.name, agentRole: agent.role,
          instructionOverride: '', passPreviousOutput: false, systemTools: [], isNew: true,
        } as AgentStepNodeData,
      }]);
    }
  };

  const updateSelectedData = useCallback((patch: Partial<AgentStepNodeData | ConditionNodeData>) => {
    if (!selectedNodeId) return;
    setRfNodes(prev => prev.map(n =>
      n.id === selectedNodeId ? { ...n, data: { ...n.data, ...patch } } : n
    ));
  }, [selectedNodeId]);

  const handleAutoLayout = useCallback(() => {
    const positions = computeAutoLayout(rfNodes, rfEdges);
    setRfNodes(prev => prev.map(n => {
      const pos = positions.get(n.id);
      return pos ? { ...n, position: pos } : n;
    }));
    setTimeout(() => fitView({ duration: 400, padding: 0.35 }), 50);
  }, [rfNodes, rfEdges, fitView]);

  const handleSave = async () => {
    if (!name.trim()) { setSaveError('Workflow name is required.'); return; }
    const steps = serializeGraph(rfNodes, rfEdges);
    if (steps.filter(s => s.type === 'Agent' || !s.type).some(s => !s.agentId)) {
      setSaveError('Each agent step must have an agent selected.');
      return;
    }
    setSaveError(null);
    setIsSaving(true);
    try {
      const payload = { name: name.trim(), description: description.trim() || null, steps };
      const saved = editingId
        ? await updateWorkflowDefinition(editingId, payload)
        : await createWorkflowDefinition({ workspaceId, ...payload });
      savePositions(saved.id, rfNodes);
      onSaved(saved);
    } catch (e: any) {
      setSaveError(e?.message ?? 'Failed to save workflow.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleDeleteWf = async () => {
    if (!editingId) return;
    setIsDeletingWf(true);
    try {
      await deleteWorkflowDefinition(editingId);
      onDelete(editingId);
      onBack();
    } catch (e) { console.error(e); }
    finally { setIsDeletingWf(false); setDeleteConfirm(false); }
  };

  const selectedNode = rfNodes.find(n => n.id === selectedNodeId);
  const filteredAgents = agents.filter(a =>
    a.name.toLowerCase().includes(agentSearch.toLowerCase()) ||
    a.role.toLowerCase().includes(agentSearch.toLowerCase())
  );

  return (
    <div style={{ position: 'absolute', inset: 0, zIndex: 10, display: 'flex', flexDirection: 'column', background: t.bg }}>

      {/* Top toolbar */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '8px 14px', background: t.surface, borderBottom: `1px solid ${t.border}`, flexShrink: 0, minHeight: 50 }}>
        <button
          onClick={onBack}
          style={{ padding: 6, borderRadius: 8, border: 'none', background: 'transparent', cursor: 'pointer', color: t.textSec, display: 'flex' }}
          onMouseEnter={e => { const b = e.currentTarget; b.style.background = t.surfaceHl; b.style.color = t.text; }}
          onMouseLeave={e => { const b = e.currentTarget; b.style.background = 'transparent'; b.style.color = t.textSec; }}
        >
          <ArrowLeft size={18} />
        </button>

        <input
          type="text"
          value={name}
          onChange={e => setName(e.target.value)}
          placeholder="Workflow name…"
          style={{ flex: 1, background: 'transparent', border: 'none', outline: 'none', fontSize: 15, fontWeight: 700, color: t.text, minWidth: 0 }}
        />

        {saveError && (
          <div style={{ display: 'flex', alignItems: 'center', gap: 5, color: t.red, fontSize: 12, flexShrink: 0 }}>
            <AlertTriangle size={13} /><span>{saveError}</span>
          </div>
        )}

        <button
          onClick={handleAutoLayout}
          title="Auto-arrange nodes"
          style={{ display: 'flex', alignItems: 'center', gap: 5, padding: '6px 10px', borderRadius: 8, border: `1px solid ${t.border}`, background: 'transparent', cursor: 'pointer', color: t.textSec, fontSize: 12 }}
          onMouseEnter={e => { const b = e.currentTarget; b.style.background = t.surfaceHl; b.style.color = t.text; }}
          onMouseLeave={e => { const b = e.currentTarget; b.style.background = 'transparent'; b.style.color = t.textSec; }}
        >
          <Layout size={14} />
        </button>

        {editingId && (
          <button
            onClick={() => setDeleteConfirm(true)}
            style={{ padding: '6px 12px', borderRadius: 8, border: `1px solid rgba(239,68,68,0.3)`, background: 'transparent', cursor: 'pointer', color: t.red, fontSize: 12, flexShrink: 0 }}
          >
            Delete
          </button>
        )}

        <button
          onClick={handleSave}
          disabled={isSaving}
          style={{ display: 'flex', alignItems: 'center', gap: 6, padding: '7px 16px', borderRadius: 8, border: 'none', background: t.primary, color: '#fff', cursor: isSaving ? 'not-allowed' : 'pointer', fontSize: 13, fontWeight: 600, flexShrink: 0, opacity: isSaving ? 0.7 : 1 }}
        >
          {isSaving ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />}
          Save
        </button>
      </div>

      {/* Main area */}
      <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>

        {/* Left sidebar */}
        <div style={{ width: 220, background: t.surface, borderRight: `1px solid ${t.border}`, display: 'flex', flexDirection: 'column', overflow: 'hidden', flexShrink: 0 }}>

          {/* Node palette */}
          <div style={{ padding: '10px 10px 8px', borderBottom: `1px solid ${t.border}` }}>
            <p style={{ margin: '0 0 7px', fontSize: 10, fontWeight: 700, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.08em' }}>Nodes</p>

            <button
              draggable
              onDragStart={e => { e.dataTransfer.setData('application/node-type', 'condition'); e.dataTransfer.effectAllowed = 'copy'; }}
              style={{ width: '100%', display: 'flex', alignItems: 'center', gap: 9, padding: '7px 8px', borderRadius: 8, border: `1px dashed rgba(245,158,11,0.45)`, background: 'rgba(245,158,11,0.06)', cursor: 'grab', textAlign: 'left', marginBottom: 4 }}
            >
              <div style={{ width: 26, height: 26, borderRadius: 6, background: 'rgba(245,158,11,0.14)', flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <GitBranch size={13} color={AMBER} />
              </div>
              <div>
                <p style={{ margin: 0, fontSize: 12, fontWeight: 600, color: t.textSec }}>Condition</p>
                <p style={{ margin: 0, fontSize: 10, color: t.textTert }}>Branch logic</p>
              </div>
            </button>

            <button
              draggable
              onDragStart={e => { e.dataTransfer.setData('application/node-type', 'end'); e.dataTransfer.effectAllowed = 'copy'; }}
              style={{ width: '100%', display: 'flex', alignItems: 'center', gap: 9, padding: '7px 8px', borderRadius: 8, border: `1px dashed rgba(239,68,68,0.4)`, background: 'rgba(239,68,68,0.06)', cursor: 'grab', textAlign: 'left' }}
            >
              <div style={{ width: 26, height: 26, borderRadius: 6, background: 'rgba(239,68,68,0.14)', flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                <div style={{ width: 10, height: 10, borderRadius: '50%', background: RED }} />
              </div>
              <div>
                <p style={{ margin: 0, fontSize: 12, fontWeight: 600, color: t.textSec }}>End</p>
                <p style={{ margin: 0, fontSize: 10, color: t.textTert }}>Branch endpoint</p>
              </div>
            </button>
          </div>

          {/* Agent search */}
          <div style={{ padding: '8px 10px 6px', borderBottom: `1px solid ${t.border}` }}>
            <p style={{ margin: '0 0 6px', fontSize: 10, fontWeight: 700, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.08em' }}>Agents</p>
            <div style={{ position: 'relative' }}>
              <Search size={12} style={{ position: 'absolute', left: 8, top: 8, color: t.textTert, pointerEvents: 'none' }} />
              <input
                type="text"
                value={agentSearch}
                onChange={e => setAgentSearch(e.target.value)}
                placeholder="Search…"
                style={{ width: '100%', padding: '6px 8px 6px 26px', borderRadius: 7, border: `1px solid ${t.border}`, background: t.surfaceHl, color: t.text, fontSize: 12, outline: 'none', boxSizing: 'border-box' }}
              />
            </div>
          </div>

          {/* Agent list */}
          <div style={{ flex: 1, overflowY: 'auto', padding: '6px' }}>
            {filteredAgents.length === 0 && (
              <p style={{ fontSize: 12, color: t.textTert, textAlign: 'center', padding: 16, fontStyle: 'italic' }}>
                {agentSearch ? 'No matches' : 'No agents'}
              </p>
            )}
            {filteredAgents.map(agent => (
              <button
                key={agent.id}
                draggable
                onDragStart={e => { e.dataTransfer.setData('application/agent-id', agent.id); e.dataTransfer.effectAllowed = 'copy'; }}
                style={{ width: '100%', display: 'flex', alignItems: 'center', gap: 9, padding: '7px 8px', borderRadius: 8, border: 'none', background: 'transparent', cursor: 'grab', textAlign: 'left', marginBottom: 2 }}
                onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.background = t.surfaceHl; }}
                onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.background = 'transparent'; }}
                title={`Drag to canvas to add ${agent.name}`}
              >
                <div style={{ width: 28, height: 28, borderRadius: 7, background: 'rgba(99,102,241,0.1)', flexShrink: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                  <Bot size={13} color={INDIGO} />
                </div>
                <div style={{ minWidth: 0, flex: 1 }}>
                  <p style={{ margin: 0, fontSize: 12, fontWeight: 600, color: t.textSec, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{agent.name}</p>
                  <p style={{ margin: 0, fontSize: 10, color: t.textTert, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{agent.role}</p>
                </div>
                <Plus size={11} color={t.textTert} style={{ flexShrink: 0 }} />
              </button>
            ))}
          </div>

          {/* Description */}
          <div style={{ padding: '10px 12px', borderTop: `1px solid ${t.border}` }}>
            <p style={{ margin: '0 0 5px', fontSize: 10, fontWeight: 700, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.08em' }}>Description</p>
            <textarea
              value={description}
              onChange={e => setDescription(e.target.value)}
              rows={2}
              placeholder="Optional description…"
              style={{ width: '100%', padding: '6px 8px', borderRadius: 6, border: `1px solid ${t.border}`, background: t.surfaceHl, color: t.textSec, fontSize: 11, resize: 'none', outline: 'none', boxSizing: 'border-box', fontFamily: 'inherit' }}
            />
          </div>
        </div>

        {/* Canvas */}
        <div
          ref={wrapperRef}
          onDrop={handleDrop}
          onDragOver={handleDragOver}
          style={{ flex: 1, position: 'relative', overflow: 'hidden', background: t.bg }}
        >
          <ReactFlow
            nodes={rfNodes}
            edges={rfEdges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={handleConnect}
            isValidConnection={isValidConnection}
            onNodeClick={(_, node) => setSelectedNodeId(node.id)}
            onPaneClick={() => setSelectedNodeId(null)}
            onNodeDragStop={onNodeDragStop}
            nodeTypes={nodeTypes}
            edgeTypes={edgeTypes}
            connectionLineStyle={{ stroke: 'rgba(99,102,241,.65)', strokeWidth: 2, strokeDasharray: '5 4' }}
            nodesDraggable
            nodesConnectable
            elementsSelectable
            deleteKeyCode="Delete"
            proOptions={{ hideAttribution: true }}
            minZoom={0.15}
            maxZoom={2}
          >
            <Background variant={BackgroundVariant.Dots} gap={24} size={1.3} color="rgba(148,163,184,0.18)" />
            <Controls style={{ background: t.surface, border: `1px solid ${t.border}`, borderRadius: 8, overflow: 'hidden' }} />
            <MiniMap
              style={{ background: t.surface, border: `1px solid ${t.border}`, borderRadius: 8 }}
              nodeColor={n => n.type === 'condition' ? AMBER : n.type === 'startNode' ? EMERALD : n.type === 'endNode' ? RED : INDIGO}
              maskColor="rgba(148,163,184,0.07)"
            />
          </ReactFlow>
        </div>

        {/* Right config panel */}
        {selectedNode && (selectedNode.type === 'agentStep' || selectedNode.type === 'condition') && (
          <div style={{ width: 288, background: t.surface, borderLeft: `1px solid ${t.border}`, display: 'flex', flexDirection: 'column', overflow: 'hidden', flexShrink: 0 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', borderBottom: `1px solid ${t.border}` }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                {selectedNode.type === 'agentStep'
                  ? <><Bot size={14} color={INDIGO} /><span style={{ fontSize: 13, fontWeight: 600, color: t.text }}>Agent Step</span></>
                  : <><GitBranch size={14} color={AMBER} /><span style={{ fontSize: 13, fontWeight: 600, color: t.text }}>Condition</span></>
                }
              </div>
              <button
                onClick={() => setSelectedNodeId(null)}
                style={{ padding: 4, borderRadius: 6, border: 'none', background: 'transparent', cursor: 'pointer', color: t.textTert, display: 'flex' }}
                onMouseEnter={e => { const b = e.currentTarget; b.style.color = t.text; b.style.background = t.surfaceHl; }}
                onMouseLeave={e => { const b = e.currentTarget; b.style.color = t.textTert; b.style.background = 'transparent'; }}
              >
                <X size={15} />
              </button>
            </div>

            <div style={{ flex: 1, overflowY: 'auto', padding: 16, display: 'flex', flexDirection: 'column', gap: 16 }}>

              {/* Agent step config */}
              {selectedNode.type === 'agentStep' && (() => {
                const d = selectedNode.data as AgentStepNodeData;
                return (
                  <>
                    <div>
                      <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>Agent *</label>
                      <select
                        value={d.agentId}
                        onChange={e => {
                          const agent = agents.find(a => a.id === e.target.value);
                          updateSelectedData({ agentId: e.target.value, agentName: agent?.name ?? '', agentRole: agent?.role ?? '' });
                        }}
                        style={{ width: '100%', padding: '8px 10px', borderRadius: 8, border: `1px solid ${t.border}`, background: t.surfaceHl, color: d.agentId ? t.text : t.textTert, fontSize: 13, outline: 'none' }}
                      >
                        <option value="">— Select agent —</option>
                        {agents.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
                      </select>
                    </div>

                    <div>
                      <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>Additional Instructions</label>
                      <textarea
                        value={d.instructionOverride}
                        onChange={e => updateSelectedData({ instructionOverride: e.target.value })}
                        rows={5}
                        placeholder="Extra instructions for this step…"
                        style={{ width: '100%', padding: '8px 10px', borderRadius: 8, border: `1px solid ${t.border}`, background: t.surfaceHl, color: t.text, fontSize: 12, resize: 'vertical', outline: 'none', boxSizing: 'border-box', fontFamily: 'inherit' }}
                      />
                    </div>

                    {availableSystemTools.length > 0 && (
                      <div>
                        <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>Additional Tools</label>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                          {availableSystemTools.map(toolId => {
                            const checked = d.systemTools.includes(toolId);
                            return (
                              <label
                                key={toolId}
                                style={{ display: 'flex', alignItems: 'flex-start', gap: 8, cursor: 'pointer', padding: '8px 10px', borderRadius: 8, border: `1px solid ${checked ? 'rgba(99,102,241,0.5)' : t.border}`, background: checked ? 'rgba(99,102,241,0.07)' : t.surfaceHl, transition: 'border-color .15s' }}
                              >
                                <input
                                  type="checkbox"
                                  checked={checked}
                                  onChange={() => {
                                    const next = checked
                                      ? d.systemTools.filter(tid => tid !== toolId)
                                      : [...d.systemTools, toolId];
                                    updateSelectedData({ systemTools: next });
                                  }}
                                  style={{ marginTop: 1, accentColor: INDIGO, flexShrink: 0 }}
                                />
                                <div style={{ minWidth: 0 }}>
                                  <p style={{ margin: 0, fontSize: 12, fontWeight: 600, color: t.textSec, fontFamily: 'monospace' }}>{toolId}</p>
                                  {toolId === 'switch_workflow_ticket' && (
                                    <p style={{ margin: '2px 0 0', fontSize: 11, color: t.textTert, lineHeight: 1.4 }}>Lets this agent redirect the rest of the workflow to a newly created external ticket</p>
                                  )}
                                </div>
                              </label>
                            );
                          })}
                        </div>
                      </div>
                    )}

                    <div style={{ padding: '12px 14px', borderRadius: 10, background: t.surfaceHl, border: `1px solid ${t.border}` }}>
                      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 10 }}>
                        <div style={{ flex: 1 }}>
                          <p style={{ margin: 0, fontSize: 13, fontWeight: 600, color: t.textSec }}>Pass previous output</p>
                          <p style={{ margin: '4px 0 0', fontSize: 11, color: t.textTert, lineHeight: 1.5 }}>Inject the previous step's response as context</p>
                        </div>
                        <button
                          type="button"
                          onClick={() => updateSelectedData({ passPreviousOutput: !d.passPreviousOutput })}
                          style={{ background: 'none', border: 'none', cursor: 'pointer', padding: 0, flexShrink: 0, color: d.passPreviousOutput ? t.primary : t.borderEl, display: 'flex', marginTop: 2, transition: 'color .15s' }}
                        >
                          {d.passPreviousOutput ? <ToggleRight size={26} /> : <ToggleLeft size={26} />}
                        </button>
                      </div>
                    </div>
                  </>
                );
              })()}

              {/* Condition config */}
              {selectedNode.type === 'condition' && (() => {
                const d = selectedNode.data as ConditionNodeData;
                return (
                  <div>
                    <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>Condition *</label>
                    <textarea
                      value={d.condition}
                      onChange={e => updateSelectedData({ condition: e.target.value })}
                      rows={4}
                      placeholder="e.g. The ticket was approved by a manager"
                      style={{ width: '100%', padding: '8px 10px', borderRadius: 8, border: `1px solid ${t.border}`, background: t.surfaceHl, color: t.text, fontSize: 12, resize: 'vertical', outline: 'none', boxSizing: 'border-box', fontFamily: 'inherit' }}
                    />
                    <p style={{ margin: '8px 0 0', fontSize: 11, color: t.textTert, lineHeight: 1.6 }}>
                      Natural-language condition evaluated by the workspace AI against the previous step's output.
                      Routes to <span style={{ color: EMERALD, fontWeight: 600 }}>True</span> or <span style={{ color: RED, fontWeight: 600 }}>False</span>.
                    </p>
                  </div>
                );
              })()}
            </div>
          </div>
        )}
      </div>

      {/* Delete workflow dialog */}
      {deleteConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="bg-surface border border-border w-full max-w-sm rounded-xl p-6 space-y-4">
            <div className="flex items-center gap-3 text-red-400">
              <AlertTriangle className="w-5 h-5" />
              <h3 className="text-lg font-bold text-text">Delete Workflow?</h3>
            </div>
            <p className="text-sm text-textMuted">This will permanently delete this workflow.</p>
            <div className="flex gap-3 pt-2">
              <button onClick={() => setDeleteConfirm(false)} disabled={isDeletingWf} className="flex-1 px-4 py-2 border border-border rounded-lg text-sm text-text hover:bg-surfaceHighlight transition-colors">Cancel</button>
              <button onClick={handleDeleteWf} disabled={isDeletingWf} className="flex-1 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-lg text-sm font-medium flex items-center justify-center gap-2">
                {isDeletingWf ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

// ── WorkflowBuilder (list view + editor host) ─────────────────────────────────

const WorkflowBuilder: React.FC<WorkflowBuilderProps> = ({ workspaceId }) => {
  const [view, setView] = useState<'list' | 'editor'>('list');
  const [workflows, setWorkflows] = useState<WorkflowDefinition[]>([]);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [availableSystemTools, setAvailableSystemTools] = useState<string[]>([]);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  // Editor init
  const [editingId, setEditingId]               = useState<string | null>(null);
  const [initName, setInitName]                 = useState('');
  const [initDescription, setInitDescription]   = useState('');
  const [initNodes, setInitNodes]               = useState<Node[]>([]);
  const [initEdges, setInitEdges]               = useState<Edge[]>([]);

  useEffect(() => { injectCanvasStyles(); }, []);

  useEffect(() => {
    if (!workspaceId) return;
    setIsLoading(true);
    Promise.all([getWorkflowDefinitions(workspaceId), getAgents(workspaceId), getWorkflowSystemTools()])
      .then(([wfs, ags, tools]) => { setWorkflows(wfs); setAgents(ags); setAvailableSystemTools(tools); })
      .catch(console.error)
      .finally(() => setIsLoading(false));
    setView('list');
  }, [workspaceId]);

  const buildGraph = useCallback((wf: WorkflowDefinition | null) => {
    const wfId = wf?.id ?? 'new';
    const saved = loadPositions(wfId);
    const agentMap = new Map(agents.map(a => [a.id, a]));

    if (!wf || wf.steps.length === 0) {
      return {
        nodes: [
          { id: 'start', type: 'startNode', position: saved['start'] ?? { x: PILL_X_OFF, y: 0 }, data: {}, selectable: false },
          { id: 'end-default', type: 'endNode', position: saved['end-default'] ?? { x: PILL_X_OFF, y: NODE_Y_GAP }, data: {}, selectable: false },
        ] as Node[],
        edges: [makeEdge('e-start-end', 'start', 'end-default')] as Edge[],
      };
    }

    const nodes: Node[] = [
      { id: 'start', type: 'startNode', position: saved['start'] ?? { x: PILL_X_OFF, y: 0 }, data: {}, selectable: false },
    ];
    const edges: Edge[] = [];
    const stepIdSet = new Set(wf.steps.map(s => s.id));

    wf.steps.forEach((step, i) => {
      const nodeId = `step-${step.id}`;
      if (step.stepType === 'Condition') {
        nodes.push({
          id: nodeId,
          type: 'condition',
          position: saved[nodeId] ?? { x: 0, y: 120 + i * NODE_Y_GAP },
          data: { condition: step.condition ?? '' } as ConditionNodeData,
        });
      } else {
        nodes.push({
          id: nodeId,
          type: 'agentStep',
          position: saved[nodeId] ?? { x: 0, y: 120 + i * NODE_Y_GAP },
          data: {
            agentId: step.agentId,
            agentName: agentMap.get(step.agentId!)?.name ?? step.agentName ?? '',
            agentRole: agentMap.get(step.agentId!)?.role ?? '',
            instructionOverride: step.instructionOverride ?? '',
            passPreviousOutput: step.passPreviousOutput,
            systemTools: step.systemTools ?? [],
          } as AgentStepNodeData,
        });
      }
    });

    const sortedSteps = [...wf.steps].sort((a, b) => a.order - b.order);
    const baseY = 120 + wf.steps.length * NODE_Y_GAP;

    // Legacy workflows (pre-condition-support) have all trueNextStepId = null.
    // Detect them and fall back to a simple order-based linear chain.
    const isLegacyLinear = wf.steps.every(
      s => !s.trueNextStepId && !s.falseNextStepId && s.stepType !== 'Condition'
    );

    if (isLegacyLinear) {
      const endId = 'end-default';
      nodes.push({ id: endId, type: 'endNode', position: saved[endId] ?? { x: PILL_X_OFF, y: baseY }, data: {}, selectable: false });
      sortedSteps.forEach((step, i) => {
        const cur = `step-${step.id}`;
        const prev = i === 0 ? 'start' : `step-${sortedSteps[i - 1].id}`;
        edges.push(makeEdge(`e-${prev}-${step.id}`, prev, cur));
      });
      const lastId = `step-${sortedSteps[sortedSteps.length - 1].id}`;
      edges.push(makeEdge(`e-${lastId}-end`, lastId, endId));
      return { nodes, edges };
    }

    // Topology-aware path: use trueNextStepId / falseNextStepId.
    edges.push(makeEdge(`e-start-${sortedSteps[0].id}`, 'start', `step-${sortedSteps[0].id}`));

    wf.steps.forEach((step, i) => {
      const nodeId = `step-${step.id}`;
      if (step.stepType === 'Condition') {
        const trueResolved = step.trueNextStepId && stepIdSet.has(step.trueNextStepId);
        const falseResolved = step.falseNextStepId && stepIdSet.has(step.falseNextStepId);

        const trueTarget = trueResolved ? `step-${step.trueNextStepId}` : `end-${step.id}-true`;
        const falseTarget = falseResolved ? `step-${step.falseNextStepId}` : `end-${step.id}-false`;

        if (!trueResolved) {
          const eid = `end-${step.id}-true`;
          nodes.push({ id: eid, type: 'endNode', position: saved[eid] ?? { x: i * 60, y: baseY }, data: {}, selectable: false });
        }
        if (!falseResolved) {
          const eid = `end-${step.id}-false`;
          nodes.push({ id: eid, type: 'endNode', position: saved[eid] ?? { x: i * 60 - 80, y: baseY }, data: {}, selectable: false });
        }

        edges.push(makeEdge(`e-${step.id}-true`, nodeId, trueTarget, 'true'));
        edges.push(makeEdge(`e-${step.id}-false`, nodeId, falseTarget, 'false'));
      } else {
        const nextResolved = step.trueNextStepId && stepIdSet.has(step.trueNextStepId);
        const nextTarget = nextResolved ? `step-${step.trueNextStepId}` : `end-${step.id}`;

        if (!nextResolved) {
          const eid = `end-${step.id}`;
          nodes.push({ id: eid, type: 'endNode', position: saved[eid] ?? { x: PILL_X_OFF, y: baseY }, data: {}, selectable: false });
        }

        edges.push(makeEdge(`e-${step.id}-next`, nodeId, nextTarget));
      }
    });

    return { nodes, edges };
  }, [agents]);

  const openCreate = () => {
    setEditingId(null);
    setInitName('');
    setInitDescription('');
    const { nodes, edges } = buildGraph(null);
    setInitNodes(nodes);
    setInitEdges(edges);
    setView('editor');
  };

  const openEdit = (wf: WorkflowDefinition) => {
    setEditingId(wf.id);
    setInitName(wf.name);
    setInitDescription(wf.description ?? '');
    const { nodes, edges } = buildGraph(wf);
    setInitNodes(nodes);
    setInitEdges(edges);
    setView('editor');
  };

  const confirmDelete = async () => {
    if (!deleteId) return;
    setIsDeleting(true);
    try {
      await deleteWorkflowDefinition(deleteId);
      setWorkflows(prev => prev.filter(w => w.id !== deleteId));
    } catch (e) { console.error(e); }
    finally { setDeleteId(null); setIsDeleting(false); }
  };

  if (isLoading) {
    return <div className="flex-1 flex items-center justify-center"><Loader2 className="w-8 h-8 animate-spin text-primary" /></div>;
  }

  if (view === 'editor') {
    return (
      <ReactFlowProvider>
        <WorkflowEditorInner
          workspaceId={workspaceId}
          editingId={editingId}
          initialName={initName}
          initialDescription={initDescription}
          initialNodes={initNodes}
          initialEdges={initEdges}
          agents={agents}
          availableSystemTools={availableSystemTools}
          onSaved={wf => {
            setWorkflows(prev => editingId ? prev.map(w => w.id === editingId ? wf : w) : [...prev, wf]);
            setView('list');
          }}
          onBack={() => setView('list')}
          onDelete={id => setWorkflows(prev => prev.filter(w => w.id !== id))}
        />
      </ReactFlowProvider>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-text">Workflows</h2>
          <p className="text-textMuted text-sm mt-1">Define agent pipelines and assign them to tickets.</p>
        </div>
        <button
          onClick={openCreate}
          className="bg-primary hover:bg-primaryHover text-white px-4 py-2 rounded-lg flex items-center gap-2 text-sm font-medium transition-colors"
        >
          <Plus className="w-4 h-4" /> New Workflow
        </button>
      </div>

      {workflows.length === 0 ? (
        <div className="flex flex-col items-center justify-center border-2 border-dashed border-border rounded-lg p-12 text-center">
          <p className="text-lg font-medium text-text mb-2">No workflows yet</p>
          <p className="text-sm text-textMuted mb-6">Create an agent pipeline to automate multi-step work on tickets.</p>
          <button onClick={openCreate} className="text-primary hover:underline text-sm font-medium">Create your first workflow</button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {workflows.map(wf => (
            <div
              key={wf.id}
              onClick={() => openEdit(wf)}
              className="bg-surface border border-border rounded-lg p-5 cursor-pointer hover:border-primary/50 hover:shadow-md transition-all group"
            >
              <div className="flex items-start justify-between mb-3">
                <h3 className="font-semibold text-text group-hover:text-primary transition-colors truncate pr-2">{wf.name}</h3>
                <button
                  onClick={e => { e.stopPropagation(); setDeleteId(wf.id); }}
                  className="p-1.5 text-textMuted hover:text-red-400 rounded opacity-0 group-hover:opacity-100 transition-all shrink-0"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              </div>
              {wf.description && <p className="text-xs text-textMuted mb-3 line-clamp-2">{wf.description}</p>}
              <p className="text-xs text-textMuted">{wf.steps.length} step{wf.steps.length !== 1 ? 's' : ''}</p>
              {wf.steps.length > 0 && (
                <div className="mt-3 space-y-1">
                  {wf.steps.slice(0, 3).map((s, i) => (
                    <div key={s.id} className="flex items-center gap-2 text-xs text-textMuted">
                      <span className="text-primary font-mono">{i + 1}.</span>
                      <span className="truncate">{s.agentName}</span>
                      {s.passPreviousOutput && <span className="text-primary/60 shrink-0">↻</span>}
                    </div>
                  ))}
                  {wf.steps.length > 3 && <p className="text-xs text-textMuted pl-4">+{wf.steps.length - 3} more</p>}
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {deleteId && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="bg-surface border border-border w-full max-w-sm rounded-xl p-6 space-y-4">
            <div className="flex items-center gap-3 text-red-400">
              <AlertTriangle className="w-5 h-5" />
              <h3 className="text-lg font-bold text-text">Delete Workflow?</h3>
            </div>
            <p className="text-sm text-textMuted">
              This will permanently delete{' '}
              <span className="font-semibold text-text">{workflows.find(w => w.id === deleteId)?.name ?? 'this workflow'}</span>.
            </p>
            <div className="flex gap-3 pt-2">
              <button onClick={() => setDeleteId(null)} disabled={isDeleting} className="flex-1 px-4 py-2 border border-border rounded-lg text-sm text-text hover:bg-surfaceHighlight transition-colors">Cancel</button>
              <button onClick={confirmDelete} disabled={isDeleting} className="flex-1 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-lg text-sm font-medium flex items-center justify-center gap-2">
                {isDeleting ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default WorkflowBuilder;
