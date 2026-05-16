import { Skill } from '../types';
import { getToken } from './authService';

const getApiBase = (workspaceId: string) =>
  `${import.meta.env.VITE_API_URL}/v1/workspaces/${workspaceId}/skills`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`,
});

export const getSkills = async (workspaceId: string): Promise<Skill[]> => {
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
    console.error('Failed to fetch skills:', error);
    return [];
  }
};

export const getSkill = async (workspaceId: string, skillId: string): Promise<Skill> => {
  const response = await fetch(`${getApiBase(workspaceId)}/${skillId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    if (response.status === 404) throw new Error('Skill not found');
    throw new Error(`Backend error: ${response.statusText}`);
  }

  return await response.json();
};

export const createSkill = async (
  workspaceId: string,
  data: { name: string; description: string; instructions: string }
): Promise<Skill> => {
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

export const updateSkill = async (
  workspaceId: string,
  skillId: string,
  data: { name: string; description: string; instructions: string }
): Promise<Skill> => {
  const response = await fetch(`${getApiBase(workspaceId)}/${skillId}`, {
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

export const deleteSkill = async (workspaceId: string, skillId: string): Promise<void> => {
  const response = await fetch(`${getApiBase(workspaceId)}/${skillId}`, {
    method: 'DELETE',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Backend error: ${response.statusText}`);
  }
};
