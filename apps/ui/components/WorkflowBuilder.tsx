import React, { useCallback, useEffect, useRef, useState } from 'react';
import ReactFlow, {
  Background,
  BackgroundVariant,
  Controls,
  Edge,
  Handle,
  MiniMap,
  Node,
  NodeProps,
  Position,
  ReactFlowInstance,
  useEdgesState,
  useNodesState,
} from 'reactflow';
import 'reactflow/dist/style.css';
import {
  Plus, Trash2, Loader2, Save, ArrowLeft, AlertTriangle,
  ToggleLeft, ToggleRight, Bot, X, Search,
} from 'lucide-react';
import { WorkflowDefinition, Agent } from '../types';
import {
  getWorkflowDefinitions,
  createWorkflowDefinition,
  updateWorkflowDefinition,
  deleteWorkflowDefinition,
  CreateWorkflowStepPayload,
} from '../services/workflowService';
import { getAgents } from '../services/agentService';

// ── Theme-aware color helpers ─────────────────────────────────────────────────

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

// Fixed semantic colors (same in both themes)
const EMERALD = '#10b981';
const RED     = '#ef4444';
const INDIGO  = '#6366f1';

// ── Interfaces ────────────────────────────────────────────────────────────────

interface WorkflowBuilderProps { workspaceId: string; }

interface StepDraft {
  id: string;
  agentId: string;
  instructionOverride: string;
  passPreviousOutput: boolean;
}

interface AgentStepNodeData {
  stepIndex: number;
  agentName: string;
  agentRole: string;
  isSelected: boolean;
  passPreviousOutput: boolean;
  hasInstructionOverride: boolean;
  onDelete: () => void;
}

// ── Constants ─────────────────────────────────────────────────────────────────

const STEP_NODE_WIDTH  = 220;
const PILL_NODE_WIDTH  = 160;
const STEP_Y_GAP       = 130;
const FIRST_STEP_Y     = 110;
const END_EXTRA_Y      = 80;
const PILL_OFFSET_X    = (STEP_NODE_WIDTH - PILL_NODE_WIDTH) / 2;

const HANDLE_STYLE = {
  width: 8, height: 8,
  background: t.surface,
  border: '2px solid rgba(148,163,184,0.7)',
  borderRadius: '50%',
};

const emptyStep = (): StepDraft => ({
  id: crypto.randomUUID(),
  agentId: '',
  instructionOverride: '',
  passPreviousOutput: false,
});

// ── Custom Nodes ──────────────────────────────────────────────────────────────

const StartNode: React.FC<NodeProps> = () => (
  <div style={{
    width: PILL_NODE_WIDTH, padding: '10px 16px',
    background: t.surface,
    border: `2px solid ${EMERALD}`,
    borderRadius: 50,
    textAlign: 'center',
    boxShadow: `0 0 12px rgba(16,185,129,0.2)`,
  }}>
    <span style={{ fontSize: 15, fontWeight: 700, color: t.text, letterSpacing: '0.02em' }}>
      Start
    </span>
    <Handle
      type="source"
      position={Position.Bottom}
      style={{ ...HANDLE_STYLE, bottom: -5, border: `2px solid ${EMERALD}` }}
    />
  </div>
);

const EndNode: React.FC<NodeProps> = () => (
  <div style={{
    width: PILL_NODE_WIDTH, padding: '10px 16px',
    background: t.surface,
    border: `2px solid ${RED}`,
    borderRadius: 50,
    textAlign: 'center',
    boxShadow: `0 0 12px rgba(239,68,68,0.2)`,
  }}>
    <Handle
      type="target"
      position={Position.Top}
      style={{ ...HANDLE_STYLE, top: -5, border: `2px solid ${RED}` }}
    />
    <span style={{ fontSize: 15, fontWeight: 700, color: t.text, letterSpacing: '0.02em' }}>
      End
    </span>
  </div>
);

