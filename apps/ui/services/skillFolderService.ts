import { DiscoveredSkill, SkillFolder } from '../types';
import { getToken } from './authService';

const getApiBase = (workspaceId: string) =>
  `${import.meta.env.VITE_API_URL}/v1/workspaces/${workspaceId}/skill-folders`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`,
});

export const getSkillFolders = async (workspaceId: string): Promise<SkillFolder[]> => {
  try {
    const response = await fetch(getApiBase(workspaceId), {
      headers: getAuthHeaders(),
    });

    const contentType = response.headers.get('content-type');
    if (contentType && contentType.includes('text/html')) throw new Error('Not JSON');

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Failed to fetch skill folders:', error);
    return [];
  }
};

export const getSkillFolder = async (workspaceId: string, skillFolderId: string): Promise<SkillFolder> => {
  const response = await fetch(`${getApiBase(workspaceId)}/${skillFolderId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    if (response.status === 404) throw new Error('Skill folder not found');
    throw new Error(`Backend error: ${response.statusText}`);
  }

  return await response.json();
};

export const getSkillsInFolder = async (workspaceId: string, skillFolderId: string): Promise<DiscoveredSkill[]> => {
  const response = await fetch(`${getApiBase(workspaceId)}/${skillFolderId}/skills`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Backend error: ${response.statusText}`);
  }

  return await response.json();
};

export const createSkillFolder = async (
  workspaceId: string,
  data: { name: string; folderPath: string }
): Promise<SkillFolder> => {
  const response = await fetch(getApiBase(workspaceId), {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify({ workspaceId, ...data }),
  });

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) throw new Error('Not JSON');

  if (!response.ok) {
    throw new Error(`Backend error: ${response.statusText}`);
  }

  return await response.json();
};

export const updateSkillFolder = async (
  workspaceId: string,
  skillFolderId: string,
  data: { name: string; folderPath: string }
): Promise<SkillFolder> => {
  const response = await fetch(`${getApiBase(workspaceId)}/${skillFolderId}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(data),
  });

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('text/html')) throw new Error('Not JSON');

  if (!response.ok) {
    throw new Error(`Backend error: ${response.statusText}`);
  }

  return await response.json();
};

export const deleteSkillFolder = async (workspaceId: string, skillFolderId: string): Promise<void> => {
  const response = await fetch(`${getApiBase(workspaceId)}/${skillFolderId}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Backend error: ${response.statusText}`);
  }
};
