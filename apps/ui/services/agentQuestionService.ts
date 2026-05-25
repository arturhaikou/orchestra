import { AgentQuestion } from '../types';
import { getToken } from './authService';

const API_BASE = `${import.meta.env.VITE_API_URL}/v1`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getPendingQuestions = async (workspaceId: string): Promise<AgentQuestion[]> => {
  const res = await fetch(
    `${API_BASE}/workspaces/${workspaceId}/agent-questions`,
    { headers: getAuthHeaders() }
  );
  if (!res.ok) throw new Error('Failed to load agent questions');
  return res.json();
};

export const submitAnswers = async (
  questionId: string,
  answers: Record<string, string | string[]>
): Promise<void> => {
  const res = await fetch(`${API_BASE}/agent-questions/${questionId}/answer`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify({ answers }),
  });
  if (!res.ok) throw new Error('Failed to submit answers');
};
