
import { JobDetail, JobStatus, PagedJobsResult } from '../types';
import { getToken } from './authService';
import { triggerQuestionsRefresh } from './agentQuestionService';

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/jobs`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getJobs = async (
  workspaceId: string,
  status?: JobStatus,
  page = 1,
  pageSize = 20
): Promise<PagedJobsResult> => {
  const params = new URLSearchParams({ workspaceId, page: String(page), pageSize: String(pageSize) });
  if (status) params.set('status', status);
  const response = await fetch(`${API_BASE_URL}?${params}`, { headers: getAuthHeaders() });
  if (!response.ok) throw new Error('Failed to fetch jobs');
  return response.json();
};

export const getJobDetail = async (jobId: string): Promise<JobDetail> => {
  const response = await fetch(`${API_BASE_URL}/${jobId}`, { headers: getAuthHeaders() });
  if (!response.ok) throw new Error('Failed to fetch job detail');
  return response.json();
};

export const cancelJob = async (jobId: string): Promise<void> => {
  const response = await fetch(`${API_BASE_URL}/${jobId}/cancel`, {
    method: 'POST',
    headers: getAuthHeaders(),
  });
  if (!response.ok && response.status !== 409) throw new Error('Failed to cancel job');
  triggerQuestionsRefresh();
};
