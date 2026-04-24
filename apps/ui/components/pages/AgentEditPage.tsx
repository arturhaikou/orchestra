import React, { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Loader2, AlertTriangle, Info } from 'lucide-react';
import { Agent, Tool } from '../../types';
import { getAgent, updateAgent } from '../../services/agentService';
import { getTools } from '../../services/toolService';
import { fetchWorkspaceModels } from '../../services/workspaceService';
import Toast from '../Toast';
import LockedField from '../agents/LockedField';
import AgentFormCapabilities from '../agents/AgentFormCapabilities';
import AgentFormToolAuthorization, { ActionModalState } from '../agents/AgentFormToolAuthorization';

interface FormState {
  name: string;
  role: string;
  currentCapability: string;
  capabilities: string[];
  toolActionIds: string[];
  customInstructions: string;
  projectPrinciples: string;
  selectedModel: string;
}

const REVIEW_ACTION_NAMES = ['review_pull_request', 'review_merge_request'];

const AgentEditPage: React.FC = () => {
  const { workspaceId, agentId } = useParams<{ workspaceId: string; agentId: string }>();
  const navigate = useNavigate();

  const [agent, setAgent] = useState<Agent | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [formState, setFormState] = useState<FormState>({
    name: '', role: '', currentCapability: '', capabilities: [],
    toolActionIds: [], customInstructions: '', projectPrinciples: '', selectedModel: 'Default',
  });
  const [availableTools, setAvailableTools] = useState<Tool[]>([]);
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [toolSearch, setToolSearch] = useState('');
  const [configuringToolId, setConfiguringToolId] = useState<string | null>(null);
  const [selectedActionIds, setSelectedActionIds] = useState<string[]>([]);

  useEffect(() => {
    if (!workspaceId || !agentId) return;
    loadAgentData();
    getTools(workspaceId).then(setAvailableTools).catch(() => setAvailableTools([]));
    fetchWorkspaceModels(workspaceId).then(setAvailableModels).catch(() => setAvailableModels([]));
  }, [workspaceId, agentId]);

  const loadAgentData = async () => {
    setIsLoading(true);
    try {
      const loaded = await getAgent(agentId!);
      setAgent(loaded);
      setFormState({
        name: loaded.name,
        role: loaded.role,
        currentCapability: '',
        capabilities: [...loaded.capabilities],
        toolActionIds: [...loaded.toolActionIds],
        customInstructions: loaded.customInstructions || '',
        projectPrinciples: loaded.projectPrinciples || '',
        selectedModel: loaded.model || 'Default',
      });
    } catch {
      setLoadError('Agent not found');
    } finally {
      setIsLoading(false);
    }
  };

  const isBuiltIn = agent?.isBuiltIn === true;

  const isReviewAgent = useMemo(() => {
    const hasReviewTools = availableTools.some(tool =>
      tool.actions?.some(action =>
        formState.toolActionIds.includes(action.id) &&
        REVIEW_ACTION_NAMES.includes(action.name)
      )
    );
    const hasExistingPrinciples = !!agent?.projectPrinciples;
    return hasReviewTools || hasExistingPrinciples;
  }, [formState.toolActionIds, availableTools, agent]);

  const validateForm = (): Record<string, string> => {
    const errors: Record<string, string> = {};
    if (!isBuiltIn) {
      if (!formState.role.trim()) errors.role = 'Agent role is required.';
    }
    if (isReviewAgent && !formState.projectPrinciples.trim())
      errors.projectPrinciples = 'Project principles are required for review agents.';
    return errors;
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    const errors = validateForm();
    if (Object.keys(errors).length > 0) { setValidationErrors(errors); return; }
    setIsSaving(true);
    setToast(null);
    try {
      const payload: Partial<Agent> = buildUpdatePayload();
      await updateAgent(agentId!, payload);
      setToast({ message: 'Agent updated successfully', type: 'success' });
      navigate(`/workspaces/${workspaceId}/agents`);
    } catch (error: any) {
      setToast({ message: error.message || 'Failed to update agent', type: 'error' });
    } finally {
      setIsSaving(false);
    }
  };

  const buildUpdatePayload = (): Partial<Agent> => {
    const payload: Partial<Agent> = {
      role: formState.role.trim(),
      capabilities: formState.capabilities,
      toolActionIds: formState.toolActionIds,
    };
    if (!isBuiltIn) {
      payload.name = formState.name.trim();
    }
    if (isReviewAgent) {
      payload.projectPrinciples = formState.projectPrinciples.trim();
    } else {
      payload.customInstructions = formState.customInstructions.trim();
    }
    if (formState.selectedModel === 'Default') {
      payload.model = null;
    } else {
      payload.model = formState.selectedModel;
    }
    return payload;
  };

  const handleCancel = () => navigate(`/workspaces/${workspaceId}/agents`);

  const clearFieldError = (field: string) => {
    setValidationErrors(prev => {
      const next = { ...prev };
      delete next[field];
      return next;
    });
  };

  const handleAddCapability = (e: React.KeyboardEvent) => {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    const cap = formState.currentCapability.trim();
    if (cap && !formState.capabilities.includes(cap)) {
      setFormState(prev => ({ ...prev, capabilities: [...prev.capabilities, cap], currentCapability: '' }));
    }
  };

  const removeCapability = (cap: string) => {
    setFormState(prev => ({ ...prev, capabilities: prev.capabilities.filter(c => c !== cap) }));
  };

  const toggleTool = (toolId: string) => {
    const tool = availableTools.find(t => t.id === toolId);
    if (tool?.actions && tool.actions.length > 0) {
      setConfiguringToolId(toolId);
      setSelectedActionIds(formState.toolActionIds.filter(id => tool.actions!.some(a => a.id === id)));
    } else {
      setFormState(prev => ({
        ...prev,
        toolActionIds: prev.toolActionIds.includes(toolId)
          ? prev.toolActionIds.filter(id => id !== toolId)
          : [...prev.toolActionIds, toolId],
      }));
    }
  };

  const confirmActionSelection = () => {
    const tool = availableTools.find(t => t.id === configuringToolId);
    if (!tool) return;
    const otherActionIds = formState.toolActionIds.filter(id => !tool.actions?.some(a => a.id === id));
    setFormState(prev => ({ ...prev, toolActionIds: [...otherActionIds, ...selectedActionIds] }));
    setConfiguringToolId(null);
    setSelectedActionIds([]);
  };

  const cancelActionSelection = () => {
    setConfiguringToolId(null);
    setSelectedActionIds([]);
  };

  const toggleActionId = (actionId: string) => {
    setSelectedActionIds(prev =>
      prev.includes(actionId) ? prev.filter(id => id !== actionId) : [...prev, actionId]
    );
  };

  const actionModalState: ActionModalState | null = configuringToolId
    ? {
        toolName: availableTools.find(t => t.id === configuringToolId)?.name ?? '',
        actions: availableTools.find(t => t.id === configuringToolId)?.actions ?? [],
        selectedActionIds,
      }
    : null;

  const filteredTools = availableTools.filter(tool =>
    tool.name.toLowerCase().includes(toolSearch.toLowerCase()) ||
    tool.description.toLowerCase().includes(toolSearch.toLowerCase())
  );

  const isToolSelected = (tool: Tool) => {
    if (tool.actions && tool.actions.length > 0) {
      return tool.actions.some(a => formState.toolActionIds.includes(a.id));
    }
    return formState.toolActionIds.includes(tool.id);
  };

  if (isLoading) {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex items-center justify-center py-20" data-testid="agent-edit-loading" role="status">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
        </div>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex flex-col items-center justify-center py-20 space-y-4">
          <AlertTriangle className="w-12 h-12 text-yellow-500" />
          <h2 className="text-xl font-bold text-text">Agent Not Found</h2>
          <Link to={`/workspaces/${workspaceId}/agents`} className="text-primary hover:underline">
            Return to Agents List
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
      <div className="bg-surface border border-border rounded-xl shadow-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-border flex justify-between items-center">
          <h1 className="text-2xl font-bold text-text">Edit Agent</h1>
        </div>

        <form onSubmit={handleSave} className="p-6 space-y-6">
          {isBuiltIn && (
            <div className="flex items-center gap-2 p-3 bg-yellow-500/10 border border-yellow-500/20 rounded-lg text-yellow-400 text-sm">
              <Info className="w-4 h-4 shrink-0" />
              <span>This is a built-in agent. Some fields cannot be modified.</span>
            </div>
          )}

          <section className="space-y-4">
            <h2 className="text-lg font-semibold text-text">Identity</h2>
            {isBuiltIn ? (
              <LockedField label="Name" value={formState.name} />
            ) : (
              <div>
                <label htmlFor="agent-name" className="block text-sm font-medium text-text mb-1">Name</label>
                <input id="agent-name" type="text" value={formState.name}
                  onChange={e => setFormState(prev => ({ ...prev, name: e.target.value }))}
                  onFocus={() => clearFieldError('name')}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
                {validationErrors.name && <p className="text-red-400 text-xs mt-1">{validationErrors.name}</p>}
              </div>
            )}
            {isBuiltIn ? (
              <LockedField label="Role" value={formState.role} />
            ) : (
              <div>
                <label htmlFor="agent-role" className="block text-sm font-medium text-text mb-1">Role</label>
                <input id="agent-role" type="text" value={formState.role}
                  onChange={e => setFormState(prev => ({ ...prev, role: e.target.value }))}
                  onFocus={() => clearFieldError('role')}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary" />
                {validationErrors.role && <p className="text-red-400 text-xs mt-1">{validationErrors.role}</p>}
              </div>
            )}
            <div>
              <label htmlFor="agent-model" className="block text-sm font-medium text-text mb-1">Model</label>
              <select id="agent-model" value={formState.selectedModel}
                onChange={e => setFormState(prev => ({ ...prev, selectedModel: e.target.value }))}
                className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary">
                <option value="Default">Default</option>
                {availableModels.map(m => <option key={m} value={m}>{m}</option>)}
              </select>
            </div>
          </section>

          {isBuiltIn ? (
            <section className="space-y-4">
              <h2 className="text-lg font-semibold text-text">Capabilities</h2>
              <div className="flex flex-wrap gap-2">
                {formState.capabilities.map(cap => (
                  <LockedField key={cap} label="" value={cap} />
                ))}
              </div>
            </section>
          ) : (
            <AgentFormCapabilities
              currentCapability={formState.currentCapability}
              capabilities={formState.capabilities}
              onCurrentCapabilityChange={value => setFormState(prev => ({ ...prev, currentCapability: value }))}
              onAddCapability={handleAddCapability}
              onRemoveCapability={removeCapability}
            />
          )}

          {!isBuiltIn && (
            <AgentFormToolAuthorization
              toolSearch={toolSearch}
              onToolSearchChange={setToolSearch}
              filteredTools={filteredTools}
              isToolSelected={isToolSelected}
              onToggleTool={toggleTool}
              actionModal={actionModalState}
              onToggleActionId={toggleActionId}
              onConfirmActions={confirmActionSelection}
              onCancelActions={cancelActionSelection}
            />
          )}

          <section className="space-y-4">
            <h2 className="text-lg font-semibold text-text">Instructions</h2>
            {isReviewAgent ? (
              <div>
                <label htmlFor="project-principles" className="block text-sm font-medium text-text mb-1">Project Principles</label>
                <textarea id="project-principles" value={formState.projectPrinciples}
                  onChange={e => setFormState(prev => ({ ...prev, projectPrinciples: e.target.value }))}
                  onFocus={() => clearFieldError('projectPrinciples')} rows={6}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-y"
                  placeholder="Define the coding standards and review principles..." />
                {validationErrors.projectPrinciples && <p className="text-red-400 text-xs mt-1">{validationErrors.projectPrinciples}</p>}
              </div>
            ) : (
              <div>
                <label htmlFor="custom-instructions" className="block text-sm font-medium text-text mb-1">Custom Instructions</label>
                <textarea id="custom-instructions" value={formState.customInstructions}
                  onChange={e => setFormState(prev => ({ ...prev, customInstructions: e.target.value }))}
                  onFocus={() => clearFieldError('customInstructions')} rows={6}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-y"
                  placeholder="Describe the agent's behavior and guidelines..." />
                {validationErrors.customInstructions && <p className="text-red-400 text-xs mt-1">{validationErrors.customInstructions}</p>}
              </div>
            )}
          </section>

          <div className="flex justify-end gap-3 pt-4 border-t border-border">
            <button type="button" onClick={handleCancel} disabled={isSaving}
              className="px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors">
              Cancel
            </button>
            <button type="submit" disabled={isSaving}
              className="px-6 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2 shadow-lg shadow-primary/20">
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isSaving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AgentEditPage;