const AgentStepNode: React.FC<NodeProps<AgentStepNodeData>> = ({ data, selected }) => {
  const [hovered, setHovered] = useState(false);
  const active = selected || data.isSelected;
  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        width: STEP_NODE_WIDTH,
        background: t.surface,
        border: `2px solid ${active ? INDIGO : 'rgba(99,102,241,0.55)'}`,
        borderRadius: 8,
        padding: '12px 14px',
        textAlign: 'center',
        cursor: 'pointer',
        position: 'relative',
        boxShadow: active
          ? `0 0 0 3px rgba(99,102,241,0.2), 0 4px 16px rgba(0,0,0,0.15)`
          : '0 2px 8px rgba(0,0,0,0.1)',
        transition: 'border-color 0.15s, box-shadow 0.15s',
      }}
    >
      <Handle
        type="target"
        position={Position.Top}
        style={{ ...HANDLE_STYLE, top: -5 }}
      />

      {/* Delete button */}
      <button
        onClick={e => { e.stopPropagation(); data.onDelete(); }}
        style={{
          position: 'absolute', top: 6, right: 6,
          padding: 3, borderRadius: 5, border: 'none', background: 'transparent',
          cursor: 'pointer', color: t.textTert, display: hovered ? 'flex' : 'none',
          transition: 'color 0.1s',
        }}
        onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.color = RED; }}
        onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.color = t.textTert; }}
        title="Remove step"
      >
        <X size={12} />
      </button>

      <p style={{
        margin: 0, fontSize: 13, fontWeight: 600,
        color: active ? t.text : t.textSec,
        lineHeight: 1.4,
      }}>
        {data.agentName
          ? data.agentName
          : <span style={{ color: t.textTert, fontStyle: 'italic', fontWeight: 400 }}>No agent selected</span>}
      </p>

      {data.agentRole && (
        <p style={{ margin: '3px 0 0', fontSize: 11, color: t.textTert, lineHeight: 1.3 }}>
          {data.agentRole}
        </p>
      )}

      {(data.passPreviousOutput || data.hasInstructionOverride) && (
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 4, marginTop: 6, flexWrap: 'wrap' }}>
          {data.passPreviousOutput && (
            <span style={{
              fontSize: 9, fontWeight: 700, color: t.purple,
              background: 'rgba(168,85,247,0.1)', padding: '1px 6px', borderRadius: 20,
            }}>↻ chain</span>
          )}
          {data.hasInstructionOverride && (
            <span style={{
              fontSize: 9, color: t.yellow,
              background: 'rgba(234,179,8,0.1)', padding: '1px 6px', borderRadius: 20,
            }}>+ notes</span>
          )}
        </div>
      )}

      <Handle
        type="source"
        position={Position.Bottom}
        style={{ ...HANDLE_STYLE, bottom: -5 }}
      />
    </div>
  );
};

const nodeTypes = { startNode: StartNode, endNode: EndNode, agentStep: AgentStepNode };

// ── WorkflowBuilder ───────────────────────────────────────────────────────────

