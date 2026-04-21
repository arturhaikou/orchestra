import React, { useState, useEffect, useCallback, useRef } from 'react';
import { Loader2, RefreshCw, AlertTriangle, Info } from 'lucide-react';
import { discoverOllamaModels } from '../services/workspaceService';

/**
 * State surfaced upward by OllamaConfigForm.
 * FR-04 extends the model-pull management section on the edit page;
 * this interface MUST NOT change between the stub and the FR-04 implementation.
 */
export interface OllamaConfigFormState {
  /** Ollama server base URL entered by the user (e.g., http://localhost:11434). */
  endpoint: string;
  /**
   * The model identifier selected by the user from the discovery list.
   * Null until the user completes a discovery call and selects a model.
   */
  selectedModelId: string | null;
  /**
   * Model names available to the AI Features section.
   * On the creation page: populated from the pre-discovery endpoint response (FR-02).
   * On the edit page: still derived from the workspace model management list (Available models).
   */
  availableModels: string[];
  /**
   * True only when endpoint is non-empty AND selectedModelId is non-null.
   * Both conditions must hold — endpoint validated and model chosen.
   */
  isValid: boolean;
}

interface OllamaConfigFormProps {
  /**
   * Workspace ID. Undefined on the creation page (no workspace exists yet).
   * When present, enables the model-pull management section (edit page).
   */
  workspaceId?: string;
  /** Called whenever the form state changes so the parent can update its validity gate. */
  onChange: (state: OllamaConfigFormState) => void;
  /**
   * Pre-fills the "Default Model" text input with the workspace's stored default model.
   * Sourced from `workspace.defaultModelId` on the edit page.
   * Absent on the creation page — input starts empty.
   */
  initialDefaultModelName?: string;
  /**
   * Pre-fills the Ollama server URL endpoint input with the workspace's stored base URL.
   * Sourced from `WorkspaceProviderConfig.ollamaBaseUrl` on the edit page.
   * Absent (or null) on the creation page — input starts empty.
   * The pre-populated value does not trigger automatic model discovery.
   */
  initialEndpoint?: string | null;
  /**
   * Pre-populates the model list from the provider validation result so the
   * Default Model dropdown is visible immediately on the edit page without
   * requiring the user to click "Validate and Get Models" first.
   * Sourced from `WorkspaceProviderConfig.models`. Absent on the creation page.
   */
  initialModels?: string[];
  /**
   * Whether the stored Ollama server credentials passed the live probe on page load.
   * `true`  → server reachable; pre-populate model dropdown immediately.
   * `false` → server unreachable; show an amber warning banner.
   * `undefined` → status unknown (creation page); no banner or pre-population.
   */
  initialIsServerValid?: boolean;
  /**
   * Whether the current authenticated user is the workspace owner.
   * When false or absent, pull and delete controls are hidden; model list is read-only.
   */
  isOwner?: boolean;
}

