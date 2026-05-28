import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Loader2, AlertTriangle, ArrowLeft, Terminal, RefreshCw } from 'lucide-react';
import { AgentTemplateDto, AiCliIntegration, AiCliProviderType } from '../../types';
import { getAgentTemplates, createAgentFromTemplate } from '../../services/agentService';
import { getCliIntegrations, discoverModelsForIntegration } from '../../services/cliIntegrationService';
import { fetchWorkspaceModels } from '../../services/workspaceService';
import { isReasoningModel, REASONING_EFFORT_OPTIONS } from '../../utils/reasoningModels';
import Toast from '../Toast';
import TemplateDetailsCard from '../agents/TemplateDetailsCard';
import AgentOptionalToolsSection from '../agents/AgentOptionalToolsSection';

const CLI_PROVIDER_LABELS: Record<AiCliProviderType, string> = {
  [AiCliProviderType.GITHUB_COPILOT]: 'GitHub Copilot',
  [AiCliProviderType.CLAUDE]: 'Claude',
  [AiCliProviderType.GEMINI]: 'Gemini',
};

type PageState = 'LOADING' | 'READY' | 'ALREADY_DEPLOYED' | 'UNMET_PREREQUISITES' | 'NOT_FOUND' | 'ERROR';

const DeployBuiltInAgentPage: React.FC = () => {
  const { workspaceId, templateId } = useParams<{ workspaceId: string; templateId: string }>();
  const navigate = useNavigate();

  const [pageState, setPageState] = useState<PageState>('LOADING');
  const [template, setTemplate] = useState<AgentTemplateDto | null>(null);
  const [isDeploying, setIsDeploying] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const [projectPrinciples, setProjectPrinciples] = useState('');
  const [customInstructions, setCustomInstructions] = useState('');
  const [cliIntegrations, setCliIntegrations] = useState<AiCliIntegration[]>([]);
  const [selectedCliIntegrationId, setSelectedCliIntegrationId] = useState<string>('');
  const [loadingIntegrations, setLoadingIntegrations] = useState(false);
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [selectedModel, setSelectedModel] = useState<string>('');
  const [isLoadingModels, setIsLoadingModels] = useState(false);
  const [selectedReasoningEffort, setSelectedReasoningEffort] = useState<string>('');
  const [selectedOptionalTools, setSelectedOptionalTools] = useState<string[]>([]);
  const agentsPath = `/workspaces/${workspaceId}/agents`;

  useEffect(() => {
    if (!workspaceId || !templateId) return;
    loadTemplate();
  }, [workspaceId, templateId]);

  const loadTemplate = async () => {
    setPageState('LOADING');
    try {
      const templates = await getAgentTemplates(workspaceId!);
      const found = templates.find(t => t.templateId === templateId);
      if (!found) {
        setPageState('NOT_FOUND');
        return;
      }
      setTemplate(found);
      setPageState(resolvePageState(found));

      if (found.isCliAgent) {
        setLoadingIntegrations(true);
        getCliIntegrations(workspaceId!)
          .then((integrations) => {
            setCliIntegrations(integrations);
            if (integrations.length === 1) setSelectedCliIntegrationId(integrations[0].id);
          })
          .catch(() => setCliIntegrations([]))
          .finally(() => setLoadingIntegrations(false));
      } else {
        // Fetch available models for non-CLI agents
        fetchWorkspaceModels(workspaceId!)
          .then((models) => setAvailableModels(models))
          .catch(() => setAvailableModels([]));
      }
    } catch (error) {
      setFetchError(error instanceof Error ? error.message : 'Failed to load template');
      setPageState('ERROR');
    }
  };

  const resolvePageState = (t: AgentTemplateDto): PageState => {
    const stateMap: Record<string, PageState> = {
      'AVAILABLE': 'READY',
      'ALREADY_DEPLOYED': 'ALREADY_DEPLOYED',
      'UNAVAILABLE': 'UNMET_PREREQUISITES',
      'ERROR': 'ERROR',
    };
    return stateMap[t.availability.status] ?? 'ERROR';
  };

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
      // silently ignore — user can still proceed without model selection
    } finally {
      setIsLoadingModels(false);
    }
  };

  const handleIntegrationChange = (integrationId: string) => {
    setSelectedCliIntegrationId(integrationId);
    if (template?.isCliAgent && integrationId) {
      loadCliModels(integrationId);
    }
  };

  const handleDeploy = async () => {
    if (!template) return;
    setIsDeploying(true);
    try {
      await createAgentFromTemplate({
        workspaceId: workspaceId!,
        templateId: templateId!,
        projectPrinciples: template.editableFields.includes('projectPrinciples') ? projectPrinciples.trim() : '',
        model: selectedModel || undefined,
        reasoningEffort: (selectedModel && isReasoningModel(selectedModel) && selectedReasoningEffort) ? selectedReasoningEffort : undefined,
        aiCliIntegrationId: template.isCliAgent ? selectedCliIntegrationId : undefined,
        selectedOptionalToolMethodNames: selectedOptionalTools.length > 0 ? selectedOptionalTools : undefined,
      });
      setToast({ message: 'Agent deployed successfully', type: 'success' });
      navigate(agentsPath);
    } catch (error) {
      setToast({ message: error instanceof Error ? error.message : 'Failed to deploy agent', type: 'error' });
      setIsDeploying(false);
    }
  };

  const handleCancel = () => navigate(agentsPath);

  const isDeployReady = !template ? false
    : (!template.isCliAgent || selectedCliIntegrationId.length > 0)
    && (!template.editableFields.includes('projectPrinciples') || projectPrinciples.trim().length > 0)
    && (!template.editableFields.includes('customInstructions') || customInstructions.trim().length > 0);

  if (pageState === 'LOADING') {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex items-center justify-center py-20" data-testid="deploy-page-loading">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
        </div>
      </div>
    );
  }

  if (pageState === 'NOT_FOUND') {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex flex-col items-center justify-center py-20 gap-4">
          <AlertTriangle className="w-12 h-12 text-yellow-500" />
          <h2 className="text-xl font-semibold">Template not found</h2>
          <Link to={agentsPath} className="text-primary hover:underline">Back to Agents</Link>
        </div>
      </div>
    );
  }

  if (pageState === 'ERROR' && !template) {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex flex-col items-center justify-center py-20 gap-4">
          <AlertTriangle className="w-12 h-12 text-red-500" />
          <h2 className="text-xl font-semibold">Failed to load template</h2>
          <Link to={agentsPath} className="text-primary hover:underline">Back to Agents</Link>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      <Link to={agentsPath} className="inline-flex items-center gap-1 text-sm text-textMuted hover:text-text mb-6">
        <ArrowLeft className="w-4 h-4" /> Back to Agents
      </Link>

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Deploy Built-In Agent</h1>
        <StatusBadge pageState={pageState} />
      </div>

      {pageState === 'ALREADY_DEPLOYED' && (
        <div className="bg-yellow-50 border border-yellow-300 text-yellow-800 rounded-lg px-4 py-3 mb-6">
          This template is already active in this workspace.
        </div>
      )}

      {pageState === 'UNMET_PREREQUISITES' && (
        <div className="bg-red-50 border border-red-300 text-red-800 rounded-lg px-4 py-3 mb-6">
          Missing required prerequisites. Please configure the required integrations before deploying.
        </div>
      )}

      {template && <TemplateDetailsCard template={template} />}

      {template && pageState === 'READY' && (
        <div className="mt-6 space-y-6">
          {template.isCliAgent && (
            <div>
              <label className="block text-sm font-semibold mb-1 flex items-center gap-1.5">
                <Terminal className="w-4 h-4" />
                CLI Integration <span className="text-red-500">*</span>
              </label>
              {loadingIntegrations ? (
                <div className="flex items-center gap-2 text-sm text-textMuted">
                  <Loader2 className="w-4 h-4 animate-spin" />
                  Loading integrations…
                </div>
              ) : cliIntegrations.length === 0 ? (
                <div className="bg-yellow-50 border border-yellow-300 text-yellow-800 rounded-lg px-4 py-3 flex items-start gap-2">
                  <AlertTriangle className="w-4 h-4 flex-shrink-0 mt-0.5" />
                  <p className="text-sm">
                    No CLI integrations configured. Please add one in{' '}
                    <strong>Settings → CLI Integrations</strong> first.
                  </p>
                </div>
              ) : (
                <select
                  value={selectedCliIntegrationId}
                  onChange={(e) => handleIntegrationChange(e.target.value)}
                  disabled={isDeploying}
                  className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background focus:outline-none focus:border-primary"
                  aria-label="Select CLI integration"
                >
                  <option value="">— Select a CLI integration —</option>
                  {cliIntegrations.map((integration) => (
                    <option key={integration.id} value={integration.id}>
                      {integration.name} ({CLI_PROVIDER_LABELS[integration.provider]})
                    </option>
                  ))}
                </select>
              )}
            </div>
          )}

          {!template.isCliAgent && (
            <div>
              <label className="block text-sm font-semibold mb-1">Model</label>
              <select
                value={selectedModel}
                onChange={(e) => setSelectedModel(e.target.value)}
                disabled={isDeploying}
                className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background focus:outline-none focus:border-primary"
                aria-label="Select model"
              >
                <option value="">Default</option>
                {availableModels.map((model) => (
                  <option key={model} value={model}>
                    {model}
                  </option>
                ))}
              </select>
            </div>
          )}

          {template.isCliAgent && selectedCliIntegrationId && (
            <div>
              <label className="block text-sm font-semibold mb-1 flex items-center gap-1.5">
                Model
                <button
                  type="button"
                  onClick={() => loadCliModels(selectedCliIntegrationId)}
                  disabled={isLoadingModels}
                  className="ml-1 inline-flex items-center gap-1 text-xs text-textMuted hover:text-text"
                  title="Reload models"
                >
                  <RefreshCw size={12} className={isLoadingModels ? 'animate-spin' : ''} />
                  {isLoadingModels ? 'Loading…' : 'Load models'}
                </button>
              </label>
              <select
                value={selectedModel}
                onChange={(e) => { setSelectedModel(e.target.value); setSelectedReasoningEffort(''); }}
                disabled={isDeploying || isLoadingModels}
                className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background focus:outline-none focus:border-primary"
                aria-label="Select model"
              >
                <option value="">Default (Copilot decides)</option>
                {availableModels.map((model) => (
                  <option key={model} value={model}>{model}</option>
                ))}
              </select>

              {selectedModel && isReasoningModel(selectedModel) && (
                <div className="mt-3">
                  <label className="block text-sm font-semibold mb-1">Reasoning Effort</label>
                  <select
                    value={selectedReasoningEffort}
                    onChange={(e) => setSelectedReasoningEffort(e.target.value)}
                    disabled={isDeploying}
                    className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background focus:outline-none focus:border-primary"
                  >
                    <option value="">Default</option>
                    {REASONING_EFFORT_OPTIONS.map(opt => (
                      <option key={opt.value} value={opt.value}>{opt.label}</option>
                    ))}
                  </select>
                  <p className="text-xs text-textMuted mt-1">Controls how much reasoning the model applies. Higher effort is slower but more thorough.</p>
                </div>
              )}
            </div>
          )}

          {template.editableFields.includes('customInstructions') && (
            <div>
              <label className="block text-sm font-semibold mb-1">
                Custom Instructions <span className="text-red-500">*</span>
              </label>
              <textarea
                value={customInstructions}
                onChange={(e) => setCustomInstructions(e.target.value)}
                placeholder="Provide any custom instructions for this agent…"
                rows={6}
                disabled={isDeploying}
                className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background font-mono focus:outline-none focus:border-primary resize-y"
              />
            </div>
          )}

          {template.editableFields.includes('projectPrinciples') && (
            <div>
              <label className="block text-sm font-semibold mb-1">
                Project Principles <span className="text-red-500">*</span>
              </label>
              <textarea
                value={projectPrinciples}
                onChange={(e) => setProjectPrinciples(e.target.value)}
                placeholder="Describe your project's coding standards, review criteria, and principles…"
                rows={6}
                disabled={isDeploying}
                className="w-full border border-border rounded-lg px-3 py-2 text-sm bg-background font-mono focus:outline-none focus:border-primary resize-y"
              />
            </div>
          )}

          <AgentOptionalToolsSection
            availableOptionalTools={template.availableOptionalTools ?? []}
            selectedMethodNames={selectedOptionalTools}
            onChange={setSelectedOptionalTools}
          />
        </div>
      )}

      <div className="flex gap-3 mt-8">
        <button
          onClick={handleDeploy}
          disabled={pageState !== 'READY' || isDeploying || !isDeployReady}
          className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-2"
        >
          {isDeploying && <Loader2 className="w-4 h-4 animate-spin" />}
          {isDeploying ? 'Deploying…' : 'Deploy'}
        </button>
        <button
          onClick={handleCancel}
          disabled={isDeploying}
          className="px-4 py-2 border border-border rounded-lg font-medium hover:bg-surface"
        >
          Cancel
        </button>
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
};

const StatusBadge: React.FC<{ pageState: PageState }> = ({ pageState }) => {
  const badges: Record<string, { label: string; className: string }> = {
    READY: { label: 'Available', className: 'bg-blue-100 text-blue-800' },
    ALREADY_DEPLOYED: { label: 'Deployed', className: 'bg-emerald-100 text-emerald-800' },
    UNMET_PREREQUISITES: { label: 'Blocked', className: 'bg-red-100 text-red-800' },
  };
  const badge = badges[pageState];
  if (!badge) return null;
  return <span className={`px-3 py-1 rounded-full text-xs font-semibold ${badge.className}`}>{badge.label}</span>;
};

export default DeployBuiltInAgentPage;