const WorkflowBuilder: React.FC<WorkflowBuilderProps> = ({ workspaceId }) => {
  const [view, setView] = useState<'list' | 'editor'>('list');
  const [workflows, setWorkflows] = useState<WorkflowDefinition[]>([]);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Editor state
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [steps, setSteps] = useState<StepDraft[]>([emptyStep()]);
  const [selectedStepIndex, setSelectedStepIndex] = useState<number | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [agentSearch, setAgentSearch] = useState('');

  // Delete state
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  // ReactFlow state
  const [rfNodes, setRfNodes, onNodesChange] = useNodesState([]);
  const [rfEdges, setRfEdges, onEdgesChange] = useEdgesState([]);

  // Canvas refs
  const nodePositionsRef = useRef<Map<string, { x: number; y: number }>>(new Map());
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const [rfInstance, setRfInstance] = useState<ReactFlowInstance | null>(null);

  // ── Data fetching ──────────────────────────────────────────────────────────

  useEffect(() => {
    if (!workspaceId) return;
    setIsLoading(true);
    Promise.all([getWorkflowDefinitions(workspaceId), getAgents(workspaceId)])
      .then(([wfs, ags]) => { setWorkflows(wfs); setAgents(ags); })
      .catch(console.error)
      .finally(() => setIsLoading(false));
    setView('list');
  }, [workspaceId]);

  // ── ReactFlow sync ─────────────────────────────────────────────────────────

  useEffect(() => {
    if (view !== 'editor') return;

    const agentMap = new Map(agents.map(a => [a.id, a]));
    const endY = FIRST_STEP_Y + steps.length * STEP_Y_GAP + END_EXTRA_Y;

    const startPos = nodePositionsRef.current.get('start') ?? { x: PILL_OFFSET_X, y: 0 };
    const endPos   = nodePositionsRef.current.get('end')   ?? { x: PILL_OFFSET_X, y: endY };

    const stepNodes: Node[] = steps.map((step, i) => {
      const nodeId = `step-${step.id}`;
      const defaultPos = { x: 0, y: FIRST_STEP_Y + i * STEP_Y_GAP };
      const position = nodePositionsRef.current.get(nodeId) ?? defaultPos;
      const agent = agentMap.get(step.agentId);
      return {
        id: nodeId,
        type: 'agentStep',
        position,
        data: {
          stepIndex: i,
          agentName: agent?.name ?? '',
          agentRole: agent?.role ?? '',
          isSelected: selectedStepIndex === i,
          passPreviousOutput: step.passPreviousOutput,
          hasInstructionOverride: step.instructionOverride.trim().length > 0,
          onDelete: () => {
            nodePositionsRef.current.delete(nodeId);
            setSteps(prev => prev.filter((_, idx) => idx !== i));
            setSelectedStepIndex(null);
          },
        } as AgentStepNodeData,
        draggable: true,
        selectable: true,
      };
    });

    const newNodes: Node[] = [
      { id: 'start', type: 'startNode', position: startPos, data: {}, draggable: true, selectable: false },
      ...stepNodes,
      { id: 'end', type: 'endNode', position: endPos, data: {}, draggable: true, selectable: false },
    ];

    const edgeStyle = { stroke: 'rgba(148,163,184,0.5)', strokeWidth: 1.5 };
    const newEdges: Edge[] = [];

    if (steps.length === 0) {
      newEdges.push({ id: 'e-start-end', source: 'start', target: 'end', type: 'smoothstep', style: edgeStyle });
    } else {
      newEdges.push({ id: 'e-start', source: 'start', target: `step-${steps[0].id}`, type: 'smoothstep', style: edgeStyle });
      steps.slice(0, -1).forEach((step, i) => {
        newEdges.push({ id: `e-${step.id}`, source: `step-${step.id}`, target: `step-${steps[i + 1].id}`, type: 'smoothstep', style: edgeStyle });
      });
      newEdges.push({ id: 'e-end', source: `step-${steps[steps.length - 1].id}`, target: 'end', type: 'smoothstep', style: edgeStyle });
    }

    setRfNodes(newNodes);
    setRfEdges(newEdges);
  }, [steps, selectedStepIndex, agents, view]);

  // ── Handlers ──────────────────────────────────────────────────────────────

  const openCreate = () => {
    nodePositionsRef.current.clear();
    setEditingId(null);
    setName('');
    setDescription('');
    setSteps([emptyStep()]);
    setSelectedStepIndex(0);
    setSaveError(null);
    setAgentSearch('');
    setView('editor');
  };

  const openEdit = (wf: WorkflowDefinition) => {
    nodePositionsRef.current.clear();
    setEditingId(wf.id);
    setName(wf.name);
    setDescription(wf.description ?? '');
    setSteps(
      wf.steps.length > 0
        ? wf.steps.map(s => ({
            id: crypto.randomUUID(),
            agentId: s.agentId,
            instructionOverride: s.instructionOverride ?? '',
            passPreviousOutput: s.passPreviousOutput,
          }))
        : [emptyStep()]
    );
    setSelectedStepIndex(null);
    setSaveError(null);
    setAgentSearch('');
    setView('editor');
  };

  const updateStep = useCallback((index: number, patch: Partial<StepDraft>) =>
    setSteps(prev => prev.map((s, i) => i === index ? { ...s, ...patch } : s)),
  []);

  const addAgentStep = (agent: Agent, position?: { x: number; y: number }) => {
    const newStep = emptyStep();
    newStep.agentId = agent.id;
    const newIndex = steps.length;
    if (position) nodePositionsRef.current.set(`step-${newStep.id}`, position);
    setSteps(prev => [...prev, newStep]);
    setSelectedStepIndex(newIndex);
  };

  const onNodeDragStop = useCallback((_: React.MouseEvent, node: Node) => {
    nodePositionsRef.current.set(node.id, node.position);
  }, []);

  const handleCanvasDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    const agentId = e.dataTransfer.getData('application/agentId');
    if (!agentId || !rfInstance || !reactFlowWrapper.current) return;
    const bounds = reactFlowWrapper.current.getBoundingClientRect();
    const position = rfInstance.screenToFlowPosition({ x: e.clientX - bounds.left, y: e.clientY - bounds.top });
    const agent = agents.find(a => a.id === agentId);
    if (agent) addAgentStep(agent, position);
  };

  const handleSave = async () => {
    if (!name.trim()) { setSaveError('Workflow name is required.'); return; }
    if (steps.some(s => !s.agentId)) { setSaveError('Each step must have an agent selected.'); return; }
    setSaveError(null);
    setIsSaving(true);
    try {
      const payload = {
        name: name.trim(),
        description: description.trim() || null,
        steps: steps.map((s, i): CreateWorkflowStepPayload => ({
          order: i,
          agentId: s.agentId,
          instructionOverride: s.instructionOverride.trim() || null,
          passPreviousOutput: s.passPreviousOutput,
        })),
      };
      if (editingId) {
        const updated = await updateWorkflowDefinition(editingId, payload);
        setWorkflows(prev => prev.map(w => w.id === editingId ? updated : w));
      } else {
        const created = await createWorkflowDefinition({ workspaceId, ...payload });
        setWorkflows(prev => [...prev, created]);
      }
      setView('list');
    } catch (e: any) {
      setSaveError(e?.message ?? 'Failed to save workflow.');
    } finally {
      setIsSaving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleteId) return;
    setIsDeleting(true);
    try {
      await deleteWorkflowDefinition(deleteId);
      setWorkflows(prev => prev.filter(w => w.id !== deleteId));
      if (editingId === deleteId) setView('list');
    } catch (e) { console.error(e); }
    finally { setDeleteId(null); setIsDeleting(false); }
  };

  const filteredAgents = agents.filter(a =>
    a.name.toLowerCase().includes(agentSearch.toLowerCase()) ||
    a.role.toLowerCase().includes(agentSearch.toLowerCase())
  );

  const selectedStep = selectedStepIndex !== null ? steps[selectedStepIndex] : null;

  // ── Render: loading ────────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary" />
      </div>
    );
  }

  // ── Render: editor (full-screen overlay) ──────────────────────────────────

  if (view === 'editor') {
    return (
      <div style={{
        position: 'absolute', inset: 0, zIndex: 10,
        display: 'flex', flexDirection: 'column',
        background: t.bg,
      }}>
        {/* Top toolbar */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 10, padding: '10px 16px',
          background: t.surface, borderBottom: `1px solid ${t.border}`, flexShrink: 0,
          minHeight: 52,
        }}>
          <button
            onClick={() => setView('list')}
            style={{
              padding: 6, borderRadius: 8, border: 'none', background: 'transparent',
              cursor: 'pointer', color: t.textSec, display: 'flex', flexShrink: 0,
            }}
            onMouseEnter={e => { const b = e.currentTarget as HTMLButtonElement; b.style.background = t.surfaceHl; b.style.color = t.text; }}
            onMouseLeave={e => { const b = e.currentTarget as HTMLButtonElement; b.style.background = 'transparent'; b.style.color = t.textSec; }}
          >
            <ArrowLeft size={18} />
          </button>

          <div style={{ display: 'flex', flexDirection: 'column', flex: 1, minWidth: 0 }}>
            <input
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="Workflow name…"
              style={{
                background: 'transparent', border: 'none', outline: 'none',
                fontSize: 15, fontWeight: 700, color: t.text, width: '100%',
              }}
            />
            {description && (
              <span style={{ fontSize: 11, color: t.textTert, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {description}
              </span>
            )}
          </div>

          {saveError && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 5, color: t.red, fontSize: 12, flexShrink: 0 }}>
              <AlertTriangle size={13} /><span>{saveError}</span>
            </div>
          )}

          {editingId && (
            <button
              onClick={() => setDeleteId(editingId)}
              style={{
                padding: '6px 12px', borderRadius: 8,
                border: `1px solid rgba(239,68,68,0.3)`, background: 'transparent',
                cursor: 'pointer', color: t.red, fontSize: 12, flexShrink: 0,
              }}
            >
              Delete
            </button>
          )}

          <button
            onClick={handleSave}
            disabled={isSaving}
            style={{
              display: 'flex', alignItems: 'center', gap: 6,
              padding: '7px 16px', borderRadius: 8, border: 'none',
              background: t.primary, color: '#ffffff',
              cursor: isSaving ? 'not-allowed' : 'pointer',
              fontSize: 13, fontWeight: 600, flexShrink: 0,
              opacity: isSaving ? 0.7 : 1,
            }}
          >
            {isSaving ? <Loader2 size={14} className="animate-spin" /> : <Save size={14} />}
            Save
          </button>
        </div>

        {/* Main area */}
        <div style={{ display: 'flex', flex: 1, overflow: 'hidden' }}>

          {/* Left panel: agent library */}
          <div style={{
            width: 220, background: t.surface, borderRight: `1px solid ${t.border}`,
            display: 'flex', flexDirection: 'column', overflow: 'hidden', flexShrink: 0,
          }}>
            <div style={{ padding: '12px 14px', borderBottom: `1px solid ${t.border}` }}>
              <p style={{ margin: '0 0 8px', fontSize: 10, fontWeight: 700, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.08em' }}>
                Agents
              </p>
              <div style={{ position: 'relative' }}>
                <Search size={13} style={{ position: 'absolute', left: 9, top: 8, color: t.textTert, pointerEvents: 'none' }} />
                <input
                  type="text"
                  value={agentSearch}
                  onChange={e => setAgentSearch(e.target.value)}
                  placeholder="Search…"
                  style={{
                    width: '100%', padding: '6px 10px 6px 28px', borderRadius: 8,
                    border: `1px solid ${t.border}`, background: t.surfaceHl,
                    color: t.text, fontSize: 12, outline: 'none', boxSizing: 'border-box',
                  }}
                />
              </div>
            </div>
            <div style={{ flex: 1, overflowY: 'auto', padding: '8px 6px' }}>
              {filteredAgents.length === 0 && (
                <p style={{ fontSize: 12, color: t.textTert, textAlign: 'center', padding: 16, fontStyle: 'italic' }}>
                  {agentSearch ? 'No matches' : 'No agents'}
                </p>
              )}
              {filteredAgents.map(agent => (
                <button
                  key={agent.id}
                  draggable
                  onDragStart={e => { e.dataTransfer.setData('application/agentId', agent.id); e.dataTransfer.effectAllowed = 'copy'; }}
                  onClick={() => addAgentStep(agent)}
                  title={`Drag or click to add ${agent.name}`}
                  style={{
                    width: '100%', display: 'flex', alignItems: 'center', gap: 9,
                    padding: '7px 8px', borderRadius: 8, border: 'none', background: 'transparent',
                    cursor: 'grab', textAlign: 'left', marginBottom: 2,
                  }}
                  onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.background = t.surfaceHl; }}
                  onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.background = 'transparent'; }}
                >
                  <div style={{
                    width: 28, height: 28, borderRadius: 7,
                    background: 'rgba(99,102,241,0.1)', flexShrink: 0,
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                  }}>
                    <Bot size={14} color={INDIGO} />
                  </div>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <p style={{ margin: 0, fontSize: 12, fontWeight: 600, color: t.textSec, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {agent.name}
                    </p>
                    <p style={{ margin: 0, fontSize: 10, color: t.textTert, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {agent.role}
                    </p>
                  </div>
                  <Plus size={12} color={t.textTert} style={{ flexShrink: 0 }} />
                </button>
              ))}
            </div>
            {/* Description field */}
            <div style={{ padding: '10px 14px', borderTop: `1px solid ${t.border}` }}>
              <p style={{ margin: '0 0 5px', fontSize: 10, fontWeight: 700, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.08em' }}>
                Description
              </p>
              <textarea
                value={description}
                onChange={e => setDescription(e.target.value)}
                rows={2}
                placeholder="Optional description…"
                style={{
                  width: '100%', padding: '6px 8px', borderRadius: 6,
                  border: `1px solid ${t.border}`, background: t.surfaceHl,
                  color: t.textSec, fontSize: 11, resize: 'none', outline: 'none',
                  boxSizing: 'border-box', fontFamily: 'inherit',
                }}
              />
            </div>
          </div>

          {/* Canvas */}
          <div
            ref={reactFlowWrapper}
            onDrop={handleCanvasDrop}
            onDragOver={e => { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; }}
            style={{ flex: 1, position: 'relative', overflow: 'hidden', background: t.bg }}
          >
            <ReactFlow
              nodes={rfNodes}
              edges={rfEdges}
              onNodesChange={onNodesChange}
              onEdgesChange={onEdgesChange}
              onNodeClick={(_, node) => {
                if (node.type === 'agentStep') {
                  setSelectedStepIndex((node.data as AgentStepNodeData).stepIndex);
                }
              }}
              onPaneClick={() => setSelectedStepIndex(null)}
              onNodeDragStop={onNodeDragStop}
              onInit={setRfInstance}
              nodeTypes={nodeTypes}
              nodesDraggable={true}
              nodesConnectable={false}
              elementsSelectable={true}
              fitView
              fitViewOptions={{ padding: 0.4, maxZoom: 1 }}
              proOptions={{ hideAttribution: true }}
              minZoom={0.2}
              maxZoom={2}
            >
              <Background
                variant={BackgroundVariant.Dots}
                gap={28}
                size={1.2}
                color="rgba(148,163,184,0.15)"
              />
              <Controls
                style={{
                  background: t.surface, border: `1px solid ${t.border}`,
                  borderRadius: 8, overflow: 'hidden',
                }}
              />
              <MiniMap
                style={{ background: t.surface, border: `1px solid ${t.border}`, borderRadius: 8 }}
                nodeColor={() => INDIGO}
                maskColor="rgba(148,163,184,0.08)"
              />
            </ReactFlow>
          </div>

          {/* Right panel: step config */}
          {selectedStep && (
            <div style={{
              width: 288, background: t.surface, borderLeft: `1px solid ${t.border}`,
              display: 'flex', flexDirection: 'column', overflow: 'hidden', flexShrink: 0,
            }}>
              <div style={{
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                padding: '12px 16px', borderBottom: `1px solid ${t.border}`,
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{
                    fontSize: 10, fontWeight: 700, color: t.primary,
                    background: 'rgba(99,102,241,0.12)', padding: '2px 8px', borderRadius: 20,
                  }}>
                    Step {selectedStepIndex! + 1}
                  </span>
                  <span style={{ fontSize: 12, fontWeight: 600, color: t.textSec }}>Config</span>
                </div>
                <button
                  onClick={() => setSelectedStepIndex(null)}
                  style={{ padding: 4, borderRadius: 6, border: 'none', background: 'transparent', cursor: 'pointer', color: t.textTert, display: 'flex' }}
                  onMouseEnter={e => { const b = e.currentTarget as HTMLButtonElement; b.style.color = t.text; b.style.background = t.surfaceHl; }}
                  onMouseLeave={e => { const b = e.currentTarget as HTMLButtonElement; b.style.color = t.textTert; b.style.background = 'transparent'; }}
                >
                  <X size={15} />
                </button>
              </div>

              <div style={{ flex: 1, overflowY: 'auto', padding: 16, display: 'flex', flexDirection: 'column', gap: 16 }}>
                <div>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>
                    Agent *
                  </label>
                  <select
                    value={selectedStep.agentId}
                    onChange={e => updateStep(selectedStepIndex!, { agentId: e.target.value })}
                    style={{
                      width: '100%', padding: '8px 10px', borderRadius: 8,
                      border: `1px solid ${t.border}`, background: t.surfaceHl,
                      color: selectedStep.agentId ? t.text : t.textTert,
                      fontSize: 13, outline: 'none',
                    }}
                  >
                    <option value="">— Select agent —</option>
                    {agents.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: t.textSec, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>
                    Additional Instructions
                  </label>
                  <textarea
                    value={selectedStep.instructionOverride}
                    onChange={e => updateStep(selectedStepIndex!, { instructionOverride: e.target.value })}
                    rows={5}
                    placeholder="Extra instructions for this step…"
                    style={{
                      width: '100%', padding: '8px 10px', borderRadius: 8,
                      border: `1px solid ${t.border}`, background: t.surfaceHl,
                      color: t.text, fontSize: 12, resize: 'vertical', outline: 'none',
                      boxSizing: 'border-box', fontFamily: 'inherit',
                    }}
                  />
                </div>

                {selectedStepIndex! > 0 && (
                  <div style={{ padding: '12px 14px', borderRadius: 10, background: t.surfaceHl, border: `1px solid ${t.border}` }}>
                    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 10 }}>
                      <div style={{ flex: 1 }}>
                        <p style={{ margin: 0, fontSize: 13, fontWeight: 600, color: t.textSec }}>Pass previous output</p>
                        <p style={{ margin: '4px 0 0', fontSize: 11, color: t.textTert, lineHeight: 1.5 }}>
                          Inject the previous step's response as context
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => updateStep(selectedStepIndex!, { passPreviousOutput: !selectedStep.passPreviousOutput })}
                        style={{
                          background: 'none', border: 'none', cursor: 'pointer', padding: 0, flexShrink: 0,
                          color: selectedStep.passPreviousOutput ? t.primary : t.borderEl,
                          display: 'flex', marginTop: 2, transition: 'color 0.15s',
                        }}
                      >
                        {selectedStep.passPreviousOutput ? <ToggleRight size={26} /> : <ToggleLeft size={26} />}
                      </button>
                    </div>
                  </div>
                )}

                {steps.length > 1 && (
                  <button
                    onClick={() => {
                      const step = steps[selectedStepIndex!];
                      nodePositionsRef.current.delete(`step-${step.id}`);
                      setSteps(prev => prev.filter((_, i) => i !== selectedStepIndex!));
                      setSelectedStepIndex(null);
                    }}
                    style={{
                      width: '100%', padding: '8px 16px', borderRadius: 8,
                      border: `1px solid rgba(239,68,68,0.25)`, background: 'transparent',
                      color: t.red, fontSize: 13, cursor: 'pointer',
                      display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
                    }}
                    onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.background = 'rgba(239,68,68,0.07)'; }}
                    onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.background = 'transparent'; }}
                  >
                    <Trash2 size={14} /> Remove Step
                  </button>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    );
  }

  // ── Render: list ───────────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold text-text">Workflows</h2>
          <p className="text-textMuted text-sm mt-1">Define sequential agent pipelines and assign them to tickets.</p>
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
          <p className="text-sm text-textMuted mb-6">Create a sequential agent pipeline to automate multi-step work on tickets.</p>
          <button onClick={openCreate} className="text-primary hover:underline text-sm font-medium">
            Create your first workflow
          </button>
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
              {wf.description && (
                <p className="text-xs text-textMuted mb-3 line-clamp-2">{wf.description}</p>
              )}
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
                  {wf.steps.length > 3 && (
                    <p className="text-xs text-textMuted pl-4">+{wf.steps.length - 3} more</p>
                  )}
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
              <button onClick={() => setDeleteId(null)} disabled={isDeleting}
                className="flex-1 px-4 py-2 border border-border rounded-lg text-sm text-text hover:bg-surfaceHighlight transition-colors">
                Cancel
              </button>
              <button onClick={confirmDelete} disabled={isDeleting}
                className="flex-1 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-lg text-sm font-medium flex items-center justify-center gap-2">
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
