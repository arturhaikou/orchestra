import React, { useState } from 'react';
import { CheckCircle2, Server, Cloud } from 'lucide-react';
import AzureOpenAIConfigForm, { AzureOpenAIConfigFormState } from './AzureOpenAIConfigForm';
import OllamaConfigForm, { OllamaConfigFormState } from './OllamaConfigForm';

/**
 * The aggregated provider state surfaced to the parent page.
 * The parent uses this to gate the submit button and build the creation / update payload.
 */
export interface ProviderCardSelectorOutput {
  /** The currently selected provider type. */
  providerType: 'AzureOpenAI' | 'Ollama';
  /**
   * True only when the selected provider's sub-form reports that its configuration is
   * complete and valid. For Azure this requires a successful credential validation (FR-03).
   * For Ollama this requires non-empty server URL and default model name.
   */
  isValid: boolean;
  /** Azure OpenAI endpoint URL. Defined only when providerType is 'AzureOpenAI'. */
  endpoint?: string;
  /**
   * Azure OpenAI API key. Defined only when providerType is 'AzureOpenAI'.
   * Never written to localStorage, sessionStorage, or any log.
   */
  apiKey?: string;

  /**
   * The model identifier to use as the workspace default.
   * For Azure: the deployment name selected from the validated model list.
   * For Ollama: the free-text default model name entered by the user.
   */
  defaultModelId: string | null;
  /**
   * Models available to the AI Features model selectors.
   * For Azure: the list returned by the credential validation call (populated in FR-03).
   * For Ollama (creation page): [ defaultModelId ] when non-empty.
   */
  availableModels: string[];
  /**
   * `true` when the Azure sub-form is in "keep existing credentials" mode.
   * When `true`, the `EditWorkspacePage` save handler must NOT include `apiKey`
   * or `endpoint` in the `WorkspaceProviderUpdateRequest`.
   * Always `false` / `undefined` for Ollama and for the creation page.
   */
  keepExistingCredentials?: boolean;
}

interface ProviderCardSelectorProps {
  /**
   * Called with the current output whenever provider selection or sub-form state changes.
   * Receives null when the provider selection is reset (e.g., when switching cards).
   */
  onChange: (output: ProviderCardSelectorOutput | null) => void;
  /**
   * Workspace ID. Undefined on the creation page; passed down to OllamaConfigForm so
   * FR-04 can gate model-pull management to the edit page only.
   */
  workspaceId?: string;
  /**
   * Pre-selects a provider card on mount. Used by EditWorkspacePage to reflect the
   * workspace's existing provider type. Absent on the creation page.
   */
  initialProviderType?: 'AzureOpenAI' | 'Ollama';
  /**
   * Pre-populated model list for Azure keep-existing mode.
   * Sourced from `WorkspaceProviderConfig.models`. Forwarded to `AzureOpenAIConfigForm`.
   */
  initialModels?: string[];
  /**
   * Pre-selected default model identifier for Azure keep-existing mode.
   * Sourced from `workspace.defaultModelId`. Forwarded to `AzureOpenAIConfigForm`.
   */
  initialModelId?: string | null;
  /**
   * Whether stored Azure credentials are currently valid (from the validate endpoint).
   * Forwarded to `AzureOpenAIConfigForm` to activate the "Use existing credentials" toggle.
   */
  existingCredentialsValid?: boolean;
  /**
   * Pre-fills the Ollama default model name input.
   * Sourced from `workspace.defaultModelId`. Forwarded to `OllamaConfigForm`.
   */
  initialDefaultModelName?: string;
  /**
   * Pre-fills the Ollama server URL endpoint input.
   * Sourced from `WorkspaceProviderConfig.ollamaBaseUrl`. Forwarded to `OllamaConfigForm`.
   * Absent (or null) on the creation page — input starts empty.
   */
  initialEndpoint?: string | null;
  /**
   * Whether the current authenticated user is the workspace owner.
   * Forwarded to `OllamaConfigForm` to gate model pull and delete controls.
   * Absent on the creation page — all model management is unavailable.
   */
  isOwner?: boolean;
  /**
   * Whether the stored Ollama server credentials passed the live probe on page load.
   * `true`  → server reachable; pre-populate model dropdown immediately.
   * `false` → server unreachable; show an amber warning banner.
   * `undefined` → status unknown (creation page); no banner or pre-population.
   * Forwarded to `OllamaConfigForm` to enable model pre-population on the edit page.
   */
  initialIsServerValid?: boolean;
  /**
   * Called whenever the user clicks a different provider card.
   * Used by EditWorkspacePage to track type-switch state for validation and model-clearing.
   */
  onProviderTypeChange?: (type: 'AzureOpenAI' | 'Ollama') => void;
}

