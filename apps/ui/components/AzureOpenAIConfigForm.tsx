import React, { useState, useEffect } from 'react';
import { Loader2 } from 'lucide-react';
import { discoverAzureModels } from '../services/workspaceService';

/**
 * State surfaced upward by AzureOpenAIConfigForm.
 * FR-03 implements full credential validation and model discovery;
 * this interface MUST NOT change between the stub and the FR-03 implementation.
 */
export interface AzureOpenAIConfigFormState {
  /** Azure OpenAI endpoint URL entered by the user. */
  endpoint: string;
  /** Azure OpenAI API key entered by the user. Never stored beyond this component's state. */
  apiKey: string;
  /** The model deployment identifier selected after a successful validation. Null until validated. */
  selectedModelId: string | null;
  /** Model deployments returned by the validation call. Empty until FR-03 implements validation. */
  availableModels: string[];
  /**
   * True only when credentials have been successfully validated AND a model is selected,
   * OR when the user has chosen to keep the existing stored credentials (keepExistingCredentials = true).
   */
  isValid: boolean;
  /**
   * True when the user selected the "Use existing stored credentials" checkbox.
   * When true, the caller must omit `apiKey` from the provider update request
   * (i.e., leave the field as `undefined` in `WorkspaceProviderUpdateRequest`).
   */
  keepExistingCredentials: boolean;
}

interface AzureOpenAIConfigFormProps {
  /** Called whenever the form state changes so the parent can update its validity gate. */
  onChange: (state: AzureOpenAIConfigFormState) => void;
  /**
   * When `true`, a "Use existing stored credentials" toggle appears at the top of the form.
   * Set to `true` on the EditWorkspacePage when the validate endpoint confirms stored creds are valid.
   * Absent (undefined / false) on the CreateWorkspacePage — no behaviour change.
   */
  existingCredentialsValid?: boolean;
  /**
   * Pre-populated model list shown in the Model dropdown when keep-existing mode is active.
   * Sourced from `WorkspaceProviderConfig.models` (the live list from the validate endpoint).
   */
  initialModels?: string[];
  /**
   * Pre-selected model identifier in the Model dropdown when keep-existing mode is active.
   * Sourced from `workspace.defaultModelId`.
   */
  initialModelId?: string | null;
}

