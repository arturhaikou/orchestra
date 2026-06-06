import { AgentQuestion, GlobalAgentQuestion } from '../types';
import { getToken } from './authService';

const API_BASE = `${import.meta.env.VITE_API_URL}/v1`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

const questionAnsweredHandlers = new Set<(questionId: string) => void>();

export const onQuestionAnswered = (handler: (questionId: string) => void): (() => void) => {
  questionAnsweredHandlers.add(handler);
  return () => questionAnsweredHandlers.delete(handler);
};

export const getPendingQuestions = async (workspaceId: string): Promise<AgentQuestion[]> => {
  const res = await fetch(
    `${API_BASE}/workspaces/${workspaceId}/agent-questions`,
    { headers: getAuthHeaders() }
  );
  if (!res.ok) throw new Error('Failed to load agent questions');
  return res.json();
};

export const getGlobalPendingQuestions = async (): Promise<GlobalAgentQuestion[]> => {
  const res = await fetch(`${API_BASE}/agent-questions/global-pending`, {
    headers: getAuthHeaders(),
  });
  if (!res.ok) throw new Error('Failed to load global pending questions');
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
  questionAnsweredHandlers.forEach(h => h(questionId));
};