const ProviderCardSelector: React.FC<ProviderCardSelectorProps> = ({
  onChange,
  workspaceId,
  initialProviderType,
  initialModels,
  initialModelId,
  existingCredentialsValid,
  initialDefaultModelName,
  initialEndpoint,
  isOwner,
  initialIsServerValid,
  onProviderTypeChange,
}) => {
  // Pre-select the provider card when arriving from the edit page.
  const [selected, setSelected] = useState<'AzureOpenAI' | 'Ollama' | null>(
    initialProviderType ?? null
  );

  // True only when the currently selected card matches the workspace's original provider.
  // When false, initial credential/model props are withheld from sub-forms so that
  // switching to a different provider always shows empty credential inputs.
  const isOriginalProvider = selected === initialProviderType;

  const selectProvider = (provider: 'AzureOpenAI' | 'Ollama') => {
    if (selected === provider) return;
    setSelected(provider);
    // Discard previous sub-form state and notify parent that validity has been reset.
    onChange(null);
    onProviderTypeChange?.(provider);
  };

  const handleAzureChange = (state: AzureOpenAIConfigFormState) => {
    onChange({
      providerType: 'AzureOpenAI',
      isValid: state.isValid,
      endpoint: state.endpoint,
      apiKey: state.apiKey,
      defaultModelId: state.selectedModelId,
      availableModels: state.availableModels,
      keepExistingCredentials: state.keepExistingCredentials,
    });
  };

  const handleOllamaChange = (state: OllamaConfigFormState) => {
    onChange({
      providerType: 'Ollama',
      isValid: state.isValid,
      endpoint: state.endpoint,
      defaultModelId: state.selectedModelId,
      availableModels: state.availableModels,
    });
  };

  return (
    <div className="space-y-3">
      {/* Provider card radio group */}
      <div className="grid grid-cols-2 gap-3">
        {/* Azure OpenAI Card */}
        <button
          type="button"
          onClick={() => selectProvider('AzureOpenAI')}
          className={`relative p-4 rounded-lg border text-left transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary ${
            selected === 'AzureOpenAI'
              ? 'border-primary bg-primary/10 shadow-md shadow-primary/10'
              : 'border-border bg-surfaceHighlight/30 hover:border-primary/50 hover:bg-surfaceHighlight/50'
          }`}
          aria-pressed={selected === 'AzureOpenAI'}
        >
          {selected === 'AzureOpenAI' && (
            <CheckCircle2 className="absolute top-2 right-2 w-4 h-4 text-primary" />
          )}
          <Cloud className="w-5 h-5 text-primary mb-2" />
          <p className="text-sm font-semibold text-text">Azure OpenAI</p>
          <p className="text-[10px] text-textMuted mt-0.5">Microsoft Azure</p>
        </button>

        {/* Ollama Card */}
        <button
          type="button"
          onClick={() => selectProvider('Ollama')}
          className={`relative p-4 rounded-lg border text-left transition-all focus:outline-none focus-visible:ring-2 focus-visible:ring-primary ${
            selected === 'Ollama'
              ? 'border-primary bg-primary/10 shadow-md shadow-primary/10'
              : 'border-border bg-surfaceHighlight/30 hover:border-primary/50 hover:bg-surfaceHighlight/50'
          }`}
          aria-pressed={selected === 'Ollama'}
        >
          {selected === 'Ollama' && (
            <CheckCircle2 className="absolute top-2 right-2 w-4 h-4 text-primary" />
          )}
          <Server className="w-5 h-5 text-primary mb-2" />
          <p className="text-sm font-semibold text-text">Ollama</p>
          <p className="text-[10px] text-textMuted mt-0.5">Self-hosted</p>
        </button>
      </div>

      {/* Sub-form: only the active provider's form is in the DOM at any time */}
      {/* Initial credential/model props are forwarded ONLY when the selected card matches
          the workspace's original provider type. Switching to a different provider always
          shows empty inputs so the user must supply fresh credentials. */}
      {selected === 'AzureOpenAI' && (
        <AzureOpenAIConfigForm
          onChange={handleAzureChange}
          existingCredentialsValid={isOriginalProvider ? existingCredentialsValid : undefined}
          initialModels={isOriginalProvider ? initialModels : undefined}
          initialModelId={isOriginalProvider ? initialModelId : undefined}
        />
      )}
      {selected === 'Ollama' && (
        <OllamaConfigForm
          workspaceId={workspaceId}
          onChange={handleOllamaChange}
          initialDefaultModelName={isOriginalProvider ? initialDefaultModelName : undefined}
          initialEndpoint={isOriginalProvider ? initialEndpoint : null}
          initialModels={isOriginalProvider ? initialModels : undefined}
          initialIsServerValid={isOriginalProvider ? (initialIsServerValid ?? existingCredentialsValid) : undefined}
          isOwner={isOwner}
        />
      )}
    </div>
  );
};

export default ProviderCardSelector;