const AzureOpenAIConfigForm: React.FC<AzureOpenAIConfigFormProps> = ({
  onChange,
  existingCredentialsValid,
  initialModels,
  initialModelId,
}) => {
  // Keep-existing mode is active by default when stored credentials are confirmed valid
  // and an initial model list is available (edit page only).
  const canKeepExisting =
    (existingCredentialsValid ?? false) && (initialModels?.length ?? 0) > 0;

  const [keepExistingCredentials, setKeepExistingCredentials] = useState(canKeepExisting);
  const [endpoint, setEndpoint] = useState('');
  const [apiKey, setApiKey] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [availableModels, setAvailableModels] = useState<string[]>(
    canKeepExisting ? (initialModels ?? []) : []
  );
  const [selectedModelId, setSelectedModelId] = useState<string | null>(
    canKeepExisting ? (initialModelId ?? null) : null
  );

  // "Validate & Load Models" is enabled only when both fields are non-empty and no call is in flight.
  const isValidateEnabled =
    endpoint.trim().length > 0 && apiKey.trim().length > 0 && !isLoading;

  const emitChange = (
    ep: string,
    ak: string,
    models: string[],
    modelId: string | null,
    keepExisting: boolean = keepExistingCredentials
  ) => {
    onChange({
      endpoint: ep,
      apiKey: ak,
      selectedModelId: modelId,
      availableModels: models,
      // Keep-existing mode: valid when a model is selected (no fresh validation needed).
      // Normal mode: valid only after a successful validation AND a model is selected.
      isValid: keepExisting ? modelId !== null : models.length > 0 && modelId !== null,
      keepExistingCredentials: keepExisting,
    });
  };

  // Emit initial state on mount if the form starts in keep-existing mode (edit page).
  // This ensures ProviderCardSelector (and ultimately EditWorkspacePage) receives
  // isValid: true without requiring any user interaction.
  useEffect(() => {
    if (keepExistingCredentials) {
      emitChange('', '', initialModels ?? [], initialModelId ?? null, true);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []); // Intentionally runs only once on mount.

  /**
   * Clears previously validated model data and notifies the parent.
   * Called whenever the user edits endpoint or apiKey after a successful validation,
   * satisfying Scenario 5 from fr.md.
   */
  const clearValidationState = (ep: string, ak: string) => {
    setAvailableModels([]);
    setSelectedModelId(null);
    setError(null);
    emitChange(ep, ak, [], null);
  };

  const handleKeepExistingChange = (checked: boolean) => {
    setKeepExistingCredentials(checked);
    if (checked) {
      // Restore the initial models/model selection and mark as keep-existing.
      setAvailableModels(initialModels ?? []);
      setSelectedModelId(initialModelId ?? null);
      setEndpoint('');
      setApiKey('');
      setError(null);
      emitChange('', '', initialModels ?? [], initialModelId ?? null, true);
    } else {
      // Transition to fresh-validation mode — clear all model state.
      setAvailableModels([]);
      setSelectedModelId(null);
      emitChange(endpoint, apiKey, [], null, false);
    }
  };

  const handleEndpointChange = (value: string) => {
    setEndpoint(value);
    // If the model list is visible, clear it — the user must re-validate.
    if (availableModels.length > 0 || selectedModelId !== null) {
      clearValidationState(value, apiKey);
    } else {
      emitChange(value, apiKey, [], null);
    }
  };

  const handleApiKeyChange = (value: string) => {
    setApiKey(value);
    // If the model list is visible, clear it — the user must re-validate.
    if (availableModels.length > 0 || selectedModelId !== null) {
      clearValidationState(endpoint, value);
    } else {
      emitChange(endpoint, value, [], null);
    }
  };

  const handleValidate = async () => {
    if (!isValidateEnabled) return;

    setIsLoading(true);
    setError(null);
    setAvailableModels([]);
    setSelectedModelId(null);

    try {
      // discoverAzureModels in workspaceService.ts sanitises errors — apiKey never appears in thrown messages.
      const models = await discoverAzureModels(endpoint.trim(), apiKey.trim());
      setAvailableModels(models);
      // Do not auto-select a model; parent isValid remains false until the user picks one.
      emitChange(endpoint, apiKey, models, null);
    } catch (err) {
      const message =
        err instanceof Error
          ? err.message
          : 'Validation failed. Please check your credentials and endpoint.';
      setError(message);
      emitChange(endpoint, apiKey, [], null);
    } finally {
      setIsLoading(false);
    }
  };

  const handleModelSelect = (modelId: string) => {
    setSelectedModelId(modelId);
    emitChange(endpoint, apiKey, availableModels, modelId);
  };

  return (
    <div className="space-y-3 mt-3 p-4 bg-surfaceHighlight/50 border border-border rounded-lg">
      {/* Keep-existing credentials banner (edit page only — shown when stored credentials are valid) */}
      {existingCredentialsValid && (
        <div className="flex items-start gap-3 p-3 bg-emerald-500/10 border border-emerald-500/30 rounded-lg">
          <input
            type="checkbox"
            id="keepExistingCredentials"
            checked={keepExistingCredentials}
            onChange={(e) => handleKeepExistingChange(e.target.checked)}
            className="mt-0.5 h-4 w-4 accent-primary cursor-pointer"
          />
          <label htmlFor="keepExistingCredentials" className="text-xs text-emerald-600 dark:text-emerald-400 cursor-pointer leading-relaxed">
            <span className="font-semibold">Use existing stored credentials</span> — credentials
            have been verified as valid. Uncheck to enter new credentials.
          </label>
        </div>
      )}

      {/* Credential inputs and validate button — hidden when using existing credentials */}
      {!keepExistingCredentials && (
        <>
          {/* Endpoint input */}
          <div className="space-y-1.5">
            <label className="text-[10px] font-semibold text-textMuted uppercase">Endpoint</label>
            <input
              type="text"
              value={endpoint}
              disabled={isLoading}
              onChange={(e) => handleEndpointChange(e.target.value)}
              placeholder="https://your-resource.openai.azure.com/"
              className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
            />
          </div>

          {/* API Key input — type="password" ensures characters are obscured in the DOM */}
          <div className="space-y-1.5">
            <label className="text-[10px] font-semibold text-textMuted uppercase">API Key</label>
            <input
              type="password"
              value={apiKey}
              disabled={isLoading}
              onChange={(e) => handleApiKeyChange(e.target.value)}
              placeholder="Enter your Azure OpenAI API key"
              className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
              autoComplete="new-password"
            />
          </div>

          {/* Validate button — disabled until both fields are non-empty; loading state during the call */}
          <button
            type="button"
            disabled={!isValidateEnabled}
            onClick={handleValidate}
            className="flex items-center gap-2 px-3 py-2 rounded-md text-sm font-medium bg-primary text-white disabled:opacity-40 disabled:cursor-not-allowed hover:bg-primary/90 transition-colors"
          >
            {isLoading && <Loader2 size={14} className="animate-spin" />}
            {isLoading ? 'Validating…' : 'Validate & Load Models'}
          </button>
        </>
      )}

      {/* Inline validation error — rendered below the button; never contains the apiKey value */}
      {error && (
        <p className="text-xs text-red-400 mt-1">{error}</p>
      )}

      {/* Model dropdown — only visible when the validation call succeeded and returned models */}
      {availableModels.length > 0 && (
        <div className="space-y-1.5">
          <label className="text-[10px] font-semibold text-textMuted uppercase">Default Model</label>
          <select
            value={selectedModelId ?? ''}
            onChange={(e) => handleModelSelect(e.target.value)}
            className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
          >
            <option value="" disabled>
              Select a model deployment…
            </option>
            {availableModels.map((model) => (
              <option key={model} value={model}>
                {model}
              </option>
            ))}
          </select>
        </div>
      )}
    </div>
  );
};

export default AzureOpenAIConfigForm;
