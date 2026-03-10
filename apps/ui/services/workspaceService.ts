
import { Workspace } from '../types';
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
      `${API_BASE_URL}/${workspaceId}/ai/models`,
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
