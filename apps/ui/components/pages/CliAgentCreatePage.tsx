import React, { useState, useEffect, useMemo } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Loader2, AlertTriangle, ArrowLeft, Plus, X, RefreshCw } from 'lucide-react';
import { AiCliIntegration, AiCliProviderType, Agent, DiscoveredSkill, SkillFolder, Tool, McpServer, McpToolSelection, ToolCatalogueEntry } from '../../types';
import { createAgent, getAgents } from '../../services/agentService';
import { getCliIntegrations, discoverModelsForIntegration } from '../../services/cliIntegrationService';
import { getSkillFolders, getSkillsInFolder } from '../../services/skillFolderService';
import { getTools } from '../../services/toolService';
import { getMcpServers } from '../../services/mcpServerService';
import { isReasoningModel, REASONING_EFFORT_OPTIONS } from '../../utils/reasoningModels';
import AgentFormCapabilities from '../agents/AgentFormCapabilities';
import AgentToolSummarySection from '../agents/AgentToolSummarySection';
import AddToolsModal from '../agents/AddToolsModal';
import AddSubAgentsModal from '../agents/AddSubAgentsModal';
import MarkdownPreviewToggle from '../agents/MarkdownPreviewToggle';

const CLI_PROVIDER_LABELS: Record<AiCliProviderType, string> = {
  [AiCliProviderType.GITHUB_COPILOT]: 'GitHub Copilot',
  [AiCliProviderType.CLAUDE]: 'Claude',
  [AiCliProviderType.GEMINI]: 'Gemini',
};

interface FormState {
  name: string;
  role: string;
  currentCapability: string;
  capabilities: string[];
  toolActionIds: string[];
  customInstructions: string;
}

const initialFormState: FormState = {
  name: '',
  role: '',
  currentCapability: '',
  capabilities: [],
  toolActionIds: [],
  customInstructions: '',
};

