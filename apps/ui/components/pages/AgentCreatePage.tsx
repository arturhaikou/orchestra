import React, { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Loader2, AlertTriangle, Plus, X } from 'lucide-react';
import { Agent, Skill, SkillFolder, Tool } from '../../types';
import { createAgent, getAgents } from '../../services/agentService';
import { getTools } from '../../services/toolService';
import { fetchWorkspaceModels } from '../../services/workspaceService';
import { getSkills } from '../../services/skillService';
import { getSkillFolders } from '../../services/skillFolderService';
import AgentFormCapabilities from '../agents/AgentFormCapabilities';
import AgentToolSummarySection from '../agents/AgentToolSummarySection';
import AddToolsModal from '../agents/AddToolsModal';
import AddSubAgentsModal from '../agents/AddSubAgentsModal';
import AddSkillsModal from '../skills/AddSkillsModal';
import MarkdownPreviewToggle from '../agents/MarkdownPreviewToggle';
import { getMcpServers } from '../../services/mcpServerService';
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
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);
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
  const [availableSkillFolders, setAvailableSkillFolders] = useState<SkillFolder[]>([]);
  const [selectedSkillFolderIds, setSelectedSkillFolderIds] = useState<string[]>([]);

  useEffect(() => {
    if (!workspaceId) return;
    getTools(workspaceId).then(setAvailableTools).catch(() => setAvailableTools([]));
    fetchWorkspaceModels(workspaceId).then(setAvailableModels).catch(() => setAvailableModels([]));
    getMcpServers(workspaceId).then(setMcpServers).catch(() => setMcpServers([]));
    getAgents(workspaceId).then(setAllAgents).catch(() => setAllAgents([]));
    getSkills(workspaceId).then(setAvailableSkills).catch(() => setAvailableSkills([]));
    getSkillFolders(workspaceId).then(setAvailableSkillFolders).catch(() => setAvailableSkillFolders([]));
  }, [workspaceId]);

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
        mcpSelections: mcpSelections,
        customInstructions: isReviewAgent ? undefined : formState.customInstructions.trim(),
        projectPrinciples: isReviewAgent ? formState.projectPrinciples.trim() : undefined,
        model: formState.selectedModel === 'Default' ? null : formState.selectedModel,
        subAgentIds: selectedSubAgentIds,
        skillIds: selectedSkillIds,
        skillFolderIds: selectedSkillFolderIds,
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

  const handleOpenModal = (sourceId?: string | null) => {
    setOpenAtSourceId(sourceId ?? null);
    setIsAddToolsModalOpen(true);
  };

  const handleCommit = (ids: string[], newMcpSelections: McpToolSelection[]) => {
    setFormState(prev => ({ ...prev, toolActionIds: ids }));
    setMcpSelections(newMcpSelections.filter(s => s.toolNames.length > 0));
    setIsAddToolsModalOpen(false);
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

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      <div className="bg-surface border border-border rounded-xl shadow-xl shadow-primary/5 overflow-hidden">
        <div className="px-6 py-4 border-b border-border-elevated flex justify-between items-center">
          <h1 className="text-2xl font-bold bg-gradient-to-r from-text to-textMuted bg-clip-text text-transparent">Create Agent</h1>
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

          <AgentToolSummarySection
            toolActionIds={formState.toolActionIds}
            toolCatalogue={toolCatalogue}
            mcpServers={mcpServers}
            mcpSelections={mcpSelections}
            onOpenModal={handleOpenModal}
            onRemoveSource={handleRemoveSource}
            onRemoveMcpServer={handleRemoveMcpServer}
          />

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

          {/* Skill Folders Section */}
          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-text">Skill Folders</h2>
            {selectedSkillFolderIds.length > 0 && (
              <div className="space-y-2">
                {selectedSkillFolderIds.map(id => {
                  const folder = availableSkillFolders.find(f => f.id === id);
                  if (!folder) return null;
                  return (
                    <div key={id} className="flex items-center gap-3 px-3 py-2 bg-surfaceHighlight border border-border rounded-lg">
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-text truncate">{folder.name}</p>
                        <p className="text-xs text-textMuted font-mono truncate">{folder.folderPath}</p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setSelectedSkillFolderIds(prev => prev.filter(i => i !== id))}
                        className="text-textMuted hover:text-red-400 transition-colors p-1 rounded hover:bg-red-500/10 flex-shrink-0"
                        aria-label={`Remove ${folder.name}`}
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                  );
                })}
              </div>
            )}
            {availableSkillFolders.length > 0 && (
              <div className="space-y-1">
                {availableSkillFolders
                  .filter(f => !selectedSkillFolderIds.includes(f.id))
                  .map(folder => (
                    <button
                      key={folder.id}
                      type="button"
                      onClick={() => setSelectedSkillFolderIds(prev => [...prev, folder.id])}
                      className="flex items-center gap-2 w-full px-3 py-2 border border-dashed border-border rounded-lg text-sm text-textMuted hover:text-primary hover:border-primary/50 transition-colors text-left"
                    >
                      <Plus className="w-4 h-4 shrink-0" />
                      <span className="truncate">{folder.name}</span>
                    </button>
                  ))
                }
              </div>
            )}
            {availableSkillFolders.length === 0 && (
              <p className="text-xs text-textMuted italic">No skill folders registered yet. <a href={`/workspaces/${workspaceId}/skill-folders/new`} className="text-primary hover:underline">Register one</a>.</p>
            )}
          </section>

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
                <p className="text-textMuted text-xs mt-1">{formState.projectPrinciples.length} characters</p>
                {validationErrors.projectPrinciples && (
                  <p className="text-red-400 text-xs mt-1">{validationErrors.projectPrinciples}</p>
                )}
              </div>
            ) : (
              <div>
                <label htmlFor="custom-instructions" className="block text-sm font-medium text-text mb-1">Custom Instructions</label>
                <MarkdownPreviewToggle
                  id="custom-instructions"
                  value={formState.customInstructions}
                  onChange={value => setFormState(prev => ({ ...prev, customInstructions: value }))}
                  onFocus={() => clearFieldError('customInstructions')}
                  rows={6}
                  placeholder="Describe the agent's behavior and guidelines..."
                />
                <p className="text-textMuted text-xs mt-1">{formState.customInstructions.length} characters</p>
                {validationErrors.customInstructions && (
                  <p className="text-red-400 text-xs mt-1">{validationErrors.customInstructions}</p>
                )}
              </div>
            )}
          </section>

          <AddToolsModal
            isOpen={isAddToolsModalOpen}
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
              className="px-6 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2 shadow-lg shadow-primary/20 hover:shadow-[0_0_20px_rgba(99,102,241,0.2)]"
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
