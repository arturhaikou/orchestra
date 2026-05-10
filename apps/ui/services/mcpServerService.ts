import { getToken } from './authService';
import { McpServer, DeleteMcpServerResponse, McpServerToolsResponse } from '../types';

const MCP_SERVERS_BASE = `${import.meta.env.VITE_API_URL}/v1/mcp-servers`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`,
});

export interface CheckNameUniqueResponse {
  isUnique: boolean;
}

export const checkMcpServerNameUnique = async (
  workspaceId: string,
  name: string,
  excludeId?: string
): Promise<CheckNameUniqueResponse> => {
  const params = new URLSearchParams({ workspaceId, name });
  if (excludeId) params.set('excludeId', excludeId);

  const response = await fetch(
    `${MCP_SERVERS_BASE}/check-name?${params.toString()}`,
    { headers: getAuthHeaders() }
  );

  if (!response.ok) {
    throw new Error(`Name uniqueness check failed: ${response.status}`);
  }

  return response.json() as Promise<CheckNameUniqueResponse>;
};

export class McpServerNotFoundError extends Error {
  constructor(serverId: string) {
    super(`MCP server '${serverId}' was not found (already deleted?).`);
    this.name = 'McpServerNotFoundError';
  }
}

export class McpServerForbiddenError extends Error {
  constructor() {
    super('You do not have permission to delete this MCP server.');
    this.name = 'McpServerForbiddenError';
  }
}

export class McpServerDeleteError extends Error {
  constructor(status: number) {
    super(`Failed to delete MCP server (HTTP ${status}).`);
    this.name = 'McpServerDeleteError';
  }
}

export const getMcpServers = async (workspaceId: string): Promise<McpServer[]> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/mcp-servers?workspaceId=${workspaceId}`,
    { headers: getAuthHeaders() },
  );
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json() as Promise<McpServer[]>;
};

export const deleteMcpServer = async (serverId: string): Promise<DeleteMcpServerResponse> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/mcp-servers/${serverId}`,
    { method: 'DELETE', headers: getAuthHeaders() },
  );

  if (response.ok) {
    return response.json() as Promise<DeleteMcpServerResponse>;
  }

  if (response.status === 404) throw new McpServerNotFoundError(serverId);
  if (response.status === 403) throw new McpServerForbiddenError();
  throw new McpServerDeleteError(response.status);
};

export class McpServerImpactFetchError extends Error {
  constructor(status: number) {
    super(`Failed to fetch delete impact (HTTP ${status}).`);
    this.name = 'McpServerImpactFetchError';
  }
}

export const fetchDeleteImpact = async (serverId: string): Promise<number> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/mcp-servers/${serverId}/impact`,
    { headers: getAuthHeaders() },
  );

  if (response.ok) {
    const body = (await response.json()) as { affectedAgentCount: number };
    return body.affectedAgentCount;
  }

  throw new McpServerImpactFetchError(response.status);
};

export const fetchMcpServerTools = async (
  serverId: string,
  workspaceId: string,
  signal?: AbortSignal
): Promise<McpServerToolsResponse> => {
  const response = await fetch(
    `${MCP_SERVERS_BASE}/${serverId}/tools?workspaceId=${workspaceId}`,
    { headers: getAuthHeaders(), signal }
  );

  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json() as Promise<McpServerToolsResponse>;
};
