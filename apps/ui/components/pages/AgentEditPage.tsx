import React, { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Loader2, AlertTriangle, Info, Plus, X } from 'lucide-react';
import { Agent, Skill, Tool } from '../../types';
import { getAgent, updateAgent, saveAgentToolAssignments, getAgents } from '../../services/agentService';
import { getTools } from '../../services/toolService';
import { fetchWorkspaceModels } from '../../services/workspaceService';
import { getSkills } from '../../services/skillService';
import Toast from '../Toast';
import LockedField from '../agents/LockedField';
import AgentFormCapabilities from '../agents/AgentFormCapabilities';
import AgentToolSummarySection from '../agents/AgentToolSummarySection';
import AddToolsModal from '../agents/AddToolsModal';
import AddSubAgentsModal from '../agents/AddSubAgentsModal';
import AddSkillsModal from '../skills/AddSkillsModal';
import MarkdownPreviewToggle from '../agents/MarkdownPreviewToggle';
import { getMcpServers } from '../../services/mcpServerService';
import { useAgentMcpAssignments } from '../../hooks/useAgentMcpAssignments';
import { McpServer, McpToolSelection, ToolCatalogueEntry } from '../../types';

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
  const [isAddToolsModalOpen, setIsAddToolsModalOpen] = useState(false);
  const [openAtSourceId, setOpenAtSourceId] = useState<string | null>(null);
  const [mcpServers, setMcpServers] = useState<McpServer[]>([]);
  const [mcpSelections, setMcpSelections] = useState<McpToolSelection[]>([]);
  const [allAgents, setAllAgents] = useState<Agent[]>([]);
  const [selectedSubAgentIds, setSelectedSubAgentIds] = useState<string[]>([]);
  const [isSubAgentsModalOpen, setIsSubAgentsModalOpen] = useState(false);
  const [availableSkills, setAvailableSkills] = useState<Skill[]>([]);
  const [selectedSkillIds, setSelectedSkillIds] = useState<string[]>([]);
  const [isSkillsModalOpen, setIsSkillsModalOpen] = useState(false);

  const { assignments: mcpAssignments } = useAgentMcpAssignments(agentId, !!agentId);

  useEffect(() => {
    if (!workspaceId || !agentId) return;
    loadAgentData();
    getTools(workspaceId).then(setAvailableTools).catch(() => setAvailableTools([]));
    fetchWorkspaceModels(workspaceId).then(setAvailableModels).catch(() => setAvailableModels([]));
    getMcpServers(workspaceId).then(setMcpServers).catch(() => setMcpServers([]));
    getAgents(workspaceId).then(setAllAgents).catch(() => setAllAgents([]));
    getSkills(workspaceId).then(setAvailableSkills).catch(() => setAvailableSkills([]));
  }, [workspaceId, agentId]);

  useEffect(() => {
    if (Object.keys(mcpAssignments).length > 0) {
      setMcpSelections(
        Object.entries(mcpAssignments)
          .filter(([, tools]) => tools.length > 0)
          .map(([serverId, toolNames]) => ({ mcpServerId: serverId, toolNames }))
      );
    }
  }, [mcpAssignments]);

  const loadAgentData = async () => {
    setIsLoading(true);
    try {
      const loaded = await getAgent(agentId!);
      setAgent(loaded);
      setSelectedSubAgentIds(loaded.subAgentIds ?? []);
      setSelectedSkillIds((loaded.skills ?? []).map((s: Skill) => s.id));
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

  const toolCatalogue = useMemo<ToolCatalogueEntry[]>(
    () =>
      availableTools.flatMap(tool =>
        (tool.actions ?? []).map(action => ({
          actionId: action.id,
          actionName: action.name,
          actionDescription: action.description,
          dangerLevel: action.dangerLevel ?? 'Safe',
          sourceId: tool.id,
          sourceName: tool.name,
          sourceType: tool.source ?? 'native',
        }))
      ),
    [availableTools]
  );

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
    const payload: Partial<Agent> = {};

    // Only include fields if they've changed (to avoid locked field violations on built-in agents)
    if (!isBuiltIn && formState.name.trim() !== agent?.name) {
      payload.name = formState.name.trim();
    }

    // For built-in agents, don't include role, capabilities, or toolActionIds
    // (they're locked fields). For custom agents, include them if changed.
    if (!isBuiltIn) {
      if (formState.role.trim() !== agent?.role) {
        payload.role = formState.role.trim();
      }
      if (JSON.stringify(formState.capabilities.sort()) !== JSON.stringify((agent?.capabilities || []).sort())) {
        payload.capabilities = formState.capabilities;
      }
      if (JSON.stringify(formState.toolActionIds.sort()) !== JSON.stringify((agent?.toolActionIds || []).sort())) {
        payload.toolActionIds = formState.toolActionIds;
      }
    }

    // Sub-agents
    if (JSON.stringify(selectedSubAgentIds.sort()) !== JSON.stringify((agent?.subAgentIds || []).sort())) {
      payload.subAgentIds = selectedSubAgentIds;
    }

    // Skills
    const originalSkillIds = (agent?.skills ?? []).map(s => s.id).sort();
    if (JSON.stringify([...selectedSkillIds].sort()) !== JSON.stringify(originalSkillIds)) {
      payload.skillIds = selectedSkillIds;
    }

    // Instructions
    if (isReviewAgent) {
      if (formState.projectPrinciples.trim() !== (agent?.projectPrinciples || '')) {
        payload.projectPrinciples = formState.projectPrinciples.trim();
      }
    } else {
      if (formState.customInstructions.trim() !== (agent?.customInstructions || '')) {
        payload.customInstructions = formState.customInstructions.trim();
      }
    }

    // Model - always include if changed
    const newModel = formState.selectedModel === 'Default' ? null : formState.selectedModel;
    const originalModel = agent?.model || null;
    if (newModel !== originalModel) {
      payload.model = newModel;
    }

    return payload;
  };

  const handleOpenModal = (sourceId?: string | null) => {
    setOpenAtSourceId(sourceId ?? null);
    setIsAddToolsModalOpen(true);
  };

  const handleCommit = async (ids: string[], newMcpSelections: McpToolSelection[]) => {
    setFormState(prev => ({ ...prev, toolActionIds: ids }));
    setIsAddToolsModalOpen(false);
    setMcpSelections(newMcpSelections.filter(s => s.toolNames.length > 0));
    if (agentId) {
      try {
        await saveAgentToolAssignments(agentId, { nativeToolActionIds: ids, mcpSelections: newMcpSelections });
        setToast({ message: `Tools saved for ${agent?.name ?? 'agent'}.`, type: 'success' });
      } catch {
        setToast({ message: 'Failed to save tool assignments.', type: 'error' });
      }
    }
  };

  const handleDiscard = () => setIsAddToolsModalOpen(false);

  const handleRemoveSource = (sourceId: string) => {
    const idsToRemove = new Set(
      toolCatalogue.filter(e => e.sourceId === sourceId).map(e => e.actionId)
    );
    setFormState(prev => ({
      ...prev,
      toolActionIds: prev.toolActionIds.filter(id => !idsToRemove.has(id)),
    }));
  };

  const handleRemoveMcpServer = (serverId: string) => {
    setMcpSelections(prev => prev.filter(s => s.mcpServerId !== serverId));
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
            <AgentToolSummarySection
              toolActionIds={formState.toolActionIds}
              toolCatalogue={toolCatalogue}
              mcpServers={mcpServers}
              mcpSelections={mcpSelections}
              onOpenModal={handleOpenModal}
              onRemoveSource={handleRemoveSource}
              onRemoveMcpServer={handleRemoveMcpServer}
            />
          )}

          {/* Sub-Agents Section */}
          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-text">Sub-Agents</h2>
            {selectedSubAgentIds.length > 0 && (
              <div className="space-y-2">
                {selectedSubAgentIds.map(id => {
                  const subAgent = allAgents.find(a => a.id === id);
                  if (!subAgent) return null;
                  return (
                    <div key={id} className="flex items-center gap-3 px-3 py-2 bg-surfaceHighlight border border-border rounded-lg">
                      <img src={subAgent.avatarUrl} alt={subAgent.name} className="w-8 h-8 rounded-full border border-border object-cover flex-shrink-0" />
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-text truncate">{subAgent.name}</p>
                        <p className="text-xs text-textMuted truncate">{subAgent.role}</p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setSelectedSubAgentIds(prev => prev.filter(i => i !== id))}
                        className="text-textMuted hover:text-red-400 transition-colors p-1 rounded hover:bg-red-500/10 flex-shrink-0"
                        aria-label={`Remove ${subAgent.name}`}
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  );
                })}
              </div>
            )}
            <button
              type="button"
              onClick={() => setIsSubAgentsModalOpen(true)}
              className="flex items-center gap-2 px-3 py-2 border border-dashed border-border rounded-lg text-sm text-textMuted hover:text-primary hover:border-primary/50 transition-colors"
            >
              <Plus className="w-4 h-4" />
              Add Sub-Agent
            </button>
          </section>

          {/* Skills Section */}
          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-text">Skills</h2>
            {selectedSkillIds.length > 0 && (
              <div className="space-y-2">
                {selectedSkillIds.map(id => {
                  const skill = availableSkills.find(s => s.id === id);
                  if (!skill) return null;
                  return (
                    <div key={id} className="flex items-center gap-3 px-3 py-2 bg-surfaceHighlight border border-border rounded-lg">
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-text truncate">{skill.name}</p>
                        <p className="text-xs text-textMuted truncate">{skill.description}</p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setSelectedSkillIds(prev => prev.filter(i => i !== id))}
                        className="text-textMuted hover:text-red-400 transition-colors p-1 rounded hover:bg-red-500/10 flex-shrink-0"
                        aria-label={`Remove ${skill.name}`}
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  );
                })}
              </div>
            )}
            <button
              type="button"
              onClick={() => setIsSkillsModalOpen(true)}
              className="flex items-center gap-2 px-3 py-2 border border-dashed border-border rounded-lg text-sm text-textMuted hover:text-primary hover:border-primary/50 transition-colors"
            >
              <Plus className="w-4 h-4" />
              Add Skill
            </button>
          </section>

          {/* Instructions Section */}
          <section className="space-y-4">
            {isReviewAgent ? (
              <div>
                <label htmlFor="project-principles" className="block text-sm font-medium text-text mb-1">Project Principles</label>
                <textarea id="project-principles" value={formState.projectPrinciples}
                  onChange={e => setFormState(prev => ({ ...prev, projectPrinciples: e.target.value }))}
                  onFocus={() => clearFieldError('projectPrinciples')} rows={6}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-y"
                  placeholder="Define the coding standards and review principles..." />
                <p className="text-textMuted text-xs mt-1">{formState.projectPrinciples.length} characters</p>
                {validationErrors.projectPrinciples && <p className="text-red-400 text-xs mt-1">{validationErrors.projectPrinciples}</p>}
              </div>
            ) : (
              <div>
                <label htmlFor="custom-instructions" className="block text-sm font-medium text-text mb-1">Custom Instructions</label>
                <MarkdownPreviewToggle id="custom-instructions" value={formState.customInstructions}
                  onChange={value => setFormState(prev => ({ ...prev, customInstructions: value }))}
                  onFocus={() => clearFieldError('customInstructions')} rows={6}
                  placeholder="Describe the agent's behavior and guidelines..." />
                <p className="text-textMuted text-xs mt-1">{formState.customInstructions.length} characters</p>
                {validationErrors.customInstructions && <p className="text-red-400 text-xs mt-1">{validationErrors.customInstructions}</p>}
              </div>
            )}
          </section>

          <AddToolsModal
            isOpen={isAddToolsModalOpen}
            agentId={agentId}
            initialToolActionIds={formState.toolActionIds}
            toolCatalogue={toolCatalogue}
            workspaceId={workspaceId!}
            onCommit={handleCommit}
            onDiscard={handleDiscard}
            openAtSource={openAtSourceId}
            initialMcpSelections={mcpSelections}
          />

          <AddSubAgentsModal
            isOpen={isSubAgentsModalOpen}
            allAgents={allAgents}
            excludeAgentId={agentId}
            alreadySelectedIds={selectedSubAgentIds}
            onCommit={ids => { setSelectedSubAgentIds(ids); setIsSubAgentsModalOpen(false); }}
            onDiscard={() => setIsSubAgentsModalOpen(false)}
          />

          <AddSkillsModal
            isOpen={isSkillsModalOpen}
            allSkills={availableSkills}
            alreadySelectedIds={selectedSkillIds}
            onCommit={ids => { setSelectedSkillIds(ids); setIsSkillsModalOpen(false); }}
            onDiscard={() => setIsSkillsModalOpen(false)}
          />

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
