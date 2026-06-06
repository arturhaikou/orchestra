
import { Workspace, WorkspaceModel, WorkspaceProviderConfig, WorkspaceProviderUpdateRequest, OllamaPullStatus } from '../types';
import { getToken } from './authService';

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/workspaces`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getWorkspaces = async (): Promise<Workspace[]> => {
  try {
    const response = await fetch(API_BASE_URL, {
      headers: getAuthHeaders()
    });

    if (response.status === 401) throw new Error('UNAUTHORIZED');

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }
    return await response.json();
  } catch (error) {
    throw error;
  }
};

export const createWorkspace = async (
  name: string,
  isAiSummarizationEnabled?: boolean,
  isCustomerSatisfactionAnalysisEnabled?: boolean,
  aiSummarizationModelId?: string,
  customerSatisfactionAnalysisModelId?: string
): Promise<Workspace> => {
  try {
    const response = await fetch(API_BASE_URL, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        name,
        ...(isAiSummarizationEnabled !== undefined && { isAiSummarizationEnabled }),
        ...(isCustomerSatisfactionAnalysisEnabled !== undefined && { isCustomerSatisfactionAnalysisEnabled }),
        ...(aiSummarizationModelId !== undefined && { aiSummarizationModelId }),
        ...(customerSatisfactionAnalysisModelId !== undefined && { customerSatisfactionAnalysisModelId }),
      }),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }

    return await response.json();
  } catch (error) {
    throw error;
  }
};

/**
 * Creates a new workspace with a required AI provider configuration.
 * Used by CreateWorkspacePage (FR-02). Transmits provider credentials (apiKey / ollamaBaseUrl)
 * only over HTTPS in a single request body — never stored locally.
 *
 * @param name - Workspace name (2–100 characters, trimmed by caller)
 * @param providerType - 'AzureOpenAI' or 'Ollama'
 * @param providerCredentials - Provider-specific credential fields; unused fields should be undefined
 * @param defaultModelId - The model identifier to use as the workspace default
 * @param isAiSummarizationEnabled - Optional AI summarisation feature flag
 * @param isCustomerSatisfactionAnalysisEnabled - Optional customer satisfaction analysis flag
 * @param aiSummarizationModelId - Optional model override for AI summarisation
 * @param customerSatisfactionAnalysisModelId - Optional model override for satisfaction analysis
 * @returns The created Workspace object
 */
export const createWorkspaceWithProvider = async (
  name: string,
  providerType: 'AzureOpenAI' | 'Ollama',
  providerCredentials: {
    endpoint?: string;
    apiKey?: string;
  },
  defaultModelId: string,
  isAiSummarizationEnabled?: boolean,
  isCustomerSatisfactionAnalysisEnabled?: boolean,
  aiSummarizationModelId?: string,
  customerSatisfactionAnalysisModelId?: string
): Promise<Workspace> => {
  const response = await fetch(API_BASE_URL, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify({
      name,
      providerType,
      ...(providerCredentials.endpoint ? { endpoint: providerCredentials.endpoint } : {}),
      ...(providerCredentials.apiKey ? { apiKey: providerCredentials.apiKey } : {}),
      defaultModelId,
      ...(isAiSummarizationEnabled !== undefined && { isAiSummarizationEnabled }),
      ...(isCustomerSatisfactionAnalysisEnabled !== undefined && { isCustomerSatisfactionAnalysisEnabled }),
      ...(aiSummarizationModelId !== undefined && { aiSummarizationModelId }),
      ...(customerSatisfactionAnalysisModelId !== undefined && { customerSatisfactionAnalysisModelId }),
    }),
  });

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) throw new Error('Not JSON');

  if (!response.ok) {
    try {
      const errorData = await response.json();
      throw new Error(errorData.detail || errorData.message || response.statusText);
    } catch {
      throw new Error(`Backend error: ${response.statusText}`);
    }
  }

  return await response.json();
};

export const updateWorkspace = async (
  id: string,
  name: string,
  isAiSummarizationEnabled?: boolean,
  isCustomerSatisfactionAnalysisEnabled?: boolean,
  aiSummarizationModelId?: string,
  customerSatisfactionAnalysisModelId?: string
): Promise<Workspace> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        name,
        ...(isAiSummarizationEnabled !== undefined && { isAiSummarizationEnabled }),
        ...(isCustomerSatisfactionAnalysisEnabled !== undefined && { isCustomerSatisfactionAnalysisEnabled }),
        ...(aiSummarizationModelId !== undefined && { aiSummarizationModelId }),
        ...(customerSatisfactionAnalysisModelId !== undefined && { customerSatisfactionAnalysisModelId }),
      }),
    });

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }
    return await response.json();
  } catch (error) {
    throw error;
  }
};

export const deleteWorkspace = async (id: string): Promise<void> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }
  } catch (error) {
    throw error;
  }
};

/**
 * Fetches the list of available AI models for a specific workspace.
 * The authenticated user must be a member of the workspace.
 * 
 * @param workspaceId - The ID of the workspace
 * @returns Promise resolving to an array of model identifier strings
 * @throws Error if the request fails or user is not a member
 */
export const fetchWorkspaceModels = async (workspaceId: string): Promise<string[]> => {
  try {
    const response = await fetch(
      `${API_BASE_URL}/${workspaceId}/provider/models`,
      {
        method: 'GET',
        headers: getAuthHeaders(),
      }
    );

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }

    const data = await response.json();
    // The backend returns { models: string[] }
    return data.models || [];
  } catch (error) {
    throw error;
  }
};

/**
 * Fetches the system startup-configured default AI model identifier.
 * Used to pre-select the default model in the Create Workspace modal.
 * 
 * @returns Promise resolving to the default model identifier string
 * @throws Error if the request fails
 */
export const fetchDefaultModel = async (): Promise<string> => {
  try {
    const response = await fetch(
      `${API_BASE_URL}/default-model`,
      {
        method: 'GET',
        headers: getAuthHeaders(),
      }
    );

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }

    const data = await response.json();
    // The backend returns { defaultModelId: string }
    return data.defaultModelId || 'gpt-4o-mini'; // Fallback to a reasonable default
  } catch (error) {
    throw error;
  }
};

/**
 * Fetches the list of available AI models from the platform without workspace scope.
 * Used in the Create Workspace modal where no workspace exists yet.
 * The authenticated user must be logged in, but no workspace membership is required.
 * 
 * @returns Promise resolving to an array of model identifier strings
 * @throws Error if the request fails
 */
export const fetchPlatformModels = async (): Promise<string[]> => {
  try {
    const response = await fetch(
      `${API_BASE_URL}/ai/models`,
      {
        method: 'GET',
        headers: getAuthHeaders(),
      }
    );

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }

    const data = await response.json();
    // The backend returns { models: string[] }
    return data.models || [];
  } catch (error) {
    throw error;
  }
};

/**
 * Fetches the list of WorkspaceModel records for a specific workspace.
 * Used by OllamaConfigForm on the edit page to populate the model list on mount.
 *
 * @param workspaceId - The ID of the workspace
 * @returns Promise resolving to a typed WorkspaceModel array
 * @throws Error if the request fails
 */
export const getWorkspaceModels = async (workspaceId: string): Promise<WorkspaceModel[]> => {
  const response = await fetch(`${API_BASE_URL}/${workspaceId}/models`, {
    method: 'GET',
    headers: getAuthHeaders(),
  });

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) throw new Error('Not JSON');

  if (!response.ok) {
    try {
      const errorData = await response.json();
      throw new Error(errorData.detail || errorData.message || response.statusText);
    } catch {
      throw new Error(`Backend error: ${response.statusText}`);
    }
  }

  const data = await response.json();
  return (data.models ?? []) as WorkspaceModel[];
};

/**
 * Initiates a model pull on the Ollama server for the given workspace.
 * Sends POST /v1/workspaces/{workspaceId}/models/pull with { modelName } in the body.
 * Expects 202 Accepted. Real-time progress arrives via SignalR (ModelPullProgress events).
 *
 * @param workspaceId - The ID of the workspace
 * @param modelName - The Ollama model name to pull (e.g., "llama3.2" or "llama3:8b")
 * @returns Promise that resolves on 202 Accepted
 * @throws Error on non-202 responses
 */
export const pullModel = async (workspaceId: string, modelName: string): Promise<void> => {
  const response = await fetch(`${API_BASE_URL}/${workspaceId}/models/pull`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify({ modelName }),
  });

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) throw new Error('Not JSON');

  if (!response.ok) {
    try {
      const errorData = await response.json();
      throw new Error(errorData.detail || errorData.message || response.statusText);
    } catch {
      throw new Error(`Backend error: ${response.statusText}`);
    }
  }
  // 202 Accepted — no response body to parse.
};

/**
 * Deletes a model from the Ollama server for the given workspace.
 * The model name is URL-encoded before being placed in the route segment because
 * Ollama model names may contain colons (e.g., "llama3:8b") and slashes.
 * Sends DELETE /v1/workspaces/{workspaceId}/models/{encodedModelName}.
 * Expects 204 No Content on success, 409 Conflict if the model is currently pulling.
 *
 * @param workspaceId - The ID of the workspace
 * @param modelName - The Ollama model name to delete
 * @throws Error (with '409' in the message) on Conflict; generic Error on other failures
 */
export const deleteModel = async (workspaceId: string, modelName: string): Promise<void> => {
  const encodedModelName = encodeURIComponent(modelName);
  const response = await fetch(`${API_BASE_URL}/${workspaceId}/models/${encodedModelName}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (response.status === 204) return; // Success — no body expected.

  if (response.status === 409) {
    throw new Error('409: Cannot delete a model that is currently pulling.');
  }

  if (!response.ok) {
    try {
      const contentType = response.headers.get('content-type');
      if (contentType && contentType.includes('application/json')) {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      }
    } catch (inner) {
      if (inner instanceof Error && inner.message.startsWith('409')) throw inner;
    }
    throw new Error(`Backend error: ${response.statusText}`);
  }
};

