import React, { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Loader2, AlertTriangle, ShieldAlert } from 'lucide-react';
import ProviderCardSelector, { ProviderCardSelectorOutput } from '../ProviderCardSelector';
import ToggleSwitch from '../ToggleSwitch';
import ModelSelector from '../ModelSelector';
import Toast from '../Toast';
import { Workspace, WorkspaceProviderConfig, WorkspaceProviderUpdateRequest } from '../../types';
import {
  updateWorkspace,
  getWorkspaceProviderConfig,
  updateWorkspaceProvider,
  getWorkspaces,
} from '../../services/workspaceService';
import { detectStaleModels } from '../../utils/staleModelDetection';
import { getUser } from '../../services/authService';

interface EditWorkspacePageProps {}

const EditWorkspacePage: React.FC<EditWorkspacePageProps> = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  // ── Provider config loading ─────────────────────────────────────────────
  const [providerConfig, setProviderConfig] = useState<WorkspaceProviderConfig | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isAccessDenied, setIsAccessDenied] = useState(false);

  // ── Workspace loaded from API ───────────────────────────────────────────
  const [workspace, setWorkspace] = useState<Workspace | null>(null);

  // ── Form fields: Workspace Name ─────────────────────────────────────────
  const [workspaceName, setWorkspaceName] = useState('');

  // ── Form fields: AI Features ────────────────────────────────────────────
  const [isAiSummarizationEnabled, setIsAiSummarizationEnabled] = useState(false);
  const [isCustomerSatisfactionAnalysisEnabled, setIsCustomerSatisfactionAnalysisEnabled] =
    useState(false);
  const [selectedAiSummarizationModel, setSelectedAiSummarizationModel] = useState<
    string | undefined
  >();
  const [selectedCustomerSatisfactionModel, setSelectedCustomerSatisfactionModel] = useState<
    string | undefined
  >();

  // ── Provider section ────────────────────────────────────────────────────
  const [providerOutput, setProviderOutput] = useState<ProviderCardSelectorOutput | null>(null);
  // isProviderDirty: true only when the user actively modifies the provider section.
  // When false, updateWorkspaceProvider is NOT called (provider config unchanged).
  const [isProviderDirty, setIsProviderDirty] = useState(false);
  // Tracks which provider card the user has selected. Used to detect type switches.
  const [selectedProviderType, setSelectedProviderType] = useState<'AzureOpenAI' | 'Ollama' | null>(null);

  // ── Save state ──────────────────────────────────────────────────────────
  const [isProcessing, setIsProcessing] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // ── Toast ───────────────────────────────────────────────────────────────
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  // ── Dirty-state tracking for Cancel guard ──────────────────────────────
  const [isDirty, setIsDirty] = useState(false);

  // ── Load provider config on mount ──────────────────────────────────────
  useEffect(() => {
    if (!workspaceId) return;

    let cancelled = false;
    setIsLoading(true);
    setLoadError(null);
    setIsAccessDenied(false);

    Promise.all([
      getWorkspaceProviderConfig(workspaceId),
      getWorkspaces(),
    ])
      .then(([config, allWorkspaces]) => {
        if (cancelled) return;
        setProviderConfig(config);
        setSelectedProviderType(config.providerType as 'AzureOpenAI' | 'Ollama');
        const found = allWorkspaces.find((w) => w.id === workspaceId) ?? null;
        if (!found) {
          setLoadError('Workspace not found.');
        } else {
          setWorkspace(found);
        }
      })
      .catch((err) => {
        if (cancelled) return;
        const msg =
          err instanceof Error ? err.message : 'Failed to load provider configuration.';
        if (msg.toLowerCase().includes('not found')) {
          setLoadError('Workspace not found.');
        } else if (
          msg.toLowerCase().includes('403') ||
          msg.toLowerCase().includes('denied') ||
          msg.toLowerCase().includes('forbidden')
        ) {
          setIsAccessDenied(true);
        } else {
          setLoadError(msg);
        }
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [workspaceId]);

  // ── Pre-populate form fields from workspace record ─────────────────────
  // Runs once when the workspace becomes available in state.
  useEffect(() => {
    if (!workspace) return;
    setWorkspaceName(workspace.name);
    setIsAiSummarizationEnabled(workspace.isAiSummarizationEnabled);
    setIsCustomerSatisfactionAnalysisEnabled(workspace.isCustomerSatisfactionAnalysisEnabled);
    setSelectedAiSummarizationModel(workspace.aiSummarizationModelId);
    setSelectedCustomerSatisfactionModel(workspace.customerSatisfactionAnalysisModelId);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [workspace?.id]); // Re-run only if the workspace identity changes (not on every render).

  // ── Derived values ──────────────────────────────────────────────────────
  const trimmedName = workspaceName.trim();
  const isNameValid = trimmedName.length >= 2 && trimmedName.length <= 100;

  // True when the user has selected a provider card that differs from the workspace's
  // original provider type. In this case the user MUST supply valid new credentials.
  const isProviderTypeSwitched =
    selectedProviderType !== null &&
    providerConfig !== null &&
    selectedProviderType !== providerConfig.providerType;

  // Provider is valid for save purposes when:
  // (a) type switched → fresh validated credentials required (providerOutput.isValid = true)
  // (b) same type, not dirty → no change, always valid
  // (c) same type, dirty → user modified credentials and they validated successfully
  const isProviderValid = isProviderTypeSwitched
    ? providerOutput?.isValid === true
    : !isProviderDirty || (providerOutput !== null && providerOutput.isValid);

  const isFormValid = isNameValid && isProviderValid && !isProcessing;

  // Ownership check: compare authenticated user ID against workspace owner ID.
  // AI feature toggles are interactive only when the current user is the workspace owner.
  const isOwner = getUser()?.id === workspace?.ownerId;

  // Available models for AI feature selectors.
  // When the provider type has been switched we never fall back to the old config's models —
  // only models returned by the new provider's validation call are valid.
  const availableModels = isProviderTypeSwitched
    ? (providerOutput?.availableModels ?? [])
    : (providerOutput?.availableModels ?? providerConfig?.models ?? []);

  // Stale model detection: compare saved model IDs against the available model list.
  const staleFlags = detectStaleModels(
    selectedAiSummarizationModel,
    selectedCustomerSatisfactionModel,
    availableModels
  );

  // ── Handlers ───────────────────────────────────────────────────────────

  // ── Clear AI feature model selections when the provider type changes ─────
  // Prevents stale old-provider model IDs from being submitted with the new provider.
  useEffect(() => {
    if (isProviderTypeSwitched) {
      setSelectedAiSummarizationModel(undefined);
      setSelectedCustomerSatisfactionModel(undefined);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isProviderTypeSwitched]);

  const handleProviderChange = useCallback(
    (output: ProviderCardSelectorOutput | null) => {
      setProviderOutput(output);
      // Mark provider section as dirty when the user provides a NEW valid provider config
      // (i.e., fresh credentials validated, or provider type switched).
      // The initial emit from AzureOpenAIConfigForm's keep-existing useEffect also fires here,
      // but with keepExistingCredentials: true — that does NOT mark as dirty.
      if (output !== null && output.isValid && !output.keepExistingCredentials) {
        setIsProviderDirty(true);
        setIsDirty(true);
      } else if (output !== null && output.isValid && output.keepExistingCredentials) {
        // Keep-existing emit: provider form is ready but the user has not changed credentials.
        // Clear dirty flag so no redundant provider update is sent on save.
        setIsProviderDirty(false);
        setProviderOutput(output);
      }
    },
    []
  );

  const handleProviderTypeChange = useCallback((type: 'AzureOpenAI' | 'Ollama') => {
    setSelectedProviderType(type);
    setIsDirty(true);
  }, []);

  const handleWorkspaceNameChange = (value: string) => {
    setWorkspaceName(value);
    setIsDirty(true);
  };

  const handleAiSummarizationToggle = (checked: boolean) => {
    setIsAiSummarizationEnabled(checked);
    setIsDirty(true);
    if (!checked) setSelectedAiSummarizationModel(undefined);
  };

  const handleCustomerSatisfactionToggle = (checked: boolean) => {
    setIsCustomerSatisfactionAnalysisEnabled(checked);
    setIsDirty(true);
    if (!checked) setSelectedCustomerSatisfactionModel(undefined);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isFormValid || !workspaceId) return;

    // Snapshot AI toggle state before submission so they can be reverted on failure.
    const prevAiSummarizationEnabled = isAiSummarizationEnabled;
    const prevCustomerSatisfactionAnalysisEnabled = isCustomerSatisfactionAnalysisEnabled;

    setIsProcessing(true);
    setSubmitError(null);

    try {
      // ── Step 1: Always update workspace name and AI feature settings ───
      // When the provider has been changed (isProviderDirty), fall back to the new provider's
      // defaultModelId for any AI feature that has no model explicitly chosen by the user.
      const newDefaultFallback = isProviderDirty ? (providerOutput?.defaultModelId ?? undefined) : undefined;
      const aiSummarizationModelToSave = isAiSummarizationEnabled
        ? (selectedAiSummarizationModel ?? newDefaultFallback)
        : undefined;
      const customerSatisfactionModelToSave = isCustomerSatisfactionAnalysisEnabled
        ? (selectedCustomerSatisfactionModel ?? newDefaultFallback)
        : undefined;

      const updatedWorkspace = await updateWorkspace(
        workspaceId,
        trimmedName,
        isAiSummarizationEnabled,
        isCustomerSatisfactionAnalysisEnabled,
        aiSummarizationModelToSave,
        customerSatisfactionModelToSave
      );
      setWorkspace(updatedWorkspace);

      // ── Step 2: Update provider only if user changed it ────────────────
      if (isProviderDirty && providerOutput && providerOutput.isValid) {
        const providerRequest: WorkspaceProviderUpdateRequest = {
          providerType: providerOutput.providerType,
          defaultModelId: providerOutput.defaultModelId ?? '',
          // Include endpoint when NOT keeping existing credentials and an endpoint is present.
          // For Azure: the encrypted Azure resource URL.
          // For Ollama: the Ollama server base URL (unified into endpoint — FR-017).
          ...(!providerOutput.keepExistingCredentials &&
          providerOutput.endpoint
            ? { endpoint: providerOutput.endpoint }
            : {}),
          ...(!providerOutput.keepExistingCredentials &&
          providerOutput.apiKey
            ? { apiKey: providerOutput.apiKey }
            : {}),
        };
        await updateWorkspaceProvider(workspaceId, providerRequest);
      }

      // ── Step 3: Navigate away with success toast ───────────────────────
      setToast({ message: 'Workspace updated successfully.', type: 'success' });
      setIsDirty(false);
      // Brief delay to let the user see the toast before navigating.
      setTimeout(() => navigate('/'), 1200);
    } catch (err) {
      // Revert AI feature toggles to their pre-submission (last persisted) state.
      setIsAiSummarizationEnabled(prevAiSummarizationEnabled);
      setIsCustomerSatisfactionAnalysisEnabled(prevCustomerSatisfactionAnalysisEnabled);
      const msg = err instanceof Error ? err.message : 'Failed to update workspace.';
      if (msg.toLowerCase().includes('access denied') || msg.includes('403')) {
        setSubmitError(
          'Access Denied: only the workspace owner can modify this workspace.'
        );
      } else {
        setSubmitError(msg);
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const handleCancel = () => {
    if (isDirty) {
      // Browser-standard confirmation — no custom modal required per FR-05.
      if (!window.confirm('Changes you made may not be saved.')) return;
    }
    navigate(-1);
  };

  // ── Loading / error states ──────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <div className="flex flex-col items-center gap-3 text-center">
          <Loader2 className="w-7 h-7 animate-spin text-primary" />
          <p className="text-sm text-textMuted">Loading workspace configuration…</p>
        </div>
      </div>
    );
  }

  if (isAccessDenied) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <div className="flex flex-col items-center gap-3 text-center max-w-sm">
          <ShieldAlert className="w-10 h-10 text-amber-500" />
          <h2 className="text-base font-bold text-text">Access Denied</h2>
          <p className="text-sm text-textMuted">
            Only the workspace owner can access the edit page.
          </p>
          <button
            type="button"
            onClick={() => navigate('/')}
            className="mt-2 px-4 py-2 rounded-md text-sm font-medium bg-primary text-white hover:bg-primary/90 transition-colors"
          >
            Back to Workspace
          </button>
        </div>
      </div>
    );
  }

  if (loadError || !workspace) {
    return (
      <div className="flex h-full items-center justify-center p-6">
        <div className="flex flex-col items-center gap-3 text-center max-w-sm">
          <AlertTriangle className="w-10 h-10 text-red-400" />
          <h2 className="text-base font-bold text-text">
            {loadError ?? 'Workspace not found.'}
          </h2>
          <p className="text-sm text-textMuted">
            The workspace could not be found or you do not have access.
          </p>
          <button
            type="button"
            onClick={() => navigate('/')}
            className="mt-2 px-4 py-2 rounded-md text-sm font-medium bg-primary text-white hover:bg-primary/90 transition-colors"
          >
            Back to Workspace
          </button>
        </div>
      </div>
    );
  }

  // ── Main render ─────────────────────────────────────────────────────────
  return (
    <div className="max-w-2xl mx-auto py-8">
      <div className="bg-surface border border-border rounded-xl shadow-lg overflow-hidden">

        {/* Page header */}
        <div className="px-6 py-4 border-b border-border bg-surfaceHighlight/50">
          <h2 className="text-xl font-bold text-text">Edit Workspace</h2>
          <p className="text-sm text-textMuted mt-0.5">
            Update workspace settings and AI provider configuration.
          </p>
        </div>

        <form onSubmit={handleSubmit} className="p-6 space-y-6">

          {/* ── Section 1: Workspace Name ── */}
          <div className="space-y-1.5">
            <label className="text-[10px] font-semibold text-textMuted uppercase tracking-widest">
              Workspace Name
            </label>
            <input
              type="text"
              value={workspaceName}
              onChange={(e) => handleWorkspaceNameChange(e.target.value)}
              placeholder="e.g., Engineering Alpha"
              maxLength={100}
              className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
            />
            {workspaceName.trim().length > 0 && !isNameValid && (
              <p className="text-xs text-red-400">
                Name must be between 2 and 100 characters.
              </p>
            )}
          </div>

          {/* ── Section 2: AI Provider ── */}
          <div className="space-y-3 border-t border-border pt-5">
            <div>
              <h3 className="text-[10px] font-bold text-textMuted uppercase tracking-widest">
                AI Provider Configuration
              </h3>
              <p className="text-xs text-textMuted mt-0.5">
                Current provider is pre-selected. Modify the configuration or select a new provider.
              </p>
            </div>
            <ProviderCardSelector
              onChange={handleProviderChange}
              onProviderTypeChange={handleProviderTypeChange}
              workspaceId={workspaceId}
              initialProviderType={providerConfig?.providerType}
              initialModels={providerConfig?.models}
              initialModelId={workspace.defaultModelId ?? null}
              existingCredentialsValid={providerConfig?.isValid}
              initialDefaultModelName={workspace.defaultModelId}
              initialEndpoint={providerConfig?.ollamaBaseUrl}
              initialIsServerValid={providerConfig?.isValid}
              isOwner={isOwner}
            />
          </div>

          {/* ── Section 3: AI Features ── */}
          {/* Toggles are always interactive for the workspace owner.         */}
          {/* Model selectors remain hidden until models are available.       */}
          <div className="space-y-4 border-t border-border pt-5">
            <h3 className="text-[10px] font-bold text-textMuted uppercase tracking-widest">
              AI Features
            </h3>

            {/* AI Summarization */}
            <div>
              <ToggleSwitch
                id="aiSummarizationEdit"
                checked={isAiSummarizationEnabled}
                onChange={handleAiSummarizationToggle}
                label="Enable AI Summarization"
                disabled={!isOwner || isProcessing}
              />
              {isAiSummarizationEnabled && availableModels.length > 0 && (
                <div className="mt-2 ml-4">
                  <ModelSelector
                    label="Model for AI Summarization"
                    selectedModel={selectedAiSummarizationModel}
                    availableModels={availableModels}
                    onModelChange={(id) => {
                      setSelectedAiSummarizationModel(id);
                      setIsDirty(true);
                    }}
                    isLoading={false}
                    error={null}
                    disabled={isProcessing}
                    isStale={staleFlags.aiSummarization}
                    featureName="AI Summarization"
                  />
                </div>
              )}
            </div>

            {/* Customer Satisfaction Analysis */}
            <div>
              <ToggleSwitch
                id="customerSatisfactionEdit"
                checked={isCustomerSatisfactionAnalysisEnabled}
                onChange={handleCustomerSatisfactionToggle}
                label="Enable Customer Satisfaction Analysis"
                disabled={!isOwner || isProcessing}
              />
              {isCustomerSatisfactionAnalysisEnabled && availableModels.length > 0 && (
                <div className="mt-2 ml-4">
                  <ModelSelector
                    label="Model for Customer Satisfaction Analysis"
                    selectedModel={selectedCustomerSatisfactionModel}
                    availableModels={availableModels}
                    onModelChange={(id) => {
                      setSelectedCustomerSatisfactionModel(id);
                      setIsDirty(true);
                    }}
                    isLoading={false}
                    error={null}
                    disabled={isProcessing}
                    isStale={staleFlags.customerSatisfactionAnalysis}
                    featureName="Customer Satisfaction Analysis"
                  />
                </div>
              )}
            </div>
          </div>

          {/* ── Inline submit error ── */}
          {submitError && (
            <div className="flex items-start gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-xs font-medium">
              <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
              <span>{submitError}</span>
            </div>
          )}

          {/* ── Action buttons ── */}
          <div className="flex items-center justify-end gap-3 pt-2 border-t border-border">
            <button
              type="button"
              onClick={handleCancel}
              disabled={isProcessing}
              className="px-4 py-2 rounded-md text-sm font-medium text-textMuted hover:text-text border border-border hover:border-primary/50 transition-colors disabled:opacity-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={!isFormValid}
              className="flex items-center gap-2 px-4 py-2 rounded-md text-sm font-medium bg-primary text-white disabled:opacity-40 disabled:cursor-not-allowed hover:bg-primary/90 transition-colors"
            >
              {isProcessing && <Loader2 size={14} className="animate-spin" />}
              {isProcessing ? 'Saving…' : 'Save Changes'}
            </button>
          </div>

        </form>
      </div>

      {/* Toast notification */}
      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}
    </div>
  );
};

export default EditWorkspacePage;
