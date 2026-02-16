
import { Job } from '../types';
import { getToken } from './authService';

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/jobs`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getJobs = async (workspaceId: string): Promise<Job[]> => {
  try {
    const response = await fetch(`${API_BASE_URL}?workspaceId=${workspaceId}`, {
      headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error("API Error");
    return await response.json();
  } catch (error) {
    console.error('Failed to fetch jobs:', error);
    return [];
  }
};

export const triggerSync = async (workspaceId: string, integrationId: string): Promise<Job> => {
  try {
    const response = await fetch(`${API_BASE_URL}/sync`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ workspaceId, integrationId }),
    });
    if (!response.ok) throw new Error("API Error");
    return await response.json();
  } catch (error) {
    console.error('Failed to trigger sync:', error);
    throw error;
  }
};

export const cancelJob = async (jobId: string): Promise<void> => {
    try {
        await fetch(`${API_BASE_URL}/${jobId}/cancel`, {
            method: 'POST',
            headers: getAuthHeaders(),
        });
    } catch (e) {
        return Promise.resolve();
    }
};
