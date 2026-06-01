
import React, { useState, useEffect, useRef } from 'react';
import { Plus, X, Loader2, Bot, Brain, Sparkles, Pencil, AlertTriangle, Briefcase, Wrench, Search, CheckCircle2, Eye, FileText } from 'lucide-react';
import ModalErrorBanner from './ModalErrorBanner';
import { useParams, useNavigate } from 'react-router-dom';
import { marked } from 'marked';
import { Agent, Tool } from '../types';
import { getAgents, updateAgent, deleteAgent } from '../services/agentService';
import { getTools } from '../services/toolService';
import { fetchWorkspaceModels } from '../services/workspaceService';
import DeployMethodDialog from './DeployMethodDialog';
import BuiltInCatalogue from './BuiltInCatalogue';
import Toast from './Toast';
import { useModalAction } from '../hooks/useModalAction';
import LockedField from './agents/LockedField';
import AgentCard from './agents/AgentCard';
import AgentChatModal from './agents/AgentChatModal';
import { isBuiltInAgent } from '../utils/builtInAgentUtils';

interface AgentsListProps {}

// Markdown Preview Component
const MarkdownPreview: React.FC<{ content: string; className?: string }> = ({ content, className = '' }) => {
  if (!content) {
    return (
      <div className={`flex items-center justify-center h-[300px] text-textMuted italic ${className}`}>
        <div className="text-center">
          <FileText className="w-8 h-8 mx-auto mb-2 opacity-50" />
          <p>Preview will appear here</p>
        </div>
      </div>
    );
  }
  
  try {
    const html = marked.parse(content, { breaks: true });
    return (
      <div 
        className={`prose prose-sm dark:prose-invert max-w-none prose-p:leading-relaxed prose-headings:mb-2 prose-headings:mt-4 first:prose-headings:mt-0 prose-headings:font-bold prose-a:text-primary hover:prose-a:text-primaryHover ${className}`}
        dangerouslySetInnerHTML={{ __html: html }}
      />
    );
  } catch (error) {
    console.error('Markdown parse error:', error);
    return (
      <div className={`text-red-400 text-sm p-3 bg-red-500/10 rounded border border-red-500/20 ${className}`}>
        Error parsing markdown
      </div>
    );
  }
};