const OllamaConfigForm: React.FC<OllamaConfigFormProps> = ({
  onChange,
  initialDefaultModelName,
  initialEndpoint,
  initialModels,
  initialIsServerValid,
}) => {
  // Pre-populate the endpoint from the stored Ollama server URL when editing an existing
  // workspace. Falls back to an empty string on the creation page (initialEndpoint absent).
  const [endpoint, setEndpoint] = useState(initialEndpoint ?? '');

  // ── Discovery state (pre-discovery endpoint — FR-02) ───────────────────────
  const [fetchedModels, setFetchedModels] = useState<string[]>([]);
  const [selectedModelId, setSelectedModelId] = useState<string | null>(null);
  const [isDiscovering, setIsDiscovering] = useState(false);
  const [discoveryError, setDiscoveryError] = useState<string | null>(null);
  // hasDiscovered: true after any successful discovery call; controls button label.
  const [hasDiscovered, setHasDiscovered] = useState(false);

  // ─── Helpers ────────────────────────────────────────────────────────────────

  // Store the latest onChange callback in a ref to decouple effect dependencies
  const onChangeRef = useRef(onChange);
  useEffect(() => {
    onChangeRef.current = onChange;
  }, [onChange]);

  const emitChange = useCallback(
    (url: string, modelId: string | null, discoveredModels: string[]) => {
      const trimmedUrl = url.trim();
      onChangeRef.current({
        endpoint: trimmedUrl,
        selectedModelId: modelId,
        availableModels: discoveredModels,
        isValid: trimmedUrl.length > 0 && modelId !== null,
      });
    },
    []
  );

  // ─── Pre-populate model dropdown when server is reachable (edit page only) ────
  // Fires once on mount when `initialIsServerValid` is true and `initialModels` is
  // non-empty. Sets `hasDiscovered = true` so the Default Model dropdown renders
  // immediately without requiring the user to click "Validate and Get Models".

  useEffect(() => {
    if (!initialIsServerValid || !initialModels || initialModels.length === 0) return;
    setFetchedModels(initialModels);
    setHasDiscovered(true);
    const preSelect =
      initialDefaultModelName && initialModels.includes(initialDefaultModelName)
        ? initialDefaultModelName
        : null;
    setSelectedModelId(preSelect);
    // Use `initialEndpoint ?? ''` because `endpoint` state may not reflect the prop yet
    // on the very first render tick.
    emitChange(initialEndpoint ?? '', preSelect, initialModels);
  }, [initialIsServerValid, initialModels, initialDefaultModelName, initialEndpoint, emitChange]);

  // ─── Discover models (pre-discovery endpoint — FR-02) ────────────────────────────

  const handleDiscoverModels = async () => {
    if (endpoint.trim() === '' || isDiscovering) return;

    setIsDiscovering(true);
    setDiscoveryError(null);
    setFetchedModels([]);
    setSelectedModelId(null);
    emitChange(endpoint, null, []);

    try {
      const result = await discoverOllamaModels(endpoint.trim());
      if (!result.isValid) {
        setDiscoveryError(result.errorMessage ?? 'Validation failed. Please check the server URL.');
        setHasDiscovered(false);
      } else {
        setFetchedModels(result.models);
        setHasDiscovered(true);
        // Pre-select the existing default model when editing a workspace (initialDefaultModelName).
        const preSelect =
          initialDefaultModelName && result.models.includes(initialDefaultModelName)
            ? initialDefaultModelName
            : null;
        setSelectedModelId(preSelect);
        emitChange(endpoint, preSelect, result.models);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to reach the Ollama server.';
      setDiscoveryError(message);
      setHasDiscovered(false);
    } finally {
      setIsDiscovering(false);
    }
  };

  // ─── Render ───────────────────────────────────────────────────────────────────

  return (
    <div className="space-y-3 mt-3 p-4 bg-surfaceHighlight/50 border border-border rounded-lg">
      {/* Server URL + Validate button */}
      <div className="space-y-1.5">
        <label className="text-[10px] font-semibold text-textMuted uppercase">Ollama Base URL</label>
        <div className="flex items-center gap-2">
          <input
            type="text"
            value={endpoint}
            onChange={(e) => {
              const newUrl = e.target.value;
              setEndpoint(newUrl);
              if (hasDiscovered) {
                // URL changed after a successful fetch — reset discovery state entirely.
                setFetchedModels([]);
                setSelectedModelId(null);
                setDiscoveryError(null);
                setHasDiscovered(false);
                emitChange(newUrl, null, []);
              } else {
                emitChange(newUrl, selectedModelId, fetchedModels);
              }
            }}
            placeholder="http://localhost:11434"
            className="flex-1 bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
          />
          <button
            type="button"
            disabled={endpoint.trim() === '' || isDiscovering}
            onClick={handleDiscoverModels}
            className="flex items-center gap-1.5 px-3 py-2 rounded-md text-sm font-medium bg-primary/10 text-primary border border-primary/30 disabled:opacity-40 disabled:cursor-not-allowed hover:bg-primary/20 transition-colors whitespace-nowrap shrink-0"
          >
            {isDiscovering ? (
              <Loader2 size={13} className="animate-spin" />
            ) : hasDiscovered ? (
              <RefreshCw size={13} />
            ) : null}
            {isDiscovering ? 'Fetching…' : hasDiscovered ? 'Refresh Models' : 'Validate and Get Models'}
          </button>
        </div>
      </div>

      {/* Unreachable server banner — shown when the stored server failed the live probe */}
      {initialEndpoint && initialIsServerValid === false && !hasDiscovered && (
        <div className="flex items-start gap-2 p-2.5 rounded-md bg-amber-400/10 border border-amber-400/30">
          <AlertTriangle size={14} className="text-amber-400 mt-0.5 shrink-0" />
          <p className="text-xs text-amber-300">
            The Ollama server is currently unreachable.
            Check that the server is running, then click &ldquo;Validate and Get Models&rdquo; to retry.
          </p>
        </div>
      )}

      {/* Discovery error */}
      {discoveryError && (
        <p className="text-xs text-red-400">{discoveryError}</p>
      )}

      {/* Zero-models informational message — shown when discovery succeeds but no models are installed */}
      {hasDiscovered && !isDiscovering && fetchedModels.length === 0 && !discoveryError && (
        <div className="flex items-start gap-2 p-3 bg-blue-500/5 border border-blue-500/20 rounded-md">
          <Info size={14} className="text-blue-400 mt-0.5 shrink-0" />
          <p className="text-xs text-textMuted">
            No models are installed on this server. Please pull at least one model using the Ollama
            CLI, then click &ldquo;Validate and Get Models&rdquo; again.
          </p>
        </div>
      )}

      {/* Model selection — visible after a successful discovery with ≥1 model */}
      {hasDiscovered && !isDiscovering && fetchedModels.length > 0 && (
        <div className="space-y-1.5">
          <label className="text-[10px] font-semibold text-textMuted uppercase">Default Model</label>
          <select
            value={selectedModelId ?? ''}
            onChange={(e) => {
              const modelId = e.target.value || null;
              setSelectedModelId(modelId);
              emitChange(endpoint, modelId, fetchedModels);
            }}
            className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
          >
            <option value="" disabled>Select a model…</option>
            {fetchedModels.map((m) => (
              <option key={m} value={m}>{m}</option>
            ))}
          </select>
        </div>
      )}


    </div>
  );
};

export default OllamaConfigForm;
