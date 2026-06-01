import React, { useCallback, useEffect, useState } from 'react';
import ReactFlow, {
  Background,
  BackgroundVariant,
  Controls,
  Edge,
  Handle,
  Node,
  NodeProps,
  Position,
  useEdgesState,
  useNodesState,
  MarkerType,
} from 'reactflow';
import 'reactflow/dist/style.css';
import {
  Plus, Trash2, Loader2, Save, ArrowLeft, AlertTriangle,
  ToggleLeft, ToggleRight, Bot, X, Search, GitBranch,
} from 'lucide-react';
import { colorTokensHex } from '../src/tokens';
import { WorkflowDefinition, Agent } from '../types';
import {
  getWorkflowDefinitions,
  createWorkflowDefinition,
  updateWorkflowDefinition,
  deleteWorkflowDefinition,
  CreateWorkflowStepPayload,
} from '../services/workflowService';
import { getAgents } from '../services/agentService';

// ── Interfaces ───────────────────────────────────────────────────────────────

interface WorkflowBuilderProps {
  workspaceId: string;
}

interface StepDraft {
  agentId: string;
  instructionOverride: string;
  passPreviousOutput: boolean;
}

interface AgentStepNodeData {
  stepIndex: number;
  agentName: string;
  isSelected: boolean;
  passPreviousOutput: boolean;
  hasInstructionOverride: boolean;
  onDelete: () => void;
}

interface AddStepNodeData {
  onAdd: () => void;
}

// ── Constants ────────────────────────────────────────────────────────────────

const NODE_WIDTH = 280;
const NODE_Y_GAP = 130;
const emptyStep = (): StepDraft => ({ agentId: '', instructionOverride: '', passPreviousOutput: false });

// ── Custom Nodes (outside component — stable nodeTypes ref) ───────────────────

const AgentStepNode: React.FC<NodeProps<AgentStepNodeData>> = ({ data, selected }) => {
  const active = selected || data.isSelected;
  return (
    <div
      style={{
        width: NODE_WIDTH,
        background: colorTokensHex.uiBg2,
        border: `2px solid ${active ? colorTokensHex.uiAccent : colorTokensHex.uiBorder1}`,
        borderRadius: 12,
        padding: '12px 14px',
        cursor: 'pointer',
        boxShadow: active ? `0 0 0 3px rgba(99,102,241,0.2)` : '0 2px 8px rgba(0,0,0,0.4)',
        transition: 'border-color 0.15s, box-shadow 0.15s',
      }}
    >
      <Handle
        type="target"
        position={Position.Top}
        style={{ width: 10, height: 10, background: colorTokensHex.uiBorder2, border: `2px solid ${colorTokensHex.uiBg2}`, top: -6 }}
      />
      <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
        <div style={{
          width: 36, height: 36, borderRadius: 8,
          background: active ? 'rgba(99,102,241,0.15)' : colorTokensHex.uiBorder1,
          display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
        }}>
          <Bot size={16} color={active ? colorTokensHex.uiAccent : colorTokensHex.uiText2} />
        </div>
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 4, marginBottom: 3, flexWrap: 'wrap' }}>
            <span style={{
              fontSize: 10, fontWeight: 700, color: colorTokensHex.uiAccent,
              background: 'rgba(99,102,241,0.12)', padding: '1px 6px', borderRadius: 20,
            }}>
              {data.stepIndex + 1}
            </span>
            {data.passPreviousOutput && (
              <span style={{ fontSize: 10, color: colorTokensHex.uiAccentPurple, background: 'rgba(167,139,250,0.1)', padding: '1px 6px', borderRadius: 20 }}>
                ↻ chain
              </span>
            )}
            {data.hasInstructionOverride && (
              <span style={{ fontSize: 10, color: colorTokensHex.uiWarning, background: 'rgba(251,191,36,0.1)', padding: '1px 6px', borderRadius: 20 }}>
                + notes
              </span>
            )}
          </div>
          <p style={{
            margin: 0, fontSize: 13, fontWeight: 600,
            color: active ? colorTokensHex.textPrimary : colorTokensHex.uiText1,
            whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis',
          }}>
            {data.agentName
              ? data.agentName
              : <span style={{ color: colorTokensHex.uiText3, fontStyle: 'italic' }}>No agent selected</span>}
          </p>
        </div>
        <button
          onClick={e => { e.stopPropagation(); data.onDelete(); }}
          style={{
            padding: 4, borderRadius: 6, border: 'none', background: 'transparent',
            cursor: 'pointer', color: colorTokensHex.uiBorder2, display: 'flex', flexShrink: 0,
          }}
          onMouseEnter={e => {
            const b = e.currentTarget as HTMLButtonElement;
            b.style.color = colorTokensHex.uiError; b.style.background = 'rgba(248,113,113,0.1)';
          }}
          onMouseLeave={e => {
            const b = e.currentTarget as HTMLButtonElement;
            b.style.color = colorTokensHex.uiBorder2; b.style.background = 'transparent';
          }}
          title="Remove step"
        >
          <X size={13} />
        </button>
      </div>
      <Handle
        type="source"
        position={Position.Bottom}
        style={{ width: 10, height: 10, background: colorTokensHex.uiBorder2, border: `2px solid ${colorTokensHex.uiBg2}`, bottom: -6 }}
      />
    </div>
  );
};

