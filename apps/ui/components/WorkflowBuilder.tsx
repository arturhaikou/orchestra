
import React, { useCallback, useEffect, useState, useRef } from 'react';
import { Play, Plus, ArrowLeft, Pencil, Trash2, Loader2, Settings, Save, Layout, AlertTriangle, Bot, GripVertical, FileText, Zap, PlayCircle, StopCircle, X, ChevronRight } from 'lucide-react';
import ReactFlow, { 
  Background, 
  Controls, 
  MiniMap,
  useNodesState,
  useEdgesState,
  addEdge,
  Connection,
  Edge,
  ReactFlowInstance,
  Node
} from 'reactflow';
import { Workflow, Agent } from '../types';
import { getWorkspacesWorkflows, createWorkflow, updateWorkflow, deleteWorkflow } from '../services/workflowService';
import { getAgents } from '../services/agentService';

interface WorkflowBuilderProps {
  workspaceId: string;
  isDarkMode?: boolean;
}

const WorkflowBuilder: React.FC<WorkflowBuilderProps> = ({ workspaceId, isDarkMode = true }) => {
  const [viewMode, setViewMode] = useState<'list' | 'editor'>('list');
  const [workflows, setWorkflows] = useState<Workflow[]>([]);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  
  // Editor State
  const [currentWorkflowId, setCurrentWorkflowId] = useState<string | null>(null);
  const [workflowName, setWorkflowName] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [reactFlowInstance, setReactFlowInstance] = useState<ReactFlowInstance | null>(null);
  const [isPaletteOpen, setIsPaletteOpen] = useState(false);
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  
  // Delete State
  const [deleteConfirmationId, setDeleteConfirmationId] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  
  const [nodes, setNodes, onNodesChange] = useNodesState([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState([]);

  // Fetch workflows and agents
  useEffect(() => {
    const fetchData = async () => {
      setIsLoading(true);
      try {
        const [wfData, agentData] = await Promise.all([
            getWorkspacesWorkflows(workspaceId),
            getAgents(workspaceId)
        ]);
        setWorkflows(wfData);
        setAgents(agentData);
      } catch (error) {
        console.error("Failed to load data", error);
      } finally {
        setIsLoading(false);
      }
    };
    
    if (workspaceId) {
        fetchData();
        setViewMode('list');
    }
  }, [workspaceId]);

  const onConnect = useCallback((params: Edge | Connection) => setEdges((eds) => addEdge(params, eds)), [setEdges]);

  const handleCreateNew = () => {
      setCurrentWorkflowId(null);
      setWorkflowName('Untitled Workflow');
      setNodes([
        { 
            id: 'start-default', 
            type: 'input', 
            data: { label: 'Start' }, 
            position: { x: 250, y: 50 },
            style: { 
                background: '#1e293b', 
                color: '#fff', 
                border: '2px solid #10b981', 
                width: 120, 
                borderRadius: '20px', 
                padding: '10px',
                display: 'flex',
                justifyContent: 'center',
                alignItems: 'center',
                fontWeight: 'bold'
            }
        }
      ]);
      setEdges([]);
      setViewMode('editor');
      setIsPaletteOpen(false);
  };

  const handleEdit = (workflow: Workflow) => {
      setCurrentWorkflowId(workflow.id);
      setWorkflowName(workflow.name);
      setNodes(workflow.nodes || []);
      setEdges(workflow.edges || []);
      setViewMode('editor');
      setIsPaletteOpen(false);
  };
  
  const handleDeleteClick = (id: string, e?: React.MouseEvent) => {
      if (e) e.stopPropagation();
      setDeleteConfirmationId(id);
  };

  const executeDelete = async () => {
      if (!deleteConfirmationId) return;
      setIsDeleting(true);
      
      try {
          await deleteWorkflow(deleteConfirmationId);
          setWorkflows(prev => prev.filter(w => w.id !== deleteConfirmationId));
          
          if (currentWorkflowId === deleteConfirmationId) {
              setViewMode('list');
              setCurrentWorkflowId(null);
          }
      } catch (error) {
          console.error("Failed to delete workflow", error);
      } finally {
          setDeleteConfirmationId(null);
          setIsDeleting(false);
      }
  };

  const handleSave = async () => {
      if (!workflowName.trim()) return;

      setIsSaving(true);
      try {
          if (currentWorkflowId) {
              const updatedWorkflow = await updateWorkflow(currentWorkflowId, {
                  name: workflowName,
                  nodes,
                  edges
              });
              setWorkflows(prev => prev.map(w => w.id === currentWorkflowId ? updatedWorkflow : w));
          } else {
              const newWorkflow = await createWorkflow(workspaceId, {
                  name: workflowName,
                  nodes,
                  edges
              });
              setWorkflows(prev => [...prev, newWorkflow]);
              setCurrentWorkflowId(newWorkflow.id);
          }
      } catch (error) {
          console.error("Failed to save workflow", error);
      } finally {
          setIsSaving(false);
      }
  };

  const handleBack = () => {
      setViewMode('list');
      setCurrentWorkflowId(null);
      setIsPaletteOpen(false);
  };

  // Drag and Drop Handlers
  const onDragStart = (event: React.DragEvent, nodeType: string, payload?: any) => {
      event.dataTransfer.setData('application/reactflow', nodeType);
      if (payload) {
          event.dataTransfer.setData('application/payload', JSON.stringify(payload));
      }
      event.dataTransfer.effectAllowed = 'move';
  };

  const onDragOver = useCallback((event: React.DragEvent) => {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
      (event: React.DragEvent) => {
        event.preventDefault();

        if (!reactFlowWrapper.current || !reactFlowInstance) return;

        const reactFlowBounds = reactFlowWrapper.current.getBoundingClientRect();
        const type = event.dataTransfer.getData('application/reactflow');
        const payloadStr = event.dataTransfer.getData('application/payload');

        if (typeof type === 'undefined' || !type) {
          return;
        }

        const position = reactFlowInstance.project({
          x: event.clientX - reactFlowBounds.left,
          y: event.clientY - reactFlowBounds.top,
        });

        let newNode: Node;

        if (type === 'agent' && payloadStr) {
            const agent = JSON.parse(payloadStr);
            newNode = {
                id: `agent-${agent.id}-${Date.now()}`,
                type: 'default',
                position,
                data: { label: `${agent.name} (${agent.role})` },
                style: { 
                    background: '#1e293b', 
                    color: '#fff', 
                    border: '1px solid #6366f1', 
                    width: 180, 
                    borderRadius: '8px', 
                    padding: '10px',
                    fontSize: '12px',
                    boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)'
                }
            };
        } else if (type === 'start') {
             newNode = {
                id: `start-${Date.now()}`,
                type: 'input',
                position,
                data: { label: 'Start' },
                style: { 
                    background: '#1e293b', 
                    color: '#fff', 
                    border: '2px solid #10b981', 
                    width: 120, 
                    borderRadius: '20px', 
                    padding: '10px',
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center',
                    fontWeight: 'bold'
                }
             };
        } else if (type === 'end') {
             newNode = {
                id: `end-${Date.now()}`,
                type: 'output',
                position,
                data: { label: 'End' },
                style: { 
                    background: '#1e293b', 
                    color: '#fff', 
                    border: '2px solid #ef4444', 
                    width: 120, 
                    borderRadius: '20px', 
                    padding: '10px',
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center',
                    fontWeight: 'bold'
                }
             };
        } else if (type === 'trigger') {
             newNode = {
                id: `trigger-${Date.now()}`,
                type: 'input',
                position,
                data: { label: 'Trigger' },
                style: { background: '#1e293b', color: '#fff', border: '1px solid #475569', width: 150, borderRadius: '8px', padding: '10px' }
             };
        } else {
            newNode = {
                id: `node-${Date.now()}`,
                type: 'default',
                position,
                data: { label: 'Action Node' },
                style: { background: '#1e293b', color: '#fff', border: '1px solid #10b981', width: 150, borderRadius: '8px', padding: '10px' }
            };
        }

        setNodes((nds) => nds.concat(newNode));
        if (window.innerWidth < 1024) setIsPaletteOpen(false);
      },
      [reactFlowInstance, setNodes],
  );

  const flowColors = {
      background: isDarkMode ? '#27272a' : '#e5e7eb',
      minimapBg: isDarkMode ? '#18181b' : '#ffffff',
      minimapBorder: isDarkMode ? '#27272a' : '#e5e7eb',
  };

  const renderDeleteModal = () => {
      if (!deleteConfirmationId) return null;
      const workflowToDelete = workflows.find(w => w.id === deleteConfirmationId) || { name: 'this workflow' };
      
      return (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-sm rounded-xl shadow-2xl overflow-hidden p-6 space-y-4 animate-scale-in">
             <div className="flex items-center gap-3 text-red-500">
                <div className="w-10 h-10 rounded-full bg-red-500/10 flex items-center justify-center shrink-0">
                    <AlertTriangle className="w-5 h-5" />
                </div>
                <h3 className="text-lg font-bold text-text">Delete Workflow?</h3>
             </div>
             
             <p className="text-sm text-textMuted leading-relaxed">
                Are you sure you want to delete <span className="font-semibold text-text">{workflowToDelete.name}</span>? This action cannot be undone.
             </p>

             <div className="flex gap-3 pt-2">
                <button 
                  onClick={() => setDeleteConfirmationId(null)}
                  className="flex-1 px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
                  disabled={isDeleting}
                >
                  Cancel
                </button>
                <button 
                  onClick={executeDelete}
                  className="flex-1 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-md text-sm font-medium transition-colors flex items-center justify-center gap-2 shadow-lg shadow-red-500/20"
                  disabled={isDeleting}
                >
                  {isDeleting ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Confirm Delete'}
                </button>
             </div>
          </div>
        </div>
      );
  };

  if (viewMode === 'list') {
      return (
          <div className="space-y-6 animate-fade-in h-full flex flex-col">
              <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 shrink-0">
                <div>
                    <h2 className="text-2xl font-bold text-text">Workflows</h2>
                    <p className="text-textMuted text-sm mt-1">Automate processes with visual workflows.</p>
                </div>
                <button 
                    onClick={handleCreateNew}
                    className="w-full sm:w-auto bg-primary hover:bg-primaryHover text-white px-4 py-2 rounded-md flex items-center justify-center gap-2 text-sm transition-colors shadow-lg shadow-primary/20"
                >
                <Plus className="w-4 h-4" /> New Workflow
                </button>
            </div>

            {isLoading ? (
                 <div className="flex-1 flex items-center justify-center">
                    <Loader2 className="w-8 h-8 animate-spin text-primary" />
                 </div>
            ) : workflows.length === 0 ? (
                <div className="flex-1 flex flex-col items-center justify-center border-2 border-dashed border-border rounded-lg p-8 sm:p-12 text-center bg-surface/30">
                    <div className="w-16 h-16 bg-surfaceHighlight rounded-full flex items-center justify-center mb-4">
                        <Layout className="w-8 h-8 text-textMuted" />
                    </div>
                    <p className="text-lg font-medium text-text">No workflows found</p>
                    <p className="text-textMuted mb-6 text-sm max-w-xs">Create your first workflow to start automating tasks in this workspace.</p>
                    <button onClick={handleCreateNew} className="text-primary hover:underline font-medium text-sm">Create Workflow</button>
                </div>
            ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 sm:gap-6 overflow-y-auto pb-4">
                    {workflows.map(wf => (
                        <div 
                            key={wf.id} 
                            onClick={() => handleEdit(wf)}
                            className="bg-surface border border-border p-6 rounded-lg cursor-pointer hover:border-primary/50 hover:shadow-lg transition-all group relative flex flex-col h-[200px]"
                        >
                            <div className="flex justify-between items-start mb-4">
                                <div className="w-10 h-10 bg-gradient-to-br from-primary/20 to-purple-500/20 rounded-lg flex items-center justify-center text-primary border border-primary/10">
                                    <Settings className="w-5 h-5" />
                                </div>
                                <button 
                                    onClick={(e) => handleDeleteClick(wf.id, e)}
                                    className="p-2 text-textMuted hover:text-red-500 hover:bg-red-500/10 rounded-md transition-all sm:opacity-0 group-hover:opacity-100"
                                    title="Delete Workflow"
                                >
                                    <Trash2 className="w-4 h-4" />
                                </button>
                            </div>
                            <h3 className="font-semibold text-text mb-1 truncate" title={wf.name}>{wf.name}</h3>
                            <p className="text-xs text-textMuted">{wf.nodes?.length || 0} nodes · {wf.edges?.length || 0} connections</p>
                            
                            <div className="mt-auto pt-4 border-t border-border flex items-center justify-between text-xs text-textMuted">
                                <span>{wf.id}</span>
                                <span className="flex items-center gap-1 text-primary font-medium group-hover:translate-x-1 transition-transform">
                                    Edit <Pencil className="w-3 h-3" />
                                </span>
                            </div>
                        </div>
                    ))}
                </div>
            )}
            {renderDeleteModal()}
          </div>
      );
  }

  // Editor View
  return (
    <div className="h-full flex flex-col animate-fade-in relative pb-4">
       <div className="flex flex-col lg:flex-row justify-between lg:items-center mb-4 gap-4 border-b border-border pb-4 shrink-0">
         <div className="flex items-center gap-3 sm:gap-4 overflow-hidden">
             <button 
                onClick={handleBack}
                className="p-2 hover:bg-surfaceHighlight rounded-full text-textMuted hover:text-text transition-colors shrink-0"
                title="Back to List"
             >
                 <ArrowLeft className="w-5 h-5" />
             </button>
             <div className="flex flex-col min-w-0">
                 <input 
                    type="text" 
                    value={workflowName}
                    onChange={(e) => setWorkflowName(e.target.value)}
                    className="bg-transparent text-base sm:text-lg font-bold text-text focus:outline-none focus:border-b border-primary w-full max-w-xs sm:w-64 placeholder:text-textMuted/50 truncate"
                    placeholder="Workflow Name"
                 />
                 <p className="text-[10px] sm:text-xs text-textMuted uppercase font-bold tracking-wider">{currentWorkflowId ? 'Editing Workflow' : 'Defining New Workflow'}</p>
             </div>
         </div>
         <div className="flex flex-wrap items-center gap-2">
           <button 
              onClick={() => setIsPaletteOpen(!isPaletteOpen)}
              className="lg:hidden px-3 py-1.5 bg-surfaceHighlight border border-border text-text rounded text-xs font-bold uppercase tracking-wider flex items-center gap-2 transition-colors"
           >
             <Plus className="w-4 h-4 text-primary" /> Components
           </button>

           <div className="flex items-center gap-2 ml-auto lg:ml-0">
                {currentWorkflowId && (
                    <button
                        onClick={() => handleDeleteClick(currentWorkflowId)}
                        className="p-2 border border-red-500/30 text-red-500 hover:bg-red-500/10 rounded transition-colors"
                        title="Delete Workflow"
                    >
                        <Trash2 className="w-4 h-4" />
                    </button>
                )}
                <button 
                    onClick={handleSave}
                    disabled={!workflowName.trim() || isSaving}
                    className="bg-surface border border-border text-text hover:bg-surfaceHighlight px-3 py-1.5 rounded text-xs font-bold uppercase tracking-wider transition-colors flex items-center gap-2 disabled:opacity-50"
                >
                    {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />} <span className="hidden sm:inline">Save</span>
                </button>
                <button className="bg-primary hover:bg-primaryHover text-white px-3 py-1.5 rounded text-xs font-bold uppercase tracking-wider flex items-center gap-2 transition-colors shadow-lg shadow-primary/20">
                    <Play className="w-3 h-3" /> <span className="hidden sm:inline">Test Run</span><span className="sm:hidden">Run</span>
                </button>
           </div>
         </div>
       </div>
       
       <div className="flex-1 flex overflow-hidden border border-border rounded-xl bg-surfaceHighlight/10 backdrop-blur-sm relative transition-colors duration-300">
          {/* Main Canvas Area */}
          <div className="flex-1 h-full relative overflow-hidden" ref={reactFlowWrapper}>
            <ReactFlow
                nodes={nodes}
                edges={edges}
                onNodesChange={onNodesChange}
                onEdgesChange={onEdgesChange}
                onConnect={onConnect}
                onInit={setReactFlowInstance}
                onDrop={onDrop}
                onDragOver={onDragOver}
                fitView
                proOptions={{ hideAttribution: true }}
                minZoom={0.5}
                maxZoom={1.5}
            >
                <Background color={flowColors.background} gap={20} size={1} />
                <Controls className="bg-surface border border-border fill-text text-text" />
                <MiniMap style={{ background: flowColors.minimapBg, border: `1px solid ${flowColors.minimapBorder}` }} nodeColor="#6366f1" />
            </ReactFlow>

            <div className="absolute top-4 left-4 bg-surface/90 backdrop-blur p-3 rounded-lg border border-border text-[10px] sm:text-xs space-y-2 pointer-events-none transition-colors duration-300 shadow-xl z-10 hidden sm:block">
                <div className="font-bold text-textMuted mb-2 uppercase tracking-widest opacity-60">Canvas Key</div>
                <div className="flex items-center gap-2 font-medium text-text"><div className="w-3 h-3 rounded-full border-2 border-emerald-500 bg-[#1e293b]"></div> Start Point</div>
                <div className="flex items-center gap-2 font-medium text-text"><div className="w-3 h-3 rounded-full border-2 border-red-500 bg-[#1e293b]"></div> Exit Point</div>
                <div className="flex items-center gap-2 font-medium text-text"><div className="w-3 h-3 rounded-sm border-2 border-indigo-500 bg-[#1e293b]"></div> Agent Action</div>
                <div className="flex items-center gap-2 font-medium text-text"><div className="w-3 h-3 rounded-sm border-2 border-slate-500 bg-[#1e293b]"></div> System Event</div>
            </div>
          </div>

          {/* Sidebar Palette / Overlay Panel */}
          {isPaletteOpen && window.innerWidth < 1024 && (
             <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-30 lg:hidden" onClick={() => setIsPaletteOpen(false)} />
          )}

          <div className={`
              fixed lg:relative inset-y-0 right-0 w-72 bg-surface border-l border-border flex flex-col z-40 transform transition-transform duration-300 ease-in-out
              ${isPaletteOpen || window.innerWidth >= 1024 ? 'translate-x-0' : 'translate-x-full'}
          `}>
             <div className="p-4 sm:p-5 border-b border-border bg-surfaceHighlight/30 flex justify-between items-center shrink-0">
                 <h3 className="font-bold text-text text-xs uppercase tracking-widest flex items-center gap-2">
                     <Layout className="w-4 h-4 text-primary" /> Components
                 </h3>
                 <button onClick={() => setIsPaletteOpen(false)} className="lg:hidden p-1 hover:bg-surfaceHighlight rounded text-textMuted">
                    <X className="w-4 h-4" />
                 </button>
             </div>
             
             <div className="flex-1 overflow-y-auto p-4 sm:p-5 space-y-8 custom-scrollbar">
                 {/* Agents Section */}
                 <div>
                    <h4 className="text-[10px] font-bold text-textMuted uppercase mb-3 tracking-widest flex items-center justify-between">
                        <span>Deployable Agents</span>
                        <span className="bg-surfaceHighlight px-1.5 py-0.5 rounded text-[9px]">{agents.length}</span>
                    </h4>
                    <div className="space-y-3">
                        {agents.length === 0 ? (
                            <div className="text-[10px] text-textMuted italic p-4 border-2 border-dashed border-border rounded-lg text-center bg-surfaceHighlight/10">No agents available</div>
                        ) : (
                            agents.map(agent => (
                                <div 
                                    key={agent.id}
                                    className="p-3 bg-surface border border-border rounded-lg cursor-grab hover:border-primary hover:shadow-lg transition-all shadow-sm group flex items-start gap-3 select-none active:cursor-grabbing hover:bg-primary/5"
                                    draggable
                                    onDragStart={(event) => onDragStart(event, 'agent', agent)}
                                >
                                    <div className="mt-0.5 p-1.5 bg-primary/10 rounded-md group-hover:bg-primary group-hover:text-white transition-colors">
                                        <Bot className="w-4 h-4 text-primary group-hover:text-white" />
                                    </div>
                                    <div className="flex-1 min-w-0">
                                        <div className="font-bold text-xs text-text truncate group-hover:text-primary transition-colors">{agent.name}</div>
                                        <div className="text-[9px] text-textMuted font-mono truncate uppercase mt-0.5">{agent.role}</div>
                                    </div>
                                    <GripVertical className="w-4 h-4 text-textMuted opacity-20 group-hover:opacity-100 mt-1" />
                                </div>
                            ))
                        )}
                    </div>
                 </div>

                 {/* Basic Blocks Section */}
                 <div className="pt-4 border-t border-border/50">
                    <h4 className="text-[10px] font-bold text-textMuted uppercase mb-3 tracking-widest">Control Logic</h4>
                    <div className="space-y-2">
                        <div 
                            className="p-3 bg-surface border border-border rounded-lg cursor-grab hover:border-emerald-500 hover:bg-emerald-500/5 transition-all shadow-sm flex items-center gap-3 select-none group"
                            draggable
                            onDragStart={(event) => onDragStart(event, 'start')}
                        >
                            <div className="p-1.5 bg-emerald-500/10 rounded-md group-hover:bg-emerald-500 transition-colors">
                                <PlayCircle className="w-4 h-4 text-emerald-500 group-hover:text-white" />
                            </div>
                            <span className="text-xs font-bold text-text uppercase tracking-wider">Start Entry</span>
                        </div>
                         <div 
                            className="p-3 bg-surface border border-border rounded-lg cursor-grab hover:border-red-500 hover:bg-red-500/5 transition-all shadow-sm flex items-center gap-3 select-none group"
                            draggable
                            onDragStart={(event) => onDragStart(event, 'end')}
                        >
                             <div className="p-1.5 bg-red-500/10 rounded-md group-hover:bg-red-500 transition-colors">
                                <StopCircle className="w-4 h-4 text-red-500 group-hover:text-white" />
                             </div>
                            <span className="text-xs font-bold text-text uppercase tracking-wider">End Result</span>
                        </div>

                        <div 
                            className="p-3 bg-surface border border-border rounded-lg cursor-grab hover:border-slate-500 hover:bg-slate-500/5 transition-all shadow-sm flex items-center gap-3 select-none group"
                            draggable
                            onDragStart={(event) => onDragStart(event, 'trigger')}
                        >
                             <div className="p-1.5 bg-slate-500/10 rounded-md group-hover:bg-slate-500 transition-colors">
                                <Zap className="w-4 h-4 text-slate-500 group-hover:text-white" />
                             </div>
                            <span className="text-xs font-bold text-text uppercase tracking-wider">Event Listen</span>
                        </div>
                        <div 
                            className="p-3 bg-surface border border-border rounded-lg cursor-grab hover:border-primary hover:bg-primary/5 transition-all shadow-sm flex items-center gap-3 select-none group"
                            draggable
                            onDragStart={(event) => onDragStart(event, 'action')}
                        >
                            <div className="p-1.5 bg-primary/10 rounded-md group-hover:bg-primary transition-colors">
                                <FileText className="w-4 h-4 text-primary group-hover:text-white" />
                            </div>
                            <span className="text-xs font-bold text-text uppercase tracking-wider">System Task</span>
                        </div>
                    </div>
                 </div>
             </div>
             
             <div className="p-4 bg-surfaceHighlight/50 border-t border-border mt-auto">
                 <p className="text-[9px] text-textMuted italic text-center">Drag components onto the canvas to architect your workflow.</p>
             </div>
          </div>
       </div>
       {renderDeleteModal()}
    </div>
  );
};

export default WorkflowBuilder;
