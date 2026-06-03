import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Loader2, AlertTriangle } from 'lucide-react';
import ProviderCardSelector, { ProviderCardSelectorOutput } from '../ProviderCardSelector';
import ToggleSwitch from '../ToggleSwitch';
import ModelSelector from '../ModelSelector';
import { createWorkspaceWithProvider } from '../../services/workspaceService';
import { Workspace } from '../../types';

interface CreateWorkspacePageProps {
  /** True when the authenticated user already has at least one workspace. Controls Cancel visibility. */
  hasExistingWorkspaces: boolean;
  /** Called with the newly created workspace so App.tsx can update global workspace state. */
  onWorkspaceCreated: (workspace: Workspace) => void;
}

const CreateWorkspacePage: React.FC<CreateWorkspacePageProps> = ({
  hasExistingWorkspaces,
  onWorkspaceCreated,
}) => {
  const navigate = useNavigate();

  // ── Section 1: Workspace Name ─────────────────────────────────────────────
  const [workspaceName, setWorkspaceName] = useState('');

  // ── Section 2: Provider ───────────────────────────────────────────────────
  const [providerOutput, setProviderOutput] = useState<ProviderCardSelectorOutput | null>(null);

  // ── Section 3: AI Features ────────────────────────────────────────────────
  const [isAiSummarizationEnabled, setIsAiSummarizationEnabled] = useState(false);
  const [isCustomerSatisfactionAnalysisEnabled, setIsCustomerSatisfactionAnalysisEnabled] = useState(false);
  const [selectedAiSummarizationModel, setSelectedAiSummarizationModel] = useState<string | undefined>();
  const [selectedCustomerSatisfactionAnalysisModel, setSelectedCustomerSatisfactionAnalysisModel] = useState<string | undefined>();

  // ── Form-level state ──────────────────────────────────────────────────────
  const [isProcessing, setIsProcessing] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // ── Derived validity ─────────────────────────────────────────────────────
  const trimmedName = workspaceName.trim();
  const isNameValid = trimmedName.length >= 2 && trimmedName.length <= 100;
  const isProviderValid = providerOutput !== null && providerOutput.isValid;
  const isFormValid = isNameValid && isProviderValid;

  // Models offered by the validated provider (empty until provider reports isValid: true)
  const availableModels = providerOutput?.availableModels ?? [];

  // ── Handlers ─────────────────────────────────────────────────────────────
  const handleProviderChange = (output: ProviderCardSelectorOutput | null) => {
    setProviderOutput(output);
    // Clear AI feature model selections whenever the provider changes or resets
    if (!output?.isValid) {
      setSelectedAiSummarizationModel(undefined);
      setSelectedCustomerSatisfactionAnalysisModel(undefined);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isFormValid || !providerOutput) return;

    setIsProcessing(true);
    setSubmitError(null);

    try {
      const workspace = await createWorkspaceWithProvider(
        trimmedName,
        providerOutput.providerType,
        {
          // endpoint is set by both AzureOpenAIConfigForm (Azure resource URL) and
          // OllamaConfigForm (Ollama server base URL) — unified into a single field (FR-017).
          endpoint: providerOutput.endpoint,
          apiKey: providerOutput.apiKey,
        },
        providerOutput.defaultModelId ?? '',
        isAiSummarizationEnabled,
        isCustomerSatisfactionAnalysisEnabled,
        isAiSummarizationEnabled ? selectedAiSummarizationModel : undefined,
        isCustomerSatisfactionAnalysisEnabled ? selectedCustomerSatisfactionAnalysisModel : undefined,
      );

      onWorkspaceCreated(workspace);
      navigate('/');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to create workspace.';
      setSubmitError(message);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleCancel = () => {
    navigate(-1);
  };

  // ── Render ────────────────────────────────────────────────────────────────
  return (
    <div className="max-w-2xl mx-auto py-8">
      <div className="bg-surface border border-border rounded-xl shadow-xl shadow-primary/5 overflow-hidden">

        {/* Page header */}
        <div className="px-6 py-4 border-b border-border-elevated bg-surfaceHighlight/50">
          <h2 className="text-xl font-bold text-text">
            {hasExistingWorkspaces ? 'New Workspace' : 'Welcome to Orchestra'}
          </h2>
          <p className="text-sm text-textMuted mt-0.5">
            Configure your workspace and AI provider to get started.
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
              onChange={(e) => setWorkspaceName(e.target.value)}
              placeholder="e.g., Engineering Alpha"
              maxLength={100}
              className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
              autoFocus
            />
            {workspaceName.trim().length > 0 && !isNameValid && (
              <p className="text-xs text-red-400">
                Name must be between 2 and 100 characters.
              </p>
            )}
          </div>

          {/* ── Section 2: Choose AI Provider ── */}
          <div className="space-y-3 border-t border-border pt-5">
            <div>
              <h3 className="text-[10px] font-bold text-textMuted uppercase tracking-widest">
                Choose AI Provider
              </h3>
              <p className="text-xs text-textMuted mt-0.5">
                Required. Select and configure the AI provider for this workspace.
              </p>
            </div>
            <ProviderCardSelector onChange={handleProviderChange} />
          </div>

          {/* ── Section 3: AI Features (disabled until provider is valid) ── */}
          <div
            className={`space-y-4 border-t border-border pt-5 transition-opacity ${
              !isProviderValid ? 'opacity-40 pointer-events-none select-none' : ''
            }`}
            aria-disabled={!isProviderValid}
          >
            <h3 className="text-[10px] font-bold text-textMuted uppercase tracking-widest">
              AI Features
            </h3>

            {/* AI Summarization */}
            <div>
              <ToggleSwitch
                id="aiSummarizationCreate"
                checked={isAiSummarizationEnabled}
                onChange={setIsAiSummarizationEnabled}
                label="Enable AI Summarization"
                disabled={!isProviderValid}
              />
              {isAiSummarizationEnabled && availableModels.length > 0 && (
                <div className="mt-2 ml-4">
                  <ModelSelector
                    label="Model for AI Summarization"
                    selectedModel={selectedAiSummarizationModel}
                    availableModels={availableModels}
                    onModelChange={setSelectedAiSummarizationModel}
                    isLoading={false}
                    error={null}
                    disabled={isProcessing}
                  />
                </div>
              )}
            </div>

            {/* Customer Satisfaction Analysis */}
            <div>
              <ToggleSwitch
                id="customerSatisfactionCreate"
                checked={isCustomerSatisfactionAnalysisEnabled}
                onChange={setIsCustomerSatisfactionAnalysisEnabled}
                label="Enable Customer Satisfaction Analysis"
                disabled={!isProviderValid}
              />
              {isCustomerSatisfactionAnalysisEnabled && availableModels.length > 0 && (
                <div className="mt-2 ml-4">
                  <ModelSelector
                    label="Model for Customer Satisfaction Analysis"
                    selectedModel={selectedCustomerSatisfactionAnalysisModel}
                    availableModels={availableModels}
                    onModelChange={setSelectedCustomerSatisfactionAnalysisModel}
                    isLoading={false}
                    error={null}
                    disabled={isProcessing}
                  />
                </div>
              )}
            </div>
          </div>

          {/* Inline backend error */}
          {submitError && (
            <div className="flex items-start gap-2 px-3 py-2 bg-red-500/10 border border-red-500/30 rounded-md">
              <AlertTriangle className="w-4 h-4 text-red-400 shrink-0 mt-0.5" />
              <p className="text-sm text-red-400">{submitError}</p>
            </div>
          )}

          {/* Actions */}
          <div className="pt-2 flex gap-3 border-t border-border">
            {/* Cancel is absent for first-time users (no existing workspaces) */}
            {hasExistingWorkspaces && (
              <button
                type="button"
                onClick={handleCancel}
                disabled={isProcessing}
                className="flex-1 px-4 py-2.5 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors disabled:opacity-50"
              >
                Cancel
              </button>
            )}
            <button
              type="submit"
              disabled={!isFormValid || isProcessing}
              className="flex-1 px-4 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-primary/20 hover:shadow-[0_0_20px_rgba(99,102,241,0.2)]"
            >
              {isProcessing && <Loader2 className="w-4 h-4 animate-spin" />}
              {isProcessing ? 'Creating...' : 'Create Workspace'}
            </button>
          </div>

        </form>
      </div>
    </div>
  );
};

export default CreateWorkspacePage;
