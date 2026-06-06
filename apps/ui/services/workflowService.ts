
import { WorkflowDefinition, WorkflowExecution } from '../types';
import { getToken } from './authService';

const DEFINITIONS_URL = `${import.meta.env.VITE_API_URL}/v1/workflow-definitions`;
const EXECUTIONS_URL = `${import.meta.env.VITE_API_URL}/v1/workflow-executions`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

const handleResponse = async <T>(response: Response): Promise<T> => {
  const contentType = response.headers.get('content-type');
  if (contentType?.includes('text/html')) throw new Error('Unexpected HTML response');
  if (!response.ok) throw new Error(`Request failed: ${response.statusText}`);
  return response.json();
};

export const getWorkflowDefinitions = async (workspaceId: string): Promise<WorkflowDefinition[]> => {
  const response = await fetch(`${DEFINITIONS_URL}?workspaceId=${workspaceId}`, {
    headers: getAuthHeaders()
  });
  return handleResponse<WorkflowDefinition[]>(response);
};

export const getWorkflowDefinition = async (id: string): Promise<WorkflowDefinition> => {
  const response = await fetch(`${DEFINITIONS_URL}/${id}`, { headers: getAuthHeaders() });
  return handleResponse<WorkflowDefinition>(response);
};

export interface CreateWorkflowStepPayload {
  clientId?: string;
  order: number;
  agentId?: string | null;
  instructionOverride?: string | null;
  passPreviousOutput: boolean;
  systemTools?: string[] | null;
  type?: 'Agent' | 'Condition';
  condition?: string | null;
  trueNextClientId?: string | null;
  falseNextClientId?: string | null;
}

export interface CreateWorkflowPayload {
  workspaceId: string;
  name: string;
  description?: string | null;
  steps: CreateWorkflowStepPayload[];
}

export const createWorkflowDefinition = async (payload: CreateWorkflowPayload): Promise<WorkflowDefinition> => {
  const response = await fetch(DEFINITIONS_URL, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(payload)
  });
  return handleResponse<WorkflowDefinition>(response);
};

export interface UpdateWorkflowPayload {
  name: string;
  description?: string | null;
  steps: CreateWorkflowStepPayload[];
}

export const updateWorkflowDefinition = async (id: string, payload: UpdateWorkflowPayload): Promise<WorkflowDefinition> => {
  const response = await fetch(`${DEFINITIONS_URL}/${id}`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(payload)
  });
  return handleResponse<WorkflowDefinition>(response);
};

export const getWorkflowSystemTools = async (): Promise<string[]> => {
  const response = await fetch(`${DEFINITIONS_URL}/system-tools`, {
    headers: getAuthHeaders()
  });
  return handleResponse<string[]>(response);
};

export const deleteWorkflowDefinition = async (id: string): Promise<void> => {
  const response = await fetch(`${DEFINITIONS_URL}/${id}`, {
    method: 'DELETE',
    headers: getAuthHeaders()
  });
  if (!response.ok) throw new Error(`Delete failed: ${response.statusText}`);
};

export const getWorkflowExecutionsByTicket = async (ticketId: string): Promise<WorkflowExecution[]> => {
  const response = await fetch(`${EXECUTIONS_URL}?ticketId=${ticketId}`, {
    headers: getAuthHeaders()
  });
  return handleResponse<WorkflowExecution[]>(response);
};

export const getWorkflowExecution = async (executionId: string): Promise<WorkflowExecution> => {
  const response = await fetch(`${EXECUTIONS_URL}/${executionId}`, {
    headers: getAuthHeaders()
  });
  return handleResponse<WorkflowExecution>(response);
};