const CliAgentCreatePage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  const [formState, setFormState] = useState<FormState>(initialFormState);
  const [isSaving, setIsSaving] = useState(false);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<string | null>(null);

  const [cliIntegrations, setCliIntegrations] = useState<AiCliIntegration[]>([]);
  const [loadingIntegrations, setLoadingIntegrations] = useState(false);
  const [selectedCliIntegrationId, setSelectedCliIntegrationId] = useState('');
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [isLoadingModels, setIsLoadingModels] = useState(false);
  const [selectedModel, setSelectedModel] = useState('');
  const [selectedReasoningEffort, setSelectedReasoningEffort] = useState('');

  // Tools
  const [availableTools, setAvailableTools] = useState<Tool[]>([]);
  const [mcpServers, setMcpServers] = useState<McpServer[]>([]);
  const [mcpSelections, setMcpSelections] = useState<McpToolSelection[]>([]);
  const [isAddToolsModalOpen, setIsAddToolsModalOpen] = useState(false);
  const [openAtSourceId, setOpenAtSourceId] = useState<string | null>(null);

  // Sub-agents
  const [allAgents, setAllAgents] = useState<Agent[]>([]);
  const [selectedSubAgentIds, setSelectedSubAgentIds] = useState<string[]>([]);
  const [isSubAgentsModalOpen, setIsSubAgentsModalOpen] = useState(false);

  // Skill folders + per-folder skills
  const [availableSkillFolders, setAvailableSkillFolders] = useState<SkillFolder[]>([]);
  const [selectedSkillFolderIds, setSelectedSkillFolderIds] = useState<string[]>([]);
  const [folderSkills, setFolderSkills] = useState<Record<string, DiscoveredSkill[]>>({});
  const [loadingFolderSkills, setLoadingFolderSkills] = useState<Record<string, boolean>>({});
  const [selectedCliSkillNames, setSelectedCliSkillNames] = useState<string[]>([]);

  useEffect(() => {
    if (!workspaceId) return;

    setLoadingIntegrations(true);
    getCliIntegrations(workspaceId)
      .then(integrations => {
        setCliIntegrations(integrations);
        if (integrations.length === 1) {
          setSelectedCliIntegrationId(integrations[0].id);
          loadCliModels(integrations[0].id);
        }
      })
      .catch(() => setCliIntegrations([]))
      .finally(() => setLoadingIntegrations(false));

    getTools(workspaceId).then(setAvailableTools).catch(() => setAvailableTools([]));
    getMcpServers(workspaceId).then(setMcpServers).catch(() => setMcpServers([]));
    getAgents(workspaceId).then(setAllAgents).catch(() => setAllAgents([]));
    getSkillFolders(workspaceId).then(setAvailableSkillFolders).catch(() => setAvailableSkillFolders([]));
  }, [workspaceId]);

  const loadCliModels = async (integrationId: string) => {
    if (!integrationId || !workspaceId) return;
    setIsLoadingModels(true);
    setAvailableModels([]);
    setSelectedModel('');
    setSelectedReasoningEffort('');
    try {
      const models = await discoverModelsForIntegration(workspaceId, integrationId);
      setAvailableModels(models);
    } catch {
      // silently ignore — user can proceed without model selection
    } finally {
      setIsLoadingModels(false);
    }
  };

  const handleIntegrationChange = (integrationId: string) => {
    setSelectedCliIntegrationId(integrationId);
    if (integrationId) loadCliModels(integrationId);
    else { setAvailableModels([]); setSelectedModel(''); setSelectedReasoningEffort(''); }
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

  const loadFolderSkills = async (folderId: string) => {
    if (!workspaceId || folderSkills[folderId] !== undefined) return;
    setLoadingFolderSkills(prev => ({ ...prev, [folderId]: true }));
    try {
      const skills = await getSkillsInFolder(workspaceId, folderId);
      setFolderSkills(prev => ({ ...prev, [folderId]: skills }));
    } catch {
      setFolderSkills(prev => ({ ...prev, [folderId]: [] }));
    } finally {
      setLoadingFolderSkills(prev => ({ ...prev, [folderId]: false }));
    }
  };

  const handleAddFolder = (folderId: string) => {
    setSelectedSkillFolderIds(prev => [...prev, folderId]);
    loadFolderSkills(folderId);
  };

  const handleRemoveFolder = (folderId: string) => {
    setSelectedSkillFolderIds(prev => prev.filter(id => id !== folderId));
    const skillsInFolder = (folderSkills[folderId] ?? []).map(s => s.name);
    setSelectedCliSkillNames(prev => prev.filter(name => !skillsInFolder.includes(name)));
  };

  const toggleCliSkill = (skillName: string) => {
    setSelectedCliSkillNames(prev =>
      prev.includes(skillName) ? prev.filter(n => n !== skillName) : [...prev, skillName]
    );
  };

  const handleOpenToolsModal = (sourceId?: string | null) => {
    setOpenAtSourceId(sourceId ?? null);
    setIsAddToolsModalOpen(true);
  };

  const handleCommitTools = (ids: string[], newMcpSelections: McpToolSelection[]) => {
    setFormState(prev => ({ ...prev, toolActionIds: ids }));
    setMcpSelections(newMcpSelections.filter(s => s.toolNames.length > 0));
    setIsAddToolsModalOpen(false);
  };

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

  const validateForm = (): Record<string, string> => {
    const errors: Record<string, string> = {};
    if (!formState.name.trim()) errors.name = 'Agent name is required.';
    else if (formState.name.trim().length > 200) errors.name = 'Name must be 200 characters or less.';
    if (!formState.role.trim()) errors.role = 'Agent role is required.';
    else if (formState.role.trim().length > 200) errors.role = 'Role must be 200 characters or less.';
    if (!selectedCliIntegrationId) errors.cliIntegration = 'A CLI integration is required.';
    if (!formState.customInstructions.trim()) errors.customInstructions = 'Custom instructions are required.';
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
        customInstructions: formState.customInstructions.trim(),
        model: selectedModel || null,
        aiCliIntegrationId: selectedCliIntegrationId,
        subAgentIds: selectedSubAgentIds,
        skillFolderIds: selectedSkillFolderIds,
        cliSkillNames: selectedCliSkillNames,
      });
      navigate(`/workspaces/${workspaceId}/agents`);
    } catch (error: any) {
      setSubmitError(error.message || 'Failed to create agent. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  const clearFieldError = (field: string) => {
    setValidationErrors(prev => { const next = { ...prev }; delete next[field]; return next; });
  };

  const handleAddCapability = (e: React.KeyboardEvent) => {
    if (e.key !== 'Enter') return;
    e.preventDefault();
    const cap = formState.currentCapability.trim();
    if (cap && !formState.capabilities.includes(cap))
      setFormState(prev => ({ ...prev, capabilities: [...prev.capabilities, cap], currentCapability: '' }));
  };

  const removeCapability = (cap: string) =>
    setFormState(prev => ({ ...prev, capabilities: prev.capabilities.filter(c => c !== cap) }));

  const selectedIntegration = cliIntegrations.find(i => i.id === selectedCliIntegrationId);

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      <div className="bg-surface border border-border rounded-xl shadow-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-border flex items-center gap-3">
          <Link
            to={`/workspaces/${workspaceId}/agents`}
            className="text-textMuted hover:text-text transition-colors"
            aria-label="Back to agents"
          >
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <h1 className="text-2xl font-bold text-text">Create CLI-Based Agent</h1>
        </div>

        <form onSubmit={handleSave} className="p-6 space-y-6">
          {submitError && (
            <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
              <AlertTriangle className="w-4 h-4 shrink-0" />
              <span>{submitError}</span>
            </div>
          )}

          {/* CLI Integration */}
          <section className="space-y-4">
            <h2 className="text-lg font-semibold text-text">CLI Integration</h2>

            {loadingIntegrations ? (
              <div className="flex items-center gap-2 text-textMuted text-sm">
                <Loader2 className="w-4 h-4 animate-spin" />
                Loading integrations…
              </div>
            ) : cliIntegrations.length === 0 ? (
              <div className="p-4 bg-yellow-500/10 border border-yellow-500/20 rounded-lg text-sm text-yellow-400">
                No CLI integrations configured.{' '}
                <Link to={`/workspaces/${workspaceId}/cli-integrations/new`} className="underline hover:no-underline">
                  Create one
                </Link>{' '}
                first.
              </div>
            ) : (
              <div>
                <label htmlFor="cli-integration" className="block text-sm font-medium text-text mb-1">
                  Integration <span className="text-red-400">*</span>
                </label>
                <select
                  id="cli-integration"
                  value={selectedCliIntegrationId}
                  onChange={e => handleIntegrationChange(e.target.value)}
                  onFocus={() => clearFieldError('cliIntegration')}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                >
                  <option value="">Select a CLI integration…</option>
                  {cliIntegrations.map(i => (
                    <option key={i.id} value={i.id}>
                      {i.name} — {CLI_PROVIDER_LABELS[i.provider]}
                    </option>
                  ))}
                </select>
                {validationErrors.cliIntegration && (
                  <p className="text-red-400 text-xs mt-1">{validationErrors.cliIntegration}</p>
                )}
              </div>
            )}

            {selectedCliIntegrationId && (
              <div>
                <label htmlFor="cli-model" className="block text-sm font-medium text-text mb-1">
                  Model
                  {isLoadingModels && <Loader2 className="w-3 h-3 animate-spin inline ml-2" />}
                  {!isLoadingModels && selectedCliIntegrationId && (
                    <button
                      type="button"
                      onClick={() => loadCliModels(selectedCliIntegrationId)}
                      className="ml-2 text-textMuted hover:text-primary transition-colors"
                      aria-label="Refresh models"
                    >
                      <RefreshCw className="w-3 h-3 inline" />
                    </button>
                  )}
                </label>
                <select
                  id="cli-model"
                  value={selectedModel}
                  onChange={e => { setSelectedModel(e.target.value); setSelectedReasoningEffort(''); }}
                  disabled={isLoadingModels}
                  className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary disabled:opacity-50"
                >
                  <option value="">Default model</option>
                  {availableModels.map(m => <option key={m} value={m}>{m}</option>)}
                </select>

                {selectedModel && isReasoningModel(selectedModel) && (
                  <div className="mt-3">
                    <label htmlFor="reasoning-effort" className="block text-sm font-medium text-text mb-1">
                      Reasoning Effort
                    </label>
                    <select
                      id="reasoning-effort"
                      value={selectedReasoningEffort}
                      onChange={e => setSelectedReasoningEffort(e.target.value)}
                      className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
                    >
                      <option value="">Default</option>
                      {REASONING_EFFORT_OPTIONS.map(opt => (
                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                      ))}
                    </select>
                  </div>
                )}
              </div>
            )}
          </section>

          {/* Identity */}
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
                placeholder="e.g. Codebase Explorer"
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
          </section>

          <AgentFormCapabilities
            currentCapability={formState.currentCapability}
            capabilities={formState.capabilities}
            onCurrentCapabilityChange={value => setFormState(prev => ({ ...prev, currentCapability: value }))}
            onAddCapability={handleAddCapability}
            onRemoveCapability={removeCapability}
          />

          {/* Tools */}
          <AgentToolSummarySection
            toolActionIds={formState.toolActionIds}
            toolCatalogue={toolCatalogue}
            mcpServers={mcpServers}
            mcpSelections={mcpSelections}
            onOpenModal={handleOpenToolsModal}
            onRemoveSource={handleRemoveSource}
            onRemoveMcpServer={handleRemoveMcpServer}
          />

          {/* Sub-Agents */}
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

          {/* Skill Folders & Skills */}
          <section className="space-y-3">
            <h2 className="text-lg font-semibold text-text">Skill Folders &amp; Skills</h2>

            {selectedSkillFolderIds.length > 0 && (
              <div className="space-y-4">
                {selectedSkillFolderIds.map(folderId => {
                  const folder = availableSkillFolders.find(f => f.id === folderId);
                  if (!folder) return null;
                  const skills = folderSkills[folderId];
                  const isLoading = loadingFolderSkills[folderId];
                  return (
                    <div key={folderId} className="border border-border rounded-lg overflow-hidden">
                      <div className="flex items-center gap-3 px-3 py-2 bg-surfaceHighlight">
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium text-text truncate">{folder.name}</p>
                          <p className="text-xs text-textMuted font-mono truncate">{folder.folderPath}</p>
                        </div>
                        <button
                          type="button"
                          onClick={() => handleRemoveFolder(folderId)}
                          className="text-textMuted hover:text-red-400 transition-colors p-1 rounded hover:bg-red-500/10 flex-shrink-0"
                          aria-label={`Remove ${folder.name}`}
                        >
                          <X className="w-4 h-4" />
                        </button>
                      </div>
                      <div className="px-3 py-2 space-y-1">
                        {isLoading && (
                          <div className="flex items-center gap-2 text-textMuted text-xs py-1">
                            <Loader2 className="w-3 h-3 animate-spin" />
                            Loading skills…
                          </div>
                        )}
                        {!isLoading && skills && skills.length === 0 && (
                          <p className="text-xs text-textMuted italic py-1">No skills found in this folder.</p>
                        )}
                        {!isLoading && skills && skills.map(skill => {
                          const isSelected = selectedCliSkillNames.includes(skill.name);
                          return (
                            <button
                              key={skill.name}
                              type="button"
                              onClick={() => toggleCliSkill(skill.name)}
                              className={`flex items-start gap-3 w-full px-3 py-2 rounded-md text-left transition-colors ${
                                isSelected
                                  ? 'bg-primary/10 border border-primary/30 text-text'
                                  : 'hover:bg-surfaceHighlight border border-transparent text-textMuted hover:text-text'
                              }`}
                            >
                              <div className={`mt-0.5 w-4 h-4 rounded border flex-shrink-0 flex items-center justify-center ${
                                isSelected ? 'bg-primary border-primary' : 'border-border'
                              }`}>
                                {isSelected && (
                                  <svg className="w-2.5 h-2.5 text-white" fill="none" viewBox="0 0 10 8">
                                    <path d="M1 4l3 3 5-6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                                  </svg>
                                )}
                              </div>
                              <div className="flex-1 min-w-0">
                                <p className="text-sm font-medium">{skill.name}</p>
                                {skill.description && (
                                  <p className="text-xs text-textMuted mt-0.5 line-clamp-2">{skill.description}</p>
                                )}
                              </div>
                            </button>
                          );
                        })}
                      </div>
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
                      onClick={() => handleAddFolder(folder.id)}
                      className="flex items-center gap-2 w-full px-3 py-2 border border-dashed border-border rounded-lg text-sm text-textMuted hover:text-primary hover:border-primary/50 transition-colors text-left"
                    >
                      <Plus className="w-4 h-4 shrink-0" />
                      <span className="truncate">{folder.name}</span>
                    </button>
                  ))}
              </div>
            )}

            {availableSkillFolders.length === 0 && (
              <p className="text-xs text-textMuted italic">
                No skill folders registered yet.{' '}
                <Link to={`/workspaces/${workspaceId}/skill-folders/new`} className="text-primary hover:underline">
                  Register one
                </Link>.
              </p>
            )}
          </section>

          {/* Instructions */}
          <section className="space-y-4">
            <h2 className="text-lg font-semibold text-text">Instructions</h2>
            <div>
              <label htmlFor="custom-instructions" className="block text-sm font-medium text-text mb-1">
                Custom Instructions
              </label>
              <MarkdownPreviewToggle
                id="custom-instructions"
                value={formState.customInstructions}
                onChange={value => setFormState(prev => ({ ...prev, customInstructions: value }))}
                onFocus={() => clearFieldError('customInstructions')}
                rows={6}
                placeholder="Describe the agent's behavior and guidelines…"
              />
              <p className="text-textMuted text-xs mt-1">{formState.customInstructions.length} characters</p>
              {validationErrors.customInstructions && (
                <p className="text-red-400 text-xs mt-1">{validationErrors.customInstructions}</p>
              )}
            </div>
          </section>

          <AddToolsModal
            isOpen={isAddToolsModalOpen}
            initialToolActionIds={formState.toolActionIds}
            toolCatalogue={toolCatalogue}
            workspaceId={workspaceId!}
            onCommit={handleCommitTools}
            onDiscard={() => setIsAddToolsModalOpen(false)}
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

          <div className="flex justify-end gap-3 pt-4 border-t border-border">
            <button
              type="button"
              onClick={() => navigate(`/workspaces/${workspaceId}/agents`)}
              disabled={isSaving}
              className="px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSaving || !selectedCliIntegrationId}
              className="px-6 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2 shadow-lg shadow-primary/20 disabled:opacity-50"
            >
              {isSaving && <Loader2 className="w-4 h-4 animate-spin" />}
              {isSaving ? 'Saving…' : 'Create Agent'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default CliAgentCreatePage;