/**
 * Probes the backend Azure OpenAI provider discovery endpoint with the supplied credentials
 * and returns the list of deployment name strings on success.
 *
 * The apiKey is transmitted only in this single HTTP request body and is NEVER stored,
 * written to localStorage/sessionStorage, or included in any log.
 *
 * Error messages surfaced to the caller have the raw apiKey value removed before they are
 * returned, ensuring it cannot be displayed in the UI.
 *
 * @param endpoint - The Azure OpenAI resource endpoint URL
 * @param apiKey - The Azure OpenAI API key (transmitted once; never stored)
 * @returns Promise resolving to an array of Azure deployment name strings
 * @throws Error with a sanitised plain-language message on network or credential failure
 */
export const discoverAzureModels = async (
  endpoint: string,
  apiKey: string
): Promise<string[]> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/provider/azure/models`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ endpoint, apiKey }),
    }
  );

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) {
    throw new Error('Unexpected response from server. Please try again.');
  }

  if (!response.ok) {
    let message = `Validation failed (HTTP ${response.status}). Please check your credentials and endpoint.`;
    try {
      const errorData = await response.json();
      const raw: string = errorData.detail || errorData.message || errorData.error || '';
      if (raw) {
        // Strip the literal apiKey value from any backend error before surfacing it to the UI.
        const sanitised = raw.replace(
          new RegExp(apiKey.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'g'),
          '***'
        );
        message = sanitised;
      }
    } catch {
      // JSON parse failed — use the default message above.
    }
    throw new Error(message);
  }

  const data = await response.json();
  return (data.models as string[]) || [];
};

/**
 * Fetches the workspace's current AI provider configuration by probing
 * the stored credentials against the live provider.
 *
 * Calls `POST /v1/workspaces/{workspaceId}/provider/validate` with no request body.
 * The backend validates the stored (encrypted) credentials and returns:
 *   - `isValid`: whether the live probe succeeded
 *   - `providerType`: "AzureOpenAI" or "Ollama"
 *   - `models`: live list of model deployment / tag names
 *
 * Security note: Azure credentials (`endpoint`) are intentionally absent from the
 * backend response (Phase 2 FR-06 security constraint) and will be `undefined`.
 * The Ollama server URL (`ollamaBaseUrl`) is plaintext and IS returned for Ollama
 * workspaces; it will be `undefined` for Azure OpenAI workspaces.
 *
 * Error mapping:
 *   - 404 → "Workspace not found or access denied."
 *   - Other non-OK → backend error message or generic message
 *
 * @param workspaceId - The ID of the workspace
 * @returns `WorkspaceProviderConfig` with providerType, models, and isValid populated
 * @throws Error on 404 or other non-OK responses
 */
export const getWorkspaceProviderConfig = async (
  workspaceId: string
): Promise<WorkspaceProviderConfig> => {
  const response = await fetch(
    `${API_BASE_URL}/${workspaceId}/provider/validate`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      // No request body — the backend validates the stored server-side credentials.
    }
  );

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) {
    throw new Error('Unexpected response from server.');
  }

  if (response.status === 404) {
    throw new Error('Workspace not found or access denied.');
  }

  if (!response.ok) {
    try {
      const errorData = await response.json();
      throw new Error(errorData.detail || errorData.message || errorData.error || response.statusText);
    } catch (inner) {
      if (inner instanceof Error && inner.message !== response.statusText) throw inner;
      throw new Error(`Backend error: ${response.statusText}`);
    }
  }

  const data = await response.json();

  // Map ProviderValidationResult (backend shape) → WorkspaceProviderConfig (UI shape).
  // ollamaBaseUrl is populated by the backend for Ollama workspaces (FR-001).
  // endpoint (Azure encrypted URL) is intentionally absent from the response.
  return {
    providerType: data.providerType as 'AzureOpenAI' | 'Ollama',
    models: (data.models ?? []) as string[],
    isValid: data.isValid as boolean,
    ollamaBaseUrl: (data.ollamaBaseUrl as string | undefined) ?? undefined,
  };
};

/**
 * Replaces the workspace's AI provider configuration.
 *
 * Calls `PUT /v1/workspaces/{workspaceId}/provider` with a `WorkspaceProviderUpdateRequest` body.
 * Expects `204 No Content` on success.
 *
 * Security note: `request.apiKey` is transmitted in the HTTP request body over HTTPS only.
 * When the caller omits `apiKey` (passes `undefined`), the backend preserves the stored key.
 * The apiKey is NEVER written to localStorage, sessionStorage, or any log by this function.
 *
 * Error mapping:
 *   - 403 → "Access Denied: only the workspace owner can update the provider configuration."
 *   - 404 → "Workspace not found."
 *   - 422 → backend human-readable validation message (e.g., invalid credentials, model not in list)
 *   - Other non-OK → generic backend error
 *
 * @param workspaceId - The ID of the workspace
 * @param request - The provider reconfiguration payload
 * @throws Error with a descriptive message on any non-204 response
 */
export const updateWorkspaceProvider = async (
  workspaceId: string,
  request: WorkspaceProviderUpdateRequest
): Promise<void> => {
  const response = await fetch(
    `${API_BASE_URL}/${workspaceId}/provider`,
    {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        providerType: request.providerType,
        ...(request.endpoint !== undefined ? { endpoint: request.endpoint } : {}),
        // apiKey is omitted when undefined — server interprets absence as "keep existing key".
        ...(request.apiKey !== undefined && request.apiKey !== '' ? { apiKey: request.apiKey } : {}),
        defaultModelId: request.defaultModelId,
      }),
    }
  );

  if (response.status === 204) return; // Success — no response body.

  if (response.status === 403) {
    throw new Error(
      'Access Denied: only the workspace owner can update the provider configuration.'
    );
  }

  if (response.status === 404) {
    throw new Error('Workspace not found.');
  }

  const contentType = response.headers.get('content-type');
  let message = `Backend error: ${response.statusText}`;
  if (contentType && contentType.includes('application/json')) {
    try {
      const errorData = await response.json();
      message = errorData.detail || errorData.message || errorData.error || message;
    } catch {
      // JSON parse failed — use the default message.
    }
  }
  throw new Error(message);
};

/**
 * Calls the Ollama pre-discovery endpoint to validate an Ollama server URL and
 * retrieve its list of installed model tags.
 *
 * Calls `POST /v1/provider/ollama/models` with `{ endpoint }` in the body.
 * Returns a structured result so the caller can distinguish between a successful
 * response with zero models, a successful response with models, and a failure.
 *
 * Security: The endpoint URL is transmitted only over HTTPS and must never be
 * written to localStorage, sessionStorage, or any client-side log.
 *
 * @param endpoint - The Ollama server base URL entered by the user (e.g., "http://localhost:11434")
 * @returns An object with `isValid`, `models`, and `errorMessage` matching the FR-02 backend contract
 * @throws Error only on network-level failures (fetch itself throws); non-OK HTTP responses are
 *         mapped to `{ isValid: false, models: [], errorMessage: <reason> }` instead of throwing.
 */
export const discoverOllamaModels = async (
  endpoint: string
): Promise<{ isValid: boolean; models: string[]; errorMessage: string | null }> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/provider/ollama/models`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ endpoint }),
    }
  );

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) {
    return { isValid: false, models: [], errorMessage: 'Unexpected response from server. Please try again.' };
  }

  if (!response.ok) {
    let message = `Request failed (HTTP ${response.status}). Please check the server URL.`;
    try {
      const errorData = await response.json();
      const raw: string = errorData.detail || errorData.message || errorData.error || '';
      if (raw) message = raw;
    } catch {
      // JSON parse failed — use the default message above.
    }
    return { isValid: false, models: [], errorMessage: message };
  }

  const data = await response.json();
  return {
    isValid: data.isValid ?? true,
    models: (data.models as string[]) ?? [],
    errorMessage: data.errorMessage ?? null,
  };
};

