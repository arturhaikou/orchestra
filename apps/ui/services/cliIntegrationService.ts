import { AiCliIntegration, CreateCliIntegrationRequest, UpdateCliIntegrationRequest, ModelMetadataDto } from '../types';
import { getToken } from './authService';

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/ai-cli-integrations`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`,
});

export const getCliIntegrations = async (workspaceId: string): Promise<AiCliIntegration[]> => {
  const response = await fetch(`${API_BASE_URL}?workspaceId=${workspaceId}`, {
    headers: getAuthHeaders(),
  });
  if (!response.ok) throw new Error('Failed to fetch CLI integrations');
  return response.json();
};

export const getCliIntegration = async (workspaceId: string, id: string): Promise<AiCliIntegration> => {
  const response = await fetch(`${API_BASE_URL}/${id}?workspaceId=${workspaceId}`, {
    headers: getAuthHeaders(),
  });
  if (!response.ok) throw new Error('Failed to fetch CLI integration');
  return response.json();
};

export const createCliIntegration = async (data: CreateCliIntegrationRequest): Promise<AiCliIntegration> => {
  const response = await fetch(API_BASE_URL, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });
  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message = (errorData as any)?.error || (errorData as any)?.message || response.statusText;
    throw new Error(message);
  }
  return response.json();
};

export const updateCliIntegration = async (id: string, data: UpdateCliIntegrationRequest): Promise<AiCliIntegration> => {
  const response = await fetch(`${API_BASE_URL}/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });
  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message = (errorData as any)?.error || (errorData as any)?.message || response.statusText;
    throw new Error(message);
  }
  return response.json();
};

export const deleteCliIntegration = async (workspaceId: string, id: string): Promise<void> => {
  const response = await fetch(`${API_BASE_URL}/${id}?workspaceId=${workspaceId}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });
  if (!response.ok) throw new Error('Failed to delete CLI integration');
};

export const discoverCopilotModels = async (
  credential: string | null,
  useLoggedInUser: boolean,
  workingDirectory: string,
  cliPath: string | null = null,
): Promise<string[]> => {
  const response = await fetch(`${API_BASE_URL}/models`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify({ credential, useLoggedInUser, workingDirectory, cliPath }),
  });
  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message = (errorData as any)?.error || 'Failed to discover models';
    throw new Error(message);
  }
  const models = await response.json();
  return models.map((m: any) => m.id);
};

export const discoverModelsForIntegration = async (
  workspaceId: string,
  integrationId: string,
): Promise<string[]> => {
  const models = await discoverModelsMetadataForIntegration(workspaceId, integrationId);
  return models.map(m => m.id);
};

export const discoverModelsMetadataForIntegration = async (
  workspaceId: string,
  integrationId: string,
): Promise<ModelMetadataDto[]> => {
  const response = await fetch(`${API_BASE_URL}/${integrationId}/models?workspaceId=${workspaceId}`, {
    headers: getAuthHeaders(),
  });
  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message = (errorData as any)?.error || 'Failed to discover models';
    throw new Error(message);
  }
  return response.json();
};
