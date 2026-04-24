import React, { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Loader2, AlertTriangle } from 'lucide-react';
import { Tool } from '../../types';
import { createAgent } from '../../services/agentService';
import { getTools } from '../../services/toolService';
import { fetchWorkspaceModels } from '../../services/workspaceService';
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

const initialFormState: FormState = {
  name: '',
  role: '',
  currentCapability: '',
  capabilities: [],
  toolActionIds: [],
  customInstructions: '',
  projectPrinciples: '',
  selectedModel: 'Default',
};

const REVIEW_ACTION_NAMES = ['review_pull_request', 'review_merge_request'];

const AgentCreatePage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  const [formState, setFormState] = useState<FormState>(initialFormState);
  const [isSaving, setIsSaving] = useState(false);
  const [availableTools, setAvailableTools] = useState<Tool[]>([]);
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [toolSearch, setToolSearch] = useState('');
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [configuringToolId, setConfiguringToolId] = useState<string | null>(null);
  const [selectedActionIds, setSelectedActionIds] = useState<string[]>([]);

  useEffect(() => {
    if (!workspaceId) return;
    getTools(workspaceId).then(setAvailableTools).catch(() => setAvailableTools([]));
    fetchWorkspaceModels(workspaceId).then(setAvailableModels).catch(() => setAvailableModels([]));
  }, [workspaceId]);

  const isReviewAgent = useMemo(() => {
    return availableTools.some(tool =>
      tool.actions?.some(action =>
        formState.toolActionIds.includes(action.id) &&
        REVIEW_ACTION_NAMES.includes(action.name)
      )
    );
  }, [formState.toolActionIds, availableTools]);

  const validateForm = (): Record<string, string> => {
    const errors: Record<string, string> = {};
    if (!formState.name.trim()) errors.name = 'Agent name is required.';
    else if (formState.name.trim().length > 200) errors.name = 'Name must be 200 characters or less.';
    if (!formState.role.trim()) errors.role = 'Agent role is required.';
    else if (formState.role.trim().length > 200) errors.role = 'Role must be 200 characters or less.';
    if (isReviewAgent && !formState.projectPrinciples.trim())
      errors.projectPrinciples = 'Project principles are required for review agents.';
    if (!isReviewAgent && !formState.customInstructions.trim())
      errors.customInstructions = 'Custom instructions are required.';
    return errors;
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    const errors = validateForm();
    if (Object.keys(errors).length > 0) { setValidationErrors(errors); return; }
    setIsSaving(true);
    setSubmitError(null);
    try {
      await createAgent(workspaceId!, {
        name: formState.name.trim(),
        role: formState.role.trim(),
        capabilities: formState.capabilities,
        toolActionIds: formState.toolActionIds,
        customInstructions: isReviewAgent ? undefined : formState.customInstructions.trim(),
        projectPrinciples: isReviewAgent ? formState.projectPrinciples.trim() : undefined,
        model: formState.selectedModel === 'Default' ? null : formState.selectedModel,
      });
      navigate(`/workspaces/${workspaceId}/agents`);
    } catch (error: any) {
      setSubmitError(error.message || 'Failed to create agent. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    navigate(`/workspaces/${workspaceId}/agents`);
  };

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
    const otherActionIds = formState.toolActionIds.filter(id =>
      !tool.actions?.some(a => a.id === id)
    );
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

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      <div className="bg-surface border border-border rounded-xl shadow-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-border flex justify-between items-center">
          <h1 className="text-2xl font-bold text-text">Create Agent</h1>
        </div>

        <form onSubmit={handleSave} className="p-6 space-y-6">
          {submitError && (
            <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
              <AlertTriangle className="w-4 h-4 shrink-0" />
              <span>{submitError}</span>
            </div>
          )}

          {/* Identity Section */}
          <section className="space-y-4">
            <h2 className="text-lg font-semibold text-text">Identity</h2>

            <div>
              <label htmlFor="agent-name" className="block text-sm font-medium text-text mb-1">Name</label>
              <input
                id="agent-name"
                type="text"
                value={formState.name}
                onChange={e => setFormState(prev => ({ ...prev, name: e.target.value }))}
                onFocus={() => clearFieldError('name')}
                className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                placeholder="e.g. Code Review Bot"
              />
              {validationErrors.name && <p className="text-red-400 text-xs mt-1">{validationErrors.name}</p>}
            </div>

            <div>
              <label htmlFor="agent-role" className="block text-sm font-medium text-text mb-1">Role</label>
              <input
                id="agent-role"
                type="text"
                value={formState.role}
                onChange={e => setFormState(prev => ({ ...prev, role: e.target.value }))}
                onFocus={() => clearFieldError('role')}
                className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                placeholder="e.g. Senior Developer"
              />
              {validationErrors.role && <p className="text-red-400 text-xs mt-1">{validationErrors.role}</p>}
            </div>

            <div>
              <label htmlFor="agent-model" className="block text-sm font-medium text-text mb-1">Model</label>
              <select
                id="agent-model"
                value={formState.selectedModel}
                onChange={e => setFormState(prev => ({ ...prev, selectedModel: e.target.value }))}
                className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
              >
                <option value="Default">Default</option>
                {availableModels.map(m => <option key={m} value={m}>{m}</option>)}
              </select>
            </div>
          </section>

          <AgentFormCapabilities
            currentCapability={formState.currentCapability}
            capabilities={formState.capabilities}
            onCurrentCapabilityChange={value => setFormState(prev => ({ ...prev, currentCapability: value }))}
            onAddCapability={handleAddCapability}
            onRemoveCapability={removeCapability}
          />

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

          {/* Instructions Section */}
          <section className="space-y-4">
            <h2 className="text-lg font-semibold text-text">Instructions</h2>
            {isReviewAgent ? (
              <div>
                <label htmlFor="project-principles" className="block text-sm font-medium text-text mb-1">Project Principles</label>
                <textarea
                  id="project-principles"
                  value={formState.projectPrinciples}
                  onChange={e => setFormState(prev => ({ ...prev, projectPrinciples: e.target.value }))}
                  onFocus={() => clearFieldError('projectPrinciples')}
                  rows={6}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-y"
                  placeholder="Define the coding standards and review principles..."
                />
                {validationErrors.projectPrinciples && (
                  <p className="text-red-400 text-xs mt-1">{validationErrors.projectPrinciples}</p>
                )}
              </div>
            ) : (
              <div>
                <label htmlFor="custom-instructions" className="block text-sm font-medium text-text mb-1">Custom Instructions</label>
                <textarea
                  id="custom-instructions"
                  value={formState.customInstructions}
                  onChange={e => setFormState(prev => ({ ...prev, customInstructions: e.target.value }))}
                  onFocus={() => clearFieldError('customInstructions')}
                  rows={6}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-y"
                  placeholder="Describe the agent's behavior and guidelines..."
                />
                {validationErrors.customInstructions && (
                  <p className="text-red-400 text-xs mt-1">{validationErrors.customInstructions}</p>
                )}
              </div>
            )}
          </section>

          {/* Form Actions */}
          <div className="flex justify-end gap-3 pt-4 border-t border-border">
            <button
              type="button"
              onClick={handleCancel}
              disabled={isSaving}
              className="px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="px-6 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2 shadow-lg shadow-primary/20"
            >
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isSaving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AgentCreatePage;