/**
 * Initiates a stateless Ollama model pull against the supplied server endpoint.
 * Calls POST /v1/provider/ollama/models/pull with { endpoint, modelName }.
 * Expects 202 Accepted. Returns the pull operation identifier for status polling.
 *
 * @param endpoint - The Ollama server base URL (e.g., "http://localhost:11434")
 * @param modelName - The Ollama model identifier to pull (e.g., "llama3:latest")
 * @returns Promise resolving to { pullId: string }
 * @throws Error on non-202 responses
 */
export const startStatelessOllamaPull = async (
  endpoint: string,
  modelName: string
): Promise<{ pullId: string }> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/provider/ollama/models/pull`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ endpoint, modelName }),
    }
  );

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) {
    throw new Error('Unexpected response from server.');
  }

  if (!response.ok) {
    try {
      const errorData = await response.json();
      throw new Error(errorData.error || errorData.detail || errorData.message || response.statusText);
    } catch (inner) {
      if (inner instanceof Error && inner.message !== response.statusText) throw inner;
      throw new Error(`Backend error: ${response.statusText}`);
    }
  }

  return await response.json() as { pullId: string };
};

/**
 * Polls the status of a stateless Ollama model pull operation.
 * Calls GET /v1/provider/ollama/models/pull/{pullId}.
 * Returns null when the pullId is unknown or has expired (404).
 *
 * @param pullId - The pull operation identifier returned by startStatelessOllamaPull
 * @returns Promise resolving to OllamaPullStatus, or null if the operation is not found
 * @throws Error on non-200/404 responses
 */
export const getOllamaPullStatus = async (
  pullId: string
): Promise<OllamaPullStatus | null> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/provider/ollama/models/pull/${encodeURIComponent(pullId)}`,
    {
      method: 'GET',
      headers: getAuthHeaders(),
    }
  );

  if (response.status === 404) return null;

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) {
    throw new Error('Unexpected response from server.');
  }

  if (!response.ok) {
    try {
      const errorData = await response.json();
      throw new Error(errorData.error || errorData.detail || errorData.message || response.statusText);
    } catch (inner) {
      if (inner instanceof Error && inner.message !== response.statusText) throw inner;
      throw new Error(`Backend error: ${response.statusText}`);
    }
  }

  return await response.json() as OllamaPullStatus;
};