const AgentsList: React.FC<AgentsListProps> = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const [agents, setAgents] = useState<Agent[]>([]);
  const [availableTools, setAvailableTools] = useState<Tool[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [chatAgent, setChatAgent] = useState<Agent | null>(null);
  
  // Form State
  const [formState, setFormState] = useState({
      name: '',
      role: '',
      currentCapability: '',
      capabilities: [] as string[],
      toolActionIds: [] as string[],
      customInstructions: '',
      projectPrinciples: '',
      selectedModel: 'Default'
  });
  const [initialModel, setInitialModel] = useState<string>('Default');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [toolSearch, setToolSearch] = useState('');
  const [isPreviewModalOpen, setIsPreviewModalOpen] = useState(false);
  const [openGuideId, setOpenGuideId] = useState<string | null>(null);
  const [isBuiltInEdit, setIsBuiltInEdit] = useState(false);
  const [isDeployDialogOpen, setIsDeployDialogOpen] = useState(false);
  const [showCatalogue, setShowCatalogue] = useState(false);
  const deployButtonRef = useRef<HTMLButtonElement>(null);

  // Delete State
  const [deleteConfirmationId, setDeleteConfirmationId] = useState<string | null>(null);
  const [successToast, setSuccessToast] = useState<string | null>(null);

  // Action Configuration State
  const [configuringToolId, setConfiguringToolId] = useState<string | null>(null);
  const [selectedActionIds, setSelectedActionIds] = useState<string[]>([]);
  const [isActionModalOpen, setIsActionModalOpen] = useState(false);

  const fetchAgentsData = async () => {
    setIsLoading(true);
    const [agentData, toolData] = await Promise.all([
      getAgents(workspaceId),
      getTools(workspaceId)
    ]);
    setAgents(agentData);
    setAvailableTools(toolData);
    setIsLoading(false);
  };

  useEffect(() => {
    if (workspaceId) fetchAgentsData();
  }, [workspaceId]);

  useEffect(() => {
    if (!isModalOpen) return;
    setAvailableModels([]);
    fetchWorkspaceModels(workspaceId)
      .then(models => setAvailableModels(models))
      .catch(() => setAvailableModels([]));
  }, [isModalOpen, workspaceId]);

  const handleOpenEdit = (agent: Agent) => {
    const modelValue = agent.model ?? 'Default';
    setEditingId(agent.id);
    setInitialModel(modelValue);
    setFormState({
        name: agent.name,
        role: agent.role,
        currentCapability: '',
        capabilities: [...agent.capabilities],
        toolActionIds: [...(agent.toolActionIds || [])],
        customInstructions: agent.customInstructions || '',
        projectPrinciples: agent.projectPrinciples || '',
        selectedModel: modelValue
    });
    setToolSearch('');
    setIsBuiltInEdit(isBuiltInAgent(agent));
    setIsModalOpen(true);
  };

  const handleAddCapability = (e: React.KeyboardEvent) => {
      if (e.key === 'Enter') {
          e.preventDefault();
          const cap = formState.currentCapability.trim();
          if (cap && !formState.capabilities.includes(cap)) {
              setFormState(prev => ({
                  ...prev,
                  capabilities: [...prev.capabilities, cap],
                  currentCapability: ''
              }));
          }
      }
  };

  const removeCapability = (cap: string) => {
      setFormState(prev => ({
          ...prev,
          capabilities: prev.capabilities.filter(c => c !== cap)
      }));
  };

  const toggleTool = (toolId: string) => {
      const tool = availableTools.find(t => t.id === toolId);
      if (tool?.actions && tool.actions.length > 0) {
          // Tool has actions - open configuration modal
          setConfiguringToolId(toolId);
          // Pre-select currently selected actions for this tool
          const currentActionIds = formState.toolActionIds.filter(id => 
              tool.actions!.some(action => action.id === id)
          );
          setSelectedActionIds(currentActionIds);
          setIsActionModalOpen(true);
      } else {
          // Tool has no actions - toggle directly
          if (formState.toolActionIds.includes(toolId)) {
              setFormState(prev => ({
                  ...prev,
                  toolActionIds: prev.toolActionIds.filter(id => id !== toolId)
              }));
          } else {
              setFormState(prev => ({
                  ...prev,
                  toolActionIds: [...prev.toolActionIds, toolId]
              }));
          }
      }
  };

  const handleSave = async (e: React.FormEvent) => {
      e.preventDefault();
      if (!formState.name || !formState.role) return;
      if (isReviewAgent && !formState.projectPrinciples.trim()) return;
      if (!isReviewAgent && !formState.customInstructions.trim()) return;

      setIsSaving(true);
      try {
          const modelChanged = formState.selectedModel !== initialModel;
          const updated = await updateAgent(editingId!, {
              name: formState.name,
              role: formState.role,
              capabilities: formState.capabilities,
              toolActionIds: formState.toolActionIds,
              customInstructions: isReviewAgent ? undefined : formState.customInstructions,
              projectPrinciples: isReviewAgent ? formState.projectPrinciples : undefined,
              ...(modelChanged && { model: formState.selectedModel === 'Default' ? null : formState.selectedModel })
          });
          setAgents(prev => prev.map(a => a.id === editingId ? { ...a, ...updated } : a));
          setIsModalOpen(false);
      } catch (error) {
          console.error("Failed to save agent", error);
      } finally {
          setIsSaving(false);
      }
  };

  const { execute: executeDeleteAction, isLoading: isDeleting, error: deleteError, resetError: resetDeleteError } = useModalAction(
    async () => {
      if (!deleteConfirmationId) return;
      await deleteAgent(deleteConfirmationId);
    },
    () => {
      const deletedName = agents.find(a => a.id === deleteConfirmationId)?.name || 'Agent';
      setAgents(prev => prev.filter(a => a.id !== deleteConfirmationId));
      setDeleteConfirmationId(null);
      setSuccessToast(`${deletedName} has been removed.`);
      setTimeout(() => setSuccessToast(null), 5000);
    }
  );

  const cancelDelete = () => {
    setDeleteConfirmationId(null);
    resetDeleteError();
  };

  const cancelActionSelection = () => {
    setIsActionModalOpen(false);
    setConfiguringToolId(null);
    setSelectedActionIds([]);
  };

  // Derived: true when the current tool selection contains a code review action.
  // Drives the conditional rendering of Project Principles vs Custom Instructions.
  const REVIEW_ACTION_NAMES = ['review_pull_request', 'review_merge_request'];
  const isReviewAgent = availableTools.some(tool =>
    tool.actions?.some(action =>
      formState.toolActionIds.includes(action.id) && REVIEW_ACTION_NAMES.includes(action.name)
    ) ?? false
  );

  const filteredTools = availableTools.filter(tool => 
    tool.name.toLowerCase().includes(toolSearch.toLowerCase()) ||
    tool.description.toLowerCase().includes(toolSearch.toLowerCase())
  );

  if (showCatalogue) {
    return (
      <BuiltInCatalogue
        workspaceId={workspaceId}
        onViewAgent={(agentId) => navigate(`/workspaces/${workspaceId}/agents/${agentId}/edit`)}
        onBack={() => setShowCatalogue(false)}
        onAgentDeployed={() => {
          setShowCatalogue(false);
          fetchAgentsData();
        }}
      />
    );
  }

  if (isLoading) {
      return (
          <div className="flex h-full items-center justify-center">
              <Loader2 className="w-8 h-8 animate-spin text-primary" />
          </div>
      );
  }

  return (
    <div className="space-y-6 pb-6">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div>
            <h2 className="text-2xl font-bold text-text">AI Teammates</h2>
            <p className="text-textMuted text-sm mt-1">Manage autonomous agents deployed in this workspace.</p>
        </div>
        <button 
            ref={deployButtonRef}
            onClick={() => setIsDeployDialogOpen(true)}
            className="w-full sm:w-auto bg-primary hover:bg-primaryHover text-white px-4 py-2 rounded-md flex items-center justify-center gap-2 text-sm transition-colors shadow-lg shadow-primary/20 shrink-0"
        >
          <Plus className="w-4 h-4" /> Deploy Agent
        </button>
      </div>

      {agents.length === 0 ? (
        <div className="flex flex-col items-center justify-center p-12 border-2 border-dashed border-border rounded-lg text-textMuted bg-surface/50">
          <div className="w-16 h-16 bg-surfaceHighlight rounded-full flex items-center justify-center mb-4">
              <Bot className="w-8 h-8 text-textMuted" />
          </div>
          <p className="text-lg font-medium text-text">No agents deployed</p>
          <p className="text-sm">Create your first AI teammate to start automating tasks.</p>
          <button 
             onClick={() => setIsDeployDialogOpen(true)}
             className="mt-4 text-primary hover:underline text-sm"
          >
             Deploy now
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
          {agents.map(agent => (
            <AgentCard
              key={agent.id}
              agent={agent}
              allAgents={agents}
              openGuideId={openGuideId}
              onToggleGuide={(id) => setOpenGuideId(openGuideId === id ? null : id)}
              onDelete={setDeleteConfirmationId}
              onEdit={() => navigate(`/workspaces/${workspaceId}/agents/${agent.id}/edit`)}
              onChat={(agent) => setChatAgent(agent)}
            />
          ))}
        </div>
      )}

      {/* Agent Chat Modal */}
      {chatAgent && (
        <AgentChatModal
          agent={chatAgent}
          workspaceId={workspaceId!}
          onClose={() => setChatAgent(null)}
        />
      )}

      {/* Delete Confirmation Modal */}
      {deleteConfirmationId && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-sm rounded-xl shadow-2xl overflow-hidden p-6 space-y-4 animate-scale-in">
             <div className="flex items-center gap-3 text-red-500">
                <div className="w-10 h-10 rounded-full bg-red-500/10 flex items-center justify-center shrink-0">
                    <AlertTriangle className="w-5 h-5" />
                </div>
                <h3 className="text-lg font-bold text-text">Decommission Agent?</h3>
             </div>
             
             <p className="text-sm text-textMuted leading-relaxed">
                Are you sure you want to remove <span className="font-semibold text-text">{agents.find(a => a.id === deleteConfirmationId)?.name}</span>? This action cannot be undone.
             </p>

             <ModalErrorBanner error={deleteError} />

             <div className="flex gap-3 pt-2">
                <button 
                  onClick={cancelDelete}
                  className="flex-1 px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
                  disabled={isDeleting}
                >
                  Cancel
                </button>
                <button 
                  onClick={executeDeleteAction}
                  className="flex-1 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-md text-sm font-medium transition-colors flex items-center justify-center gap-2 shadow-lg shadow-red-500/20"
                  disabled={isDeleting}
                >
                  {isDeleting ? <Loader2 className="w-4 h-4 animate-spin" /> : deleteError ? 'Retry' : 'Confirm'}
                </button>
             </div>
          </div>
        </div>
      )}

      {/* Success Toast */}
      {successToast && (
        <Toast message={successToast} type="success" onClose={() => setSuccessToast(null)} />
      )}

      {/* Main Edit/Deploy Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-2 sm:p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-5xl rounded-xl shadow-2xl overflow-hidden animate-scale-in flex flex-col h-[95vh] lg:h-auto lg:max-h-[90vh]">
            {/* Modal Header */}
            <div className="px-4 sm:px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50 shrink-0">
              <div className="flex items-center gap-3">
                 <div className="w-10 h-10 bg-primary/10 rounded-lg flex items-center justify-center shrink-0">
                    <Pencil className="w-5 h-5 text-primary" />
                 </div>
                 <div className="min-w-0">
                    <div className="flex items-center gap-0">
                        <h3 className="text-base sm:text-lg font-bold text-text leading-tight truncate">
                            {'Edit Agent Authorization'}
                        </h3>
                        {isBuiltInEdit && (
                            <span className="text-[10px] bg-primary/10 border border-primary/20 text-primary px-2 py-0.5 rounded flex items-center gap-1 ml-2">
                                <Sparkles className="w-3 h-3" /> Built-In
                            </span>
                        )}
                    </div>
                    <p className="text-[10px] sm:text-xs text-textMuted mt-0.5 truncate">Configure identity, capabilities and tool access.</p>
                 </div>
              </div>
              <button onClick={() => setIsModalOpen(false)} className="p-1 hover:bg-surfaceHighlight rounded-full text-textMuted hover:text-text transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>
            
            {/* Form Content - Unified scroll on mobile, split on desktop */}
            <form onSubmit={handleSave} className="flex-1 flex flex-col lg:flex-row overflow-y-auto lg:overflow-hidden bg-background/30">
              
              {/* Tool Library Sidebar (Now on the Left) */}
              {!isBuiltInEdit && (
              <div className="w-full lg:w-80 p-4 sm:p-6 border-b lg:border-b-0 lg:border-r border-border lg:overflow-y-auto space-y-6 bg-surface shrink-0">
                 <div className="flex flex-col gap-3">
                    <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-2">
                        <Wrench className="w-3.5 h-3.5" /> Tool Library
                    </h4>
                    <div className="relative w-full">
                        <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-textMuted" />
                        <input 
                            type="text" 
                            placeholder="Search tools..."
                            value={toolSearch}
                            onChange={(e) => setToolSearch(e.target.value)}
                            className="bg-background border border-border rounded-full pl-8 pr-3 py-1.5 text-xs w-full focus:outline-none focus:border-primary transition-all shadow-sm"
                        />
                    </div>
                 </div>

                 <div className="space-y-3 pb-4">
                    {filteredTools.length === 0 ? (
                        <div className="py-12 text-center text-textMuted text-[10px] border-2 border-dashed border-border rounded-lg bg-surfaceHighlight/20 px-4">
                            No tools match your search criteria.
                        </div>
                    ) : (
                        filteredTools.map(tool => {
                            const selectedActionCount = tool.actions ? 
                                tool.actions.filter(action => formState.toolActionIds.includes(action.id)).length : 0;
                            const isSelected = formState.toolActionIds.includes(tool.id) || selectedActionCount > 0;
                            const totalActions = tool.actions?.length || 0;
                            return (
                                <div 
                                    key={tool.id}
                                    onClick={() => toggleTool(tool.id)}
                                    className={`p-3 border rounded-xl cursor-pointer transition-all duration-200 flex flex-col gap-1.5 relative group active:scale-95
                                        ${isSelected 
                                            ? 'bg-primary/5 border-primary shadow-[0_0_15px_rgba(99,102,241,0.05)]' 
                                            : 'bg-background/40 border-border hover:border-textMuted'
                                        }`}
                                >
                                    <div className="flex justify-between items-start">
                                        <div className={`w-8 h-8 rounded-lg flex items-center justify-center transition-colors shadow-sm
                                            ${isSelected ? 'bg-primary text-white' : 'bg-surfaceHighlight text-textMuted'}
                                        `}>
                                            <Sparkles className="w-4 h-4" />
                                        </div>
                                        {isSelected && (
                                            <div className="flex items-center gap-1">
                                                {totalActions > 0 ? (
                                                    <span className="text-xs font-bold text-primary bg-primary/10 px-1.5 py-0.5 rounded-full">
                                                        {selectedActionCount}/{totalActions}
                                                    </span>
                                                ) : (
                                                    <CheckCircle2 className="w-4 h-4 text-primary animate-in zoom-in duration-300" />
                                                )}
                                            </div>
                                        )}
                                    </div>
                                    <div>
                                        <h5 className={`font-semibold text-xs transition-colors ${isSelected ? 'text-text' : 'text-textMuted group-hover:text-text'}`}>
                                            {tool.name}
                                        </h5>
                                        <p className="text-[9px] text-textMuted line-clamp-2 mt-0.5">
                                            {tool.description}
                                        </p>
                                    </div>
                                    <div className="mt-1 flex items-center justify-between">
                                        <span className="text-[8px] uppercase font-bold tracking-tighter text-textMuted bg-surfaceHighlight/50 border border-border/30 px-1 py-0.5 rounded">
                                            {tool.category}
                                        </span>
                                    </div>
                                </div>
                            );
                        })
                    )}
                 </div>
              </div>
              )}

              {/* Identity & Logic Section (Now on the Right, Flex-1) */}
              <div className="flex-1 p-4 sm:p-6 lg:overflow-y-auto space-y-8 bg-surfaceHighlight/5">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                    <div className="space-y-6">
                        <div className="space-y-4">
                            <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-2">
                                <Briefcase className="w-3.5 h-3.5" /> Identity
                            </h4>
                            {isBuiltInEdit ? (
                                <>
                                    <LockedField label="Agent Name" value={formState.name} />
                                    <LockedField label="Role / Persona" value={formState.role} />
                                </>
                            ) : (
                                <>
                            <div className="space-y-1.5">
                                <label className="text-[10px] font-semibold text-textMuted uppercase">Agent Name</label>
                                <input 
                                type="text" 
                                value={formState.name}
                                onChange={(e) => setFormState({...formState, name: e.target.value})}
                                placeholder="e.g., QA Bot Beta"
                                className="w-full bg-surface border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                                autoFocus
                                />
                            </div>

                            <div className="space-y-1.5">
                                <label className="text-[10px] font-semibold text-textMuted uppercase">Role / Persona</label>
                                <input 
                                type="text" 
                                value={formState.role}
                                onChange={(e) => setFormState({...formState, role: e.target.value})}
                                placeholder="e.g., Frontend Specialist"
                                className="w-full bg-surface border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                                />
                            </div>
                                </>
                            )}

                            <div className="space-y-1.5">
                                <label className="text-[10px] font-semibold text-textMuted uppercase">LLM Model</label>
                                <select
                                    value={formState.selectedModel}
                                    onChange={(e) => setFormState({...formState, selectedModel: e.target.value})}
                                    className="w-full bg-surface border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                                >
                                    <option value="Default">Default</option>
                                    {availableModels.map(m => (
                                        <option key={m} value={m}>{m}</option>
                                    ))}
                                </select>
                            </div>
                        </div>

                        <div className="space-y-4 pt-6 border-t border-border/50">
                            <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-2">
                                <Brain className="w-3.5 h-3.5" /> {isBuiltInEdit ? 'Capabilities (Locked)' : 'Logical Boundary'}
                            </h4>
                            {isBuiltInEdit ? (
                                <div className="flex flex-wrap gap-1.5">
                                    {formState.capabilities.map(cap => (
                                        <span key={cap} className="text-[10px] bg-surfaceHighlight border border-border text-textMuted px-2 py-0.5 rounded">
                                            {cap}
                                        </span>
                                    ))}
                                </div>
                            ) : (
                            <div className="space-y-1.5">
                                <label className="text-[10px] font-semibold text-textMuted uppercase">Capabilities (Tags)</label>
                                <div className="w-full bg-surface border border-border rounded-md px-3 py-2 text-sm text-text focus-within:border-primary flex flex-wrap gap-1.5 min-h-[60px] content-start shadow-sm">
                                    {formState.capabilities.map(cap => (
                                        <span key={cap} className="bg-surfaceHighlight border border-border rounded px-2 py-0.5 text-[10px] flex items-center gap-1 animate-in fade-in slide-in-from-left-2">
                                            {cap}
                                            <button type="button" onClick={() => removeCapability(cap)} className="hover:text-red-400 p-0.5"><X className="w-2.5 h-2.5" /></button>
                                        </span>
                                    ))}
                                    <input 
                                        type="text" 
                                        value={formState.currentCapability}
                                        onChange={(e) => setFormState({...formState, currentCapability: e.target.value})}
                                        onKeyDown={handleAddCapability}
                                        placeholder="Add tag..."
                                        className="bg-transparent focus:outline-none min-w-[60px] flex-1 text-sm h-6"
                                    />
                                </div>
                            </div>
                            )}
                        </div>
                    </div>

                    <div className="space-y-4">
                        <div className="flex items-center justify-between">
                            <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-2">
                                <Bot className="w-3.5 h-3.5" /> {isReviewAgent ? 'Project Principles' : 'Custom Instructions'}
                            </h4>
                            {!isReviewAgent && (
                              <button
                                type="button"
                                onClick={() => setIsPreviewModalOpen(true)}
                                className="text-xs px-2 py-1 rounded flex items-center gap-1.5 transition-colors bg-surfaceHighlight text-textMuted hover:text-text hover:bg-surface"
                              >
                                <Eye className="w-3.5 h-3.5" />
                                Show Preview
                              </button>
                            )}
                        </div>
                        {isReviewAgent ? (
                          <div className="space-y-1.5">
                              <label className="text-[10px] font-semibold text-textMuted uppercase">Project Principles</label>
                              <p className="text-[10px] text-textMuted">These standards will be enforced by the code review agent.</p>
                              <textarea
                                value={formState.projectPrinciples}
                                onChange={(e) => setFormState({...formState, projectPrinciples: e.target.value})}
                                placeholder="We follow SOLID principles; all public methods must be documented; no magic numbers; avoid nested ternaries…"
                                className="w-full bg-surface border border-border rounded-md px-4 py-3 text-sm text-text focus:outline-none focus:border-primary h-full min-h-[300px] resize-none font-mono text-[12px] shadow-sm leading-relaxed"
                                required
                              />
                          </div>
                        ) : (
                          <div className="space-y-1.5">
                              <label className="text-[10px] font-semibold text-textMuted uppercase">Core Behavior & System Prompts</label>
                              <textarea
                                value={formState.customInstructions}
                                onChange={(e) => setFormState({...formState, customInstructions: e.target.value})}
                                placeholder="Define how the agent should think, reason, and interact with tools... (Markdown supported)"
                                className="w-full bg-surface border border-border rounded-md px-4 py-3 text-sm text-text focus:outline-none focus:border-primary h-full min-h-[300px] resize-none font-mono text-[12px] shadow-sm leading-relaxed"
                              />
                          </div>
                        )}
                    </div>
                </div>
              </div>
            </form>

            {/* Modal Footer - Fixed at bottom */}
            <div className="px-4 sm:px-6 py-4 border-t border-border bg-surfaceHighlight/30 flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 shrink-0">
                <div className="text-[11px] text-textMuted text-center sm:text-left font-medium">
                   <span className="font-bold text-primary bg-primary/10 px-2 py-0.5 rounded-full mr-1">{formState.toolActionIds.length}</span> tools authorized for this agent
                </div>
                <div className="flex gap-3 w-full sm:w-auto">
                    <button 
                        type="button" 
                        onClick={() => setIsModalOpen(false)}
                        className="flex-1 sm:flex-none px-6 py-2.5 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-all"
                        disabled={isSaving}
                    >
                        Cancel
                    </button>
                    <button 
                        onClick={handleSave}
                        disabled={
                          isBuiltInEdit
                            ? (!formState.projectPrinciples.trim() || isSaving)
                            : (!formState.name ||
                          !formState.role ||
                          (isReviewAgent ? !formState.projectPrinciples.trim() : !formState.customInstructions.trim()) ||
                          isSaving)
                        }
                        className="flex-1 sm:flex-none px-6 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-bold transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-primary/20 active:scale-95"
                    >
                        {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Save Changes'}
                    </button>
                </div>
            </div>
          </div>
        </div>
      )}


      {/* Markdown Preview Modal */}
      {isPreviewModalOpen && (
        <div className="fixed inset-0 z-[70] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-4xl rounded-xl shadow-2xl overflow-hidden animate-scale-in flex flex-col h-[90vh]">
            {/* Modal Header */}
            <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/30 shrink-0">
              <div className="flex items-center gap-3">
                <Eye className="w-5 h-5 text-primary" />
                <h3 className="text-lg font-bold text-text">Custom Instructions Preview</h3>
              </div>
              <button 
                onClick={() => setIsPreviewModalOpen(false)}
                className="p-1 hover:bg-surfaceHighlight rounded-full text-textMuted hover:text-text transition-colors"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            
            {/* Modal Content - Two Column */}
            <div className="flex-1 overflow-hidden flex">
              {/* Editor */}
              <div className="flex-1 flex flex-col border-r border-border p-6 overflow-hidden">
                <p className="text-xs font-bold text-textMuted uppercase tracking-widest mb-3">Editor</p>
                <textarea 
                  value={formState.customInstructions}
                  onChange={(e) => setFormState({...formState, customInstructions: e.target.value})}
                  placeholder="Define how the agent should think, reason, and interact with tools... (Markdown supported)"
                  className="flex-1 bg-background border border-border rounded-md px-4 py-3 text-sm text-text focus:outline-none focus:border-primary resize-none font-mono text-[12px] shadow-sm leading-relaxed custom-scrollbar"
                />
              </div>
              
              {/* Preview */}
              <div className="flex-1 flex flex-col p-6 bg-surfaceHighlight/30 overflow-hidden">
                <p className="text-xs font-bold text-textMuted uppercase tracking-widest mb-3 flex items-center gap-1">
                  <Eye className="w-3 h-3" /> Live Preview
                </p>
                <div className="flex-1 bg-surface border border-border rounded-md px-4 py-3 overflow-y-auto shadow-sm custom-scrollbar">
                  <MarkdownPreview content={formState.customInstructions} className="text-text" />
                </div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Tool Configuration Modal */}
      {isActionModalOpen && configuringToolId && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-2xl rounded-xl shadow-2xl overflow-hidden animate-scale-in flex flex-col h-[80vh] lg:h-auto lg:max-h-[80vh]">
            {/* Modal Header */}
            <div className="px-4 sm:px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50 shrink-0">
              <div className="flex items-center gap-3">
                 <div className="w-10 h-10 bg-primary/10 rounded-lg flex items-center justify-center shrink-0">
                    <Sparkles className="w-5 h-5 text-primary" />
                 </div>
                 <div className="min-w-0">
                    <h3 className="text-base sm:text-lg font-bold text-text leading-tight truncate">
                        Configure {availableTools.find(t => t.id === configuringToolId)?.name}
                    </h3>
                    <p className="text-[10px] sm:text-xs text-textMuted mt-0.5 truncate">Select specific actions to authorize</p>
                 </div>
              </div>
              <button onClick={cancelActionSelection} className="p-1 hover:bg-surfaceHighlight rounded-full text-textMuted hover:text-text transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>
            
            {/* Modal Content */}
            <div className="flex-1 overflow-y-auto p-4 sm:p-6 space-y-6">
              {/* Action List */}
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <h4 className="text-sm font-semibold text-text">Available Actions</h4>
                  <div className="flex gap-2">
                    <button 
                      onClick={() => setSelectedActionIds(availableTools.find(t => t.id === configuringToolId)?.actions?.map(a => a.id) || [])}
                      className="text-xs px-3 py-1.5 bg-primary/10 hover:bg-primary/20 text-primary rounded-md transition-colors"
                    >
                      Select All
                    </button>
                    <button 
                      onClick={() => setSelectedActionIds([])}
                      className="text-xs px-3 py-1.5 bg-surfaceHighlight hover:bg-surface text-textMuted hover:text-text rounded-md transition-colors"
                    >
                      Deselect All
                    </button>
                  </div>
                </div>
                
                <div className="space-y-3">
                  {availableTools.find(t => t.id === configuringToolId)?.actions?.map(action => (
                    <div key={action.id} className="flex items-start gap-3 p-3 border border-border rounded-lg hover:border-primary/50 transition-colors">
                      <input
                        type="checkbox"
                        id={action.id}
                        checked={selectedActionIds.includes(action.id)}
                        onChange={(e) => {
                          if (e.target.checked) {
                            setSelectedActionIds(prev => [...prev, action.id]);
                          } else {
                            setSelectedActionIds(prev => prev.filter(id => id !== action.id));
                          }
                        }}
                        className="mt-0.5 w-4 h-4 text-primary bg-surface border-border rounded focus:ring-primary focus:ring-2"
                      />
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <label htmlFor={action.id} className="text-sm font-medium text-text cursor-pointer">
                            {action.name}
                          </label>
                          {action.dangerLevel && (
                            <span 
                              className={`text-xs px-1.5 py-0.5 rounded-full font-medium cursor-help ${
                                action.dangerLevel.toLowerCase() === 'safe' ? 'bg-emerald-500/20 text-emerald-400' :
                                action.dangerLevel.toLowerCase() === 'moderate' ? 'bg-yellow-500/20 text-yellow-400' :
                                'bg-red-500/20 text-red-400'
                              }`}
                              title={
                                action.dangerLevel.toLowerCase() === 'safe' ? 'Safe: Read operations, no data modification' :
                                action.dangerLevel.toLowerCase() === 'moderate' ? 'Moderate: Create/update operations, reversible changes' :
                                'Destructive: Delete operations, permanent changes'
                              }
                            >
                              {action.dangerLevel.toLowerCase() === 'safe' ? '✓' : action.dangerLevel.toLowerCase() === 'moderate' ? '⚠️' : '🔥'}
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-textMuted leading-relaxed">
                          {action.description}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* Modal Footer */}
            <div className="px-4 sm:px-6 py-4 border-t border-border bg-surfaceHighlight/30 flex flex-col sm:flex-row sm:justify-between sm:items-center gap-4 shrink-0">
                <div className="text-[11px] text-textMuted text-center sm:text-left font-medium">
                   <span className="font-bold text-primary bg-primary/10 px-2 py-0.5 rounded-full mr-1">{selectedActionIds.length}</span> of <span className="font-bold text-text">{availableTools.find(t => t.id === configuringToolId)?.actions?.length || 0}</span> actions selected
                </div>
                <div className="flex gap-3 w-full sm:w-auto">
                    <button 
                        type="button" 
                        onClick={cancelActionSelection}
                        className="flex-1 sm:flex-none px-6 py-2.5 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-all"
                    >
                        Cancel
                    </button>
                    <button 
                        onClick={() => {
                          // Update formState.toolActionIds with selected action IDs
                          const tool = availableTools.find(t => t.id === configuringToolId);
                          if (tool) {
                            // Remove existing actions for this tool
                            const filteredIds = formState.toolActionIds.filter(id => 
                              !tool.actions!.some(action => action.id === id)
                            );
                            // Add selected actions
                            setFormState(prev => ({
                              ...prev,
                              toolActionIds: [...filteredIds, ...selectedActionIds]
                            }));
                          }
                          setIsActionModalOpen(false);
                          setConfiguringToolId(null);
                          setSelectedActionIds([]);
                        }}
                        className="flex-1 sm:flex-none px-6 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-bold transition-all flex items-center justify-center gap-2 shadow-lg shadow-primary/20 active:scale-95"
                    >
                        Confirm Selection
                    </button>
                </div>
            </div>
          </div>
        </div>
      )}

      <DeployMethodDialog
        isOpen={isDeployDialogOpen}
        onClose={() => {
          setIsDeployDialogOpen(false);
          deployButtonRef.current?.focus();
        }}
        onSelectScratch={() => {
          setIsDeployDialogOpen(false);
          navigate('new');
        }}
        onSelectBuiltIn={() => {
          setIsDeployDialogOpen(false);
          setShowCatalogue(true);
        }}
        onSelectCli={() => {
          setIsDeployDialogOpen(false);
          navigate('new-cli');
        }}
      />
    </div>
  );
};

export default AgentsList;
