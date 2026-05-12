
import { Agent, AgentTemplateDto, CreateAgentFromTemplateRequest, McpToolSelection } from '../types';
import { getToken } from './authService';

export interface SaveAgentToolsPayload {
  nativeToolActionIds: string[];
  mcpSelections: McpToolSelection[];
}

export interface AgentMcpServerAssignment {
  mcpServerId: string;
  toolNames: string[];
}

export interface AgentToolAssignmentsResponse {
  nativeToolActionIds: string[];
  mcpAssignments: AgentMcpServerAssignment[];
}

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/agents`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getAgents = async (workspaceId: string): Promise<Agent[]> => {
  try {
    const response = await fetch(`${API_BASE_URL}?workspaceId=${workspaceId}`, {
      headers: getAuthHeaders()
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }
    return await response.json();
  } catch (error) {
    console.error('Failed to fetch agents:', error);
    return [];
  }
};

export const getAgent = async (agentId: string): Promise<Agent> => {
  const response = await fetch(`${API_BASE_URL}/${agentId}`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('Agent not found');
    }
    throw new Error(`Backend error: ${response.statusText}`);
  }

  return await response.json();
};

export const getAgentTemplates = async (workspaceId: string): Promise<AgentTemplateDto[]> => {
  try {
    const response = await fetch(`${API_BASE_URL}/templates?workspaceId=${encodeURIComponent(workspaceId)}`, {
      headers: getAuthHeaders()
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }
    return await response.json();
  } catch (error) {
    console.error('Failed to fetch agent templates:', error);
    throw error;
  }
};

export const createAgentFromTemplate = async (
  request: CreateAgentFromTemplateRequest
): Promise<Agent> => {
  const response = await fetch(`${API_BASE_URL}/from-template`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const errorBody = await response.json().catch(() => ({ message: 'Failed to deploy agent. Please try again.' }));
    throw new Error(errorBody.detail || errorBody.message || `Backend error: ${response.statusText}`);
  }

  return await response.json();
};

export const createAgent = async (workspaceId: string, data: { name: string; role: string; capabilities: string[]; toolActionIds: string[]; mcpSelections?: McpToolSelection[]; customInstructions?: string; projectPrinciples?: string; model?: string | null; subAgentIds?: string[] }): Promise<Agent> => {
  try {
    const response = await fetch(API_BASE_URL, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ workspaceId, ...data }),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Failed to create agent:', error);
    throw error;
  }
};

export const updateAgent = async (agentId: string, data: Partial<Agent>): Promise<Agent> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${agentId}`, {
      method: 'PUT',
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
    console.error('Failed to update agent:', error);
    throw error;
  }
};

export const deleteAgent = async (agentId: string): Promise<void> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${agentId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }
  } catch (error) {
    console.error('Failed to delete agent:', error);
    throw error;
  }
};

export const saveAgentToolAssignments = async (
  agentId: string,
  payload: SaveAgentToolsPayload
): Promise<void> => {
  const response = await fetch(`${API_BASE_URL}/${agentId}/tools`, {
    method: 'PUT',
    headers: getAuthHeaders(),
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    throw new Error(`Backend error: ${response.statusText}`);
  }
};

export const getAgentMcpAssignments = async (
  agentId: string
): Promise<AgentToolAssignmentsResponse> => {
  const response = await fetch(`${API_BASE_URL}/${agentId}/mcp-tools`, {
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    throw new Error(`Backend error: ${response.statusText}`);
  }

  return response.json();
};
