
import { Integration, IntegrationType } from '../types';
import { getToken } from './authService';

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/integrations`;

export interface IntegrationDTO {
  name: string;
  type: IntegrationType;
  provider: string;
  url: string;
  username?: string;
  apiKey: string;
  filterQuery?: string;
  vectorize?: boolean;
  connected?: boolean;  // Optional connection status
}

export interface CreateIntegrationDTO extends IntegrationDTO {
  workspaceId: string;
}

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getIntegrations = async (workspaceId: string): Promise<Integration[]> => {
  try {
    const response = await fetch(`${API_BASE_URL}?workspaceId=${workspaceId}`, {
        headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error("API Error");
    return await response.json();
  } catch (error) {
    console.error('Failed to fetch integrations:', error);
    return [];
  }
};

export const createIntegration = async (data: CreateIntegrationDTO): Promise<Integration> => {
  try {
    const response = await fetch(API_BASE_URL, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Failed to create integration:', error);
    throw error;
  }
};

export const updateIntegration = async (id: string, data: IntegrationDTO): Promise<Integration> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Failed to update integration:', error);
    throw error;
  }
};

export const deleteIntegration = async (id: string): Promise<void> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }
  } catch (error) {
    console.error('Failed to delete integration:', error);
    throw error;
  }
};

export interface ConnectionTestRequest {
  provider: string;
  url: string;
  username?: string;
  apiKey: string;
  jiraType?: string;  // "Cloud" or "OnPremise" for Jira integrations
}

export const testIntegrationConnection = async (data: ConnectionTestRequest): Promise<void> => {
  try {
    const response = await fetch(`${API_BASE_URL}/validate-connection`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      const errorMessage = (errorData as any)?.message || `Connection failed: ${response.statusText}`;
      throw new Error(errorMessage);
    }
  } catch (error) {
    console.error('Failed to test integration connection:', error);
    throw error;
  }
};