const AddStepNode: React.FC<NodeProps<AddStepNodeData>> = ({ data }) => (
  <div
    onClick={data.onAdd}
    style={{
      width: 40, height: 40, borderRadius: '50%',
      background: colorTokensHex.uiBg1, border: `2px dashed ${colorTokensHex.uiBorder2}`,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      cursor: 'pointer', transition: 'border-color 0.15s, background 0.15s',
    }}
    onMouseEnter={e => {
      const el = e.currentTarget as HTMLDivElement;
      el.style.borderColor = colorTokensHex.uiAccent; el.style.background = 'rgba(99,102,241,0.1)';
    }}
    onMouseLeave={e => {
      const el = e.currentTarget as HTMLDivElement;
      el.style.borderColor = colorTokensHex.uiBorder2; el.style.background = colorTokensHex.uiBg1;
    }}
    title="Add step"
  >
    <Handle
      type="target"
      position={Position.Top}
      style={{ width: 10, height: 10, background: colorTokensHex.uiBorder2, border: `2px solid ${colorTokensHex.uiBg1}`, top: -6 }}
    />
    <Plus size={16} color={colorTokensHex.uiText3} />
  </div>
);

const nodeTypes = { agentStep: AgentStepNode, addStep: AddStepNode };

// ── WorkflowBuilder ──────────────────────────────────────────────────────────

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

    const agentMap = new Map(agents.map(a => [a.id, a.name]));

    const newNodes: Node[] = [
      ...steps.map((step, i) => ({
        id: `step-${i}`,
        type: 'agentStep',
        position: { x: 0, y: i * NODE_Y_GAP },
        data: {
          stepIndex: i,
          agentName: agentMap.get(step.agentId) ?? '',
          isSelected: selectedStepIndex === i,
          passPreviousOutput: step.passPreviousOutput,
          hasInstructionOverride: step.instructionOverride.trim().length > 0,
          onDelete: () => {
            setSteps(prev => prev.filter((_, idx) => idx !== i));
            setSelectedStepIndex(null);
          },
        } as AgentStepNodeData,
        draggable: false,
        selectable: true,
      })),
      {
        id: 'add-step',
        type: 'addStep',
        position: { x: (NODE_WIDTH - 40) / 2, y: steps.length * NODE_Y_GAP },
        data: {
          onAdd: () => {
            setSteps(prev => [...prev, emptyStep()]);
            setSelectedStepIndex(steps.length);
          },
        } as AddStepNodeData,
        draggable: false,
        selectable: false,
      },
    ];

    const newEdges: Edge[] = steps.map((_, i) => ({
      id: `e-${i}`,
      source: `step-${i}`,
      target: i === steps.length - 1 ? 'add-step' : `step-${i + 1}`,
      type: 'smoothstep',
      style: { stroke: colorTokensHex.uiBorder2, strokeWidth: 2 },
      markerEnd: { type: MarkerType.ArrowClosed, color: colorTokensHex.uiBorder2, width: 14, height: 14 },
    }));

    setRfNodes(newNodes);
    setRfEdges(newEdges);
  }, [steps, selectedStepIndex, agents, view]);

  // ── Handlers ───────────────────────────────────────────────────────────────

  const openCreate = () => {
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
    setEditingId(wf.id);
    setName(wf.name);
    setDescription(wf.description ?? '');
    setSteps(
      wf.steps.length > 0
        ? wf.steps.map(s => ({
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

  const addAgentStep = (agent: Agent) => {
    const newIndex = steps.length;
    setSteps([...steps, { agentId: agent.id, instructionOverride: '', passPreviousOutput: false }]);
    setSelectedStepIndex(newIndex);
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
    } catch (e) {
      console.error(e);
    } finally {
      setDeleteId(null);
      setIsDeleting(false);
    }
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
      <div
        style={{
          position: 'fixed', inset: 0, zIndex: 50,
          display: 'flex', flexDirection: 'column',
          background: colorTokensHex.bgApp,
        }}
      >
        {/* Top toolbar */}
        <div style={{
          display: 'flex', alignItems: 'center', gap: 10, padding: '10px 16px',
          background: colorTokensHex.uiBg1, borderBottom: `1px solid ${colorTokensHex.uiBorder1}`, flexShrink: 0,
          minHeight: 52,
        }}>
          <button
            onClick={() => setView('list')}
            title="Back to workflows"
            style={{
              padding: 6, borderRadius: 8, border: 'none', background: 'transparent',
              cursor: 'pointer', color: colorTokensHex.uiText2, display: 'flex', flexShrink: 0,
            }}
            onMouseEnter={e => { const b = e.currentTarget as HTMLButtonElement; b.style.background = colorTokensHex.uiBg2; b.style.color = colorTokensHex.textPrimary; }}
            onMouseLeave={e => { const b = e.currentTarget as HTMLButtonElement; b.style.background = 'transparent'; b.style.color = colorTokensHex.uiText2; }}
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
                fontSize: 15, fontWeight: 700, color: colorTokensHex.textPrimary, width: '100%',
              }}
            />
            {description && (
              <span style={{ fontSize: 11, color: colorTokensHex.uiText3, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {description}
              </span>
            )}
          </div>

          {saveError && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 5, color: colorTokensHex.uiError, fontSize: 12, flexShrink: 0 }}>
              <AlertTriangle size={13} />
              <span>{saveError}</span>
            </div>
          )}

          {editingId && (
            <button
              onClick={() => setDeleteId(editingId)}
              style={{
                padding: '6px 12px', borderRadius: 8,
                border: `1px solid rgba(239,68,68,0.3)`, background: 'transparent',
                cursor: 'pointer', color: colorTokensHex.uiError, fontSize: 12, flexShrink: 0,
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
              background: colorTokensHex.uiAccent, color: colorTokensHex.textPrimary,
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
            width: 220, background: colorTokensHex.uiBg1, borderRight: `1px solid ${colorTokensHex.uiBorder1}`,
            display: 'flex', flexDirection: 'column', overflow: 'hidden', flexShrink: 0,
          }}>
            <div style={{ padding: '12px 14px', borderBottom: `1px solid ${colorTokensHex.uiBorder1}` }}>
              <p style={{
                margin: '0 0 8px', fontSize: 10, fontWeight: 700, color: colorTokensHex.uiText2,
                textTransform: 'uppercase', letterSpacing: '0.08em',
              }}>
                Agents
              </p>
              <div style={{ position: 'relative' }}>
                <Search size={13} style={{ position: 'absolute', left: 9, top: 8, color: colorTokensHex.uiText3, pointerEvents: 'none' }} />
                <input
                  type="text"
                  value={agentSearch}
                  onChange={e => setAgentSearch(e.target.value)}
                  placeholder="Search…"
                  style={{
                    width: '100%', padding: '6px 10px 6px 28px', borderRadius: 8,
                    border: `1px solid ${colorTokensHex.uiBorder1}`, background: colorTokensHex.uiBg2,
                    color: colorTokensHex.textPrimary, fontSize: 12, outline: 'none', boxSizing: 'border-box',
                  }}
                />
              </div>
            </div>
            <div style={{ flex: 1, overflowY: 'auto', padding: '8px 6px' }}>
              {filteredAgents.length === 0 && (
                <p style={{ fontSize: 12, color: colorTokensHex.uiText3, textAlign: 'center', padding: 16, fontStyle: 'italic' }}>
                  {agentSearch ? 'No matches' : 'No agents'}
                </p>
              )}
              {filteredAgents.map(agent => (
                <button
                  key={agent.id}
                  onClick={() => addAgentStep(agent)}
                  title={`Add ${agent.name} as next step`}
                  style={{
                    width: '100%', display: 'flex', alignItems: 'center', gap: 9,
                    padding: '7px 8px', borderRadius: 8, border: 'none', background: 'transparent',
                    cursor: 'pointer', textAlign: 'left', marginBottom: 2,
                  }}
                  onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.background = colorTokensHex.uiBg2; }}
                  onMouseLeave={e => { (e.currentTarget as HTMLButtonElement).style.background = 'transparent'; }}
                >
                  <div style={{
                    width: 28, height: 28, borderRadius: 7,
                    background: 'rgba(99,102,241,0.1)', flexShrink: 0,
                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                  }}>
                    <Bot size={14} color={colorTokensHex.uiAccent} />
                  </div>
                  <div style={{ minWidth: 0, flex: 1 }}>
                    <p style={{ margin: 0, fontSize: 12, fontWeight: 600, color: colorTokensHex.uiText1, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {agent.name}
                    </p>
                    <p style={{ margin: 0, fontSize: 10, color: colorTokensHex.uiText3, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {agent.role}
                    </p>
                  </div>
                  <Plus size={12} color={colorTokensHex.uiBorder2} style={{ flexShrink: 0 }} />
                </button>
              ))}
            </div>
            {/* Description field at bottom */}
            <div style={{ padding: '10px 14px', borderTop: `1px solid ${colorTokensHex.uiBorder1}` }}>
              <p style={{ margin: '0 0 5px', fontSize: 10, fontWeight: 700, color: colorTokensHex.uiText2, textTransform: 'uppercase', letterSpacing: '0.08em' }}>
                Description
              </p>
              <textarea
                value={description}
                onChange={e => setDescription(e.target.value)}
                rows={2}
                placeholder="Optional description…"
                style={{
                  width: '100%', padding: '6px 8px', borderRadius: 6,
                  border: `1px solid ${colorTokensHex.uiBorder1}`, background: colorTokensHex.uiBg2,
                  color: colorTokensHex.uiText1, fontSize: 11, resize: 'none', outline: 'none',
                  boxSizing: 'border-box', fontFamily: 'inherit',
                }}
              />
            </div>
          </div>

          {/* Canvas */}
          <div style={{ flex: 1, position: 'relative', overflow: 'hidden', background: colorTokensHex.bgApp }}>
            {steps.length === 0 ? (
              <div style={{
                position: 'absolute', inset: 0, display: 'flex', flexDirection: 'column',
                alignItems: 'center', justifyContent: 'center', gap: 10,
              }}>
                <GitBranch size={36} color={colorTokensHex.uiBorder1} />
                <p style={{ color: colorTokensHex.uiText3, fontSize: 13, margin: 0, textAlign: 'center' }}>
                  Click an agent on the left to add the first step
                </p>
              </div>
            ) : (
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
                nodeTypes={nodeTypes}
                nodesDraggable={false}
                nodesConnectable={false}
                elementsSelectable={true}
                fitView
                fitViewOptions={{ padding: 0.5, maxZoom: 1 }}
                proOptions={{ hideAttribution: true }}
                minZoom={0.3}
                maxZoom={2}
              >
                <Background variant={BackgroundVariant.Dots} gap={24} size={1.5} color={colorTokensHex.uiBg2} />
                <Controls
                  style={{
                    background: colorTokensHex.uiBg2, border: `1px solid ${colorTokensHex.uiBorder1}`,
                    borderRadius: 8, overflow: 'hidden',
                  }}
                />
              </ReactFlow>
            )}
          </div>

          {/* Right panel: step config */}
          {selectedStep && (
            <div style={{
              width: 288, background: colorTokensHex.uiBg1, borderLeft: `1px solid ${colorTokensHex.uiBorder1}`,
              display: 'flex', flexDirection: 'column', overflow: 'hidden', flexShrink: 0,
            }}>
              {/* Header */}
              <div style={{
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                padding: '12px 16px', borderBottom: `1px solid ${colorTokensHex.uiBorder1}`,
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                  <span style={{
                    fontSize: 10, fontWeight: 700, color: colorTokensHex.uiAccent,
                    background: 'rgba(99,102,241,0.12)', padding: '2px 8px', borderRadius: 20,
                  }}>
                    Step {selectedStepIndex! + 1}
                  </span>
                  <span style={{ fontSize: 12, fontWeight: 600, color: colorTokensHex.uiText2 }}>Config</span>
                </div>
                <button
                  onClick={() => setSelectedStepIndex(null)}
                  style={{ padding: 4, borderRadius: 6, border: 'none', background: 'transparent', cursor: 'pointer', color: colorTokensHex.uiText3, display: 'flex' }}
                  onMouseEnter={e => { const b = e.currentTarget as HTMLButtonElement; b.style.color = colorTokensHex.textPrimary; b.style.background = colorTokensHex.uiBg2; }}
                  onMouseLeave={e => { const b = e.currentTarget as HTMLButtonElement; b.style.color = colorTokensHex.uiText3; b.style.background = 'transparent'; }}
                >
                  <X size={15} />
                </button>
              </div>

              {/* Fields */}
              <div style={{ flex: 1, overflowY: 'auto', padding: 16, display: 'flex', flexDirection: 'column', gap: 16 }}>

                {/* Agent */}
                <div>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: colorTokensHex.uiText2, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>
                    Agent *
                  </label>
                  <select
                    value={selectedStep.agentId}
                    onChange={e => updateStep(selectedStepIndex!, { agentId: e.target.value })}
                    style={{
                      width: '100%', padding: '8px 10px', borderRadius: 8,
                      border: `1px solid ${colorTokensHex.uiBorder1}`, background: colorTokensHex.uiBg2,
                      color: selectedStep.agentId ? colorTokensHex.textPrimary : colorTokensHex.uiText3,
                      fontSize: 13, outline: 'none',
                    }}
                  >
                    <option value="">— Select agent —</option>
                    {agents.map(a => (
                      <option key={a.id} value={a.id}>{a.name}</option>
                    ))}
                  </select>
                </div>

                {/* Instructions */}
                <div>
                  <label style={{ display: 'block', fontSize: 11, fontWeight: 600, color: colorTokensHex.uiText2, textTransform: 'uppercase', letterSpacing: '0.06em', marginBottom: 6 }}>
                    Additional Instructions
                  </label>
                  <textarea
                    value={selectedStep.instructionOverride}
                    onChange={e => updateStep(selectedStepIndex!, { instructionOverride: e.target.value })}
                    rows={5}
                    placeholder="Extra instructions appended to this agent's context for this step…"
                    style={{
                      width: '100%', padding: '8px 10px', borderRadius: 8,
                      border: `1px solid ${colorTokensHex.uiBorder1}`, background: colorTokensHex.uiBg2,
                      color: colorTokensHex.textPrimary, fontSize: 12, resize: 'vertical', outline: 'none',
                      boxSizing: 'border-box', fontFamily: 'inherit',
                    }}
                  />
                </div>

                {/* Pass previous output — only show for step index > 0 */}
                {selectedStepIndex! > 0 && (
                  <div style={{ padding: '12px 14px', borderRadius: 10, background: colorTokensHex.uiBg2, border: `1px solid ${colorTokensHex.uiBorder1}` }}>
                    <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 10 }}>
                      <div style={{ flex: 1 }}>
                        <p style={{ margin: 0, fontSize: 13, fontWeight: 600, color: colorTokensHex.uiText1 }}>Pass previous output</p>
                        <p style={{ margin: '4px 0 0', fontSize: 11, color: colorTokensHex.uiText3, lineHeight: 1.5 }}>
                          Inject the previous step's response as context for this agent
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => updateStep(selectedStepIndex!, { passPreviousOutput: !selectedStep.passPreviousOutput })}
                        style={{
                          background: 'none', border: 'none', cursor: 'pointer', padding: 0, flexShrink: 0,
                          color: selectedStep.passPreviousOutput ? colorTokensHex.uiAccent : colorTokensHex.uiBorder2,
                          display: 'flex', marginTop: 2, transition: 'color 0.15s',
                        }}
                      >
                        {selectedStep.passPreviousOutput
                          ? <ToggleRight size={26} />
                          : <ToggleLeft size={26} />}
                      </button>
                    </div>
                  </div>
                )}

                {/* Remove step */}
                {steps.length > 1 && (
                  <button
                    onClick={() => {
                      setSteps(prev => prev.filter((_, i) => i !== selectedStepIndex!));
                      setSelectedStepIndex(null);
                    }}
                    style={{
                      width: '100%', padding: '8px 16px', borderRadius: 8,
                      border: `1px solid rgba(239,68,68,0.25)`, background: 'transparent',
                      color: colorTokensHex.uiError, fontSize: 13, cursor: 'pointer',
                      display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 8,
                    }}
                    onMouseEnter={e => { (e.currentTarget as HTMLButtonElement).style.background = 'rgba(248,113,113,0.08)'; }}
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
              <button
                onClick={() => setDeleteId(null)}
                disabled={isDeleting}
                className="flex-1 px-4 py-2 border border-border rounded-lg text-sm text-text hover:bg-surfaceHighlight transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={confirmDelete}
                disabled={isDeleting}
                className="flex-1 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-lg text-sm font-medium flex items-center justify-center gap-2"
              >
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
