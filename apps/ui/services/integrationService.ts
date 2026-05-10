
import { Integration, IntegrationType, McpDiscoveryError, DiscoveredTool, SyncToolsResult, McpServer } from '../types';
import { getToken } from './authService';

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/integrations`;

export interface IntegrationDTO {
  name: string;
  types: IntegrationType[];
  provider: string;
  url: string;
  username?: string;
  apiKey: string;
  filterQuery?: string;
  vectorize?: boolean;
  connected?: boolean;  // Optional connection status
}

export interface CreateIntegrationDTO extends IntegrationDTO {
  workspaceId: string;
}

export interface DiscoverMcpToolsRequest {
  workspaceId: string;
  mcpEndpointUrl: string;
  mcpAuthType: 'ApiKey' | 'None';
  apiKey?: string;
}

export interface DiscoverMcpToolsResponse {
  tools: DiscoveredTool[];
  totalCount: number;
}

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getIntegrations = async (workspaceId: string): Promise<Integration[]> => {
  try {
    const response = await fetch(`${API_BASE_URL}?workspaceId=${workspaceId}`, {
        headers: getAuthHeaders()
    });
    if (!response.ok) throw new Error("API Error");
    return await response.json();
  } catch (error) {
    console.error('Failed to fetch integrations:', error);
    return [];
  }
};

export const createIntegration = async (data: CreateIntegrationDTO): Promise<Integration> => {
  try {
    const response = await fetch(API_BASE_URL, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      // Try to extract detailed error message from response body
      const errorData = await response.json().catch(() => ({}));
      const errorMessage = (errorData as any)?.error || (errorData as any)?.message || response.statusText;
      throw new Error(errorMessage);
    }

    return await response.json();
  } catch (error) {
    console.error('Failed to create integration:', error);
    throw error;
  }
};

export const updateIntegration = async (id: string, data: IntegrationDTO): Promise<Integration> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      // Try to extract detailed error message from response body
      const errorData = await response.json().catch(() => ({}));
      const errorMessage = (errorData as any)?.error || (errorData as any)?.message || response.statusText;
      throw new Error(errorMessage);
    }

    return await response.json();
  } catch (error) {
    console.error('Failed to update integration:', error);
    throw error;
  }
};

export const deleteIntegration = async (id: string): Promise<void> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      throw new Error(`Backend error: ${response.statusText}`);
    }
  } catch (error) {
    console.error('Failed to delete integration:', error);
    throw error;
  }
};

export interface ConnectionTestRequest {
  provider: string;
  url: string;
  username?: string;
  apiKey: string;
}

export const testIntegrationConnection = async (data: ConnectionTestRequest): Promise<void> => {
  try {
    const response = await fetch(`${API_BASE_URL}/validate-connection`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      const errorMessage = (errorData as any)?.message || `Connection failed: ${response.statusText}`;
      throw new Error(errorMessage);
    }
  } catch (error) {
    console.error('Failed to test integration connection:', error);
    throw error;
  }
};

export const discoverMcpTools = async (
  request: DiscoverMcpToolsRequest
): Promise<DiscoverMcpToolsResponse> => {
  const response = await fetch(`${API_BASE_URL}/mcp/discover`, {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify(request),
  });

  if (response.status === 422) {
    const errorData = await response.json().catch(() => ({}));
    const error: McpDiscoveryError = {
      errorType: (errorData as any)?.errorType ?? 'ConnectionFailed',
      message: (errorData as any)?.error ?? 'MCP connection failed',
    };
    throw error;
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error((errorData as any)?.error ?? response.statusText);
  }

  return response.json();
};

export interface DeletionImpactDto {
  affectedAgentCount: number;
  toolActionCount: number;
  affectedAgentNames: string[];
}

export const getDeletionImpact = async (
  integrationId: string
): Promise<DeletionImpactDto> => {
  const response = await fetch(`${API_BASE_URL}/${integrationId}/deletion-impact`, {
    headers: getAuthHeaders(),
  });
  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error((errorData as any)?.error ?? response.statusText);
  }
  return response.json();
};

export const syncIntegrationTools = async (
  integrationId: string
): Promise<SyncToolsResult> => {
  const response = await fetch(`${API_BASE_URL}/${integrationId}/sync-tools`, {
    method: 'POST',
    headers: getAuthHeaders(),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new Error((errorData as any)?.error ?? response.statusText);
  }

  return response.json();
};

export interface CreateHttpMcpPayload {
  workspaceId: string;
  name: string;
  endpointUrl: string;
  authType: 'API_KEY' | 'NONE';
  apiKey?: string;
}

export interface CreateStdioMcpPayload {
  workspaceId: string;
  name: string;
  command: string;
  arguments?: string[];
  environmentVariables?: Record<string, string>;
}

export interface TransportCreatedTool {
  toolId: string;
  toolName: string;
  dangerLevel: string;
}

export interface TransportCreateResult {
  id: string;
  name: string;
  transportType: 'HTTP' | 'STDIO';
  toolCount: number;
  tools: TransportCreatedTool[];
}

export const createHttpMcpIntegration = async (
  payload: CreateHttpMcpPayload
): Promise<TransportCreateResult> => {
  const response = await fetch(
    `${API_BASE_URL}/mcp/http?workspaceId=${encodeURIComponent(payload.workspaceId)}`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        name: payload.name,
        transportType: 'HTTP',
        endpointUrl: payload.endpointUrl,
        authType: payload.authType,
        apiKey: payload.authType === 'API_KEY' ? payload.apiKey : null,
        mcpCommand: null,
        mcpArguments: null,
        mcpEnvironmentVariables: null,
      }),
    }
  );

  if (response.status === 422) {
    const body = await response.json().catch(() => ({}));
    const err: any = new Error((body as any)?.message ?? 'MCP connection failed');
    err.type = (body as any)?.type ?? 'MCP_CONNECTION_ERROR';
    throw err;
  }

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error((body as any)?.message ?? response.statusText);
  }

  const data = await response.json();
  return {
    id: data.id,
    name: data.name,
    transportType: 'HTTP',
    toolCount: data.toolCount ?? 0,
    tools: (data.tools ?? []).map((t: any) => ({
      toolId: t.name ?? t.toolId ?? '',
      toolName: t.name ?? t.toolName ?? '',
      dangerLevel: t.dangerLevel ?? 'Safe',
    })),
  };
};

export const createStdioMcpIntegration = async (
  payload: CreateStdioMcpPayload
): Promise<TransportCreateResult> => {
  const response = await fetch(
    `${API_BASE_URL}/mcp/stdio?workspaceId=${encodeURIComponent(payload.workspaceId)}`,
    {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({
        name: payload.name,
        command: payload.command,
        arguments: payload.arguments ?? null,
        environmentVariables: payload.environmentVariables ?? null,
        endpointUrl: null,
        authType: null,
      }),
    }
  );

  if (response.status === 422) {
    const body = await response.json().catch(() => ({}));
    const err: any = new Error((body as any)?.message ?? 'stdio process failed');
    err.type = (body as any)?.type ?? 'PROCESS_LAUNCH_FAILURE';
    throw err;
  }

  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error((body as any)?.message ?? response.statusText);
  }

  const data = await response.json();
  return {
    id: data.id,
    name: data.name,
    transportType: 'STDIO',
    toolCount: data.toolCount ?? 0,
    tools: (data.tools ?? []).map((t: any) => ({
      toolId: t.toolId ?? t.name ?? '',
      toolName: t.toolName ?? t.name ?? '',
      dangerLevel: t.dangerLevel ?? 'Safe',
    })),
  };
};

export const getMcpServers = async (workspaceId: string): Promise<McpServer[]> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/integrations/mcp-servers?workspaceId=${workspaceId}`,
    { headers: getAuthHeaders() }
  );
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json();
};

export const deleteMcpServer = async (serverId: string): Promise<void> => {
  const response = await fetch(
    `${import.meta.env.VITE_API_URL}/v1/integrations/${serverId}`,
    { method: 'DELETE', headers: getAuthHeaders() }
  );
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
};
