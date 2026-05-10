import { getToken } from './authService';
import {
  ConnectMcpServerResponse,
  ConnectMcpServerErrorResponse,
  GetMcpServerByIdResponseDto,
  SaveMcpServerResponseDto,
  SaveMcpServerErrorDto,
} from '../types';

const BASE = `${import.meta.env.VITE_API_URL}/v1/mcp-servers`;

interface ConnectHttpPayload {
  url: string;
  authType: 'NONE' | 'API_KEY' | 'BEARER_TOKEN';
  apiKey?: string;
}

interface ConnectStdioPayload {
  command: string;
  args: string[];
  envVars: { key: string; value: string }[];
}

export interface ConnectMcpServerRequest {
  workspaceId: string;
  transportType: 'HTTP' | 'STDIO';
  http?: ConnectHttpPayload;
  stdio?: ConnectStdioPayload;
}

/**
 * Calls POST /v1/integrations/mcp-servers/connect.
 *
 * Resolves with `ConnectMcpServerResponse` on HTTP 200.
 * Rejects with `ConnectMcpServerErrorResponse` on HTTP 422.
 * Rejects with `ConnectMcpServerErrorResponse{ errorCode: 'UNKNOWN' }` on other non-2xx.
 * Rejects with `ConnectMcpServerErrorResponse{ errorCode: 'UNREACHABLE' }` on network TypeError.
 * Re-throws `AbortError` unchanged — caller is responsible for detecting it.
 */
export const connectMcpServer = async (
  request: ConnectMcpServerRequest,
  signal?: AbortSignal
): Promise<ConnectMcpServerResponse> => {
  try {
    const response = await fetch(`${BASE}/connect`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${getToken() ?? ''}`,
      },
      body: JSON.stringify(request),
      signal,
    });

    if (response.ok) {
      return (await response.json()) as ConnectMcpServerResponse;
    }

    return handleErrorResponse(response);
  } catch (err) {
    return handleFetchError(err);
  }
};

async function handleErrorResponse(response: Response): Promise<never> {
  if (response.status === 422) {
    const body = (await response.json()) as ConnectMcpServerErrorResponse;
    throw body;
  }

  throw {
    errorCode: 'UNKNOWN',
    message: `Unexpected HTTP ${response.status}`,
  } as ConnectMcpServerErrorResponse;
}

function handleFetchError(err: unknown): never {
  if (err instanceof Error && err.name === 'AbortError') {
    throw err;
  }

  if (err instanceof TypeError) {
    throw {
      errorCode: 'UNREACHABLE',
      message: 'Network error — could not reach the server.',
    } as ConnectMcpServerErrorResponse;
  }

  throw err;
}

// ─── Save request internal shapes (FR-007) ───────────────────────────────────

interface SaveHttpPayload {
  url: string;
  authType: 'NONE' | 'API_KEY' | 'BEARER_TOKEN';
  apiKey?: string | null;
}

interface SaveEnvVar {
  key: string;
  value: string | null;
}

interface SaveStdioPayload {
  command: string;
  args: string[];
  envVars: SaveEnvVar[];
}

export interface SaveMcpServerRequest {
  workspaceId: string;
  name: string;
  transportType: 'HTTP' | 'STDIO';
  http?: SaveHttpPayload;
  stdio?: SaveStdioPayload;
}

export const saveMcpServer = async (
  request: SaveMcpServerRequest
): Promise<SaveMcpServerResponseDto> => {
  let response: Response;

  try {
    response = await fetch(`${BASE}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${getToken() ?? ''}`,
      },
      body: JSON.stringify(request),
    });
  } catch {
    throw { errorCode: 'NETWORK', message: 'Network error' } satisfies SaveMcpServerErrorDto;
  }

  if (response.status === 201) {
    return (await response.json()) as SaveMcpServerResponseDto;
  }

  return rejectWithErrorDto(response);
};

async function rejectWithErrorDto(response: Response): Promise<never> {
  const errorBody = await response.json().catch(() => null);

  if (response.status === 409) {
    throw { errorCode: 'DUPLICATE_NAME', message: errorBody?.message ?? '' } satisfies SaveMcpServerErrorDto;
  }
  if (response.status === 400) {
    throw { errorCode: 'VALIDATION_ERROR', message: errorBody?.message ?? 'Validation failed.' } satisfies SaveMcpServerErrorDto;
  }

  throw {
    errorCode: 'UNKNOWN',
    message: `Unexpected HTTP ${response.status}`,
  } satisfies SaveMcpServerErrorDto;
}

export const getMcpServerById = async (
  serverId: string,
  workspaceId: string
): Promise<GetMcpServerByIdResponseDto> => {
  let response: Response;
  try {
    response = await fetch(`${BASE}/${serverId}?workspaceId=${workspaceId}`, {
      method: 'GET',
      headers: { Authorization: `Bearer ${getToken() ?? ''}` },
    });
  } catch {
    throw { errorCode: 'NETWORK', message: 'Network error' };
  }

  if (response.status === 200) {
    return (await response.json()) as GetMcpServerByIdResponseDto;
  }
  if (response.status === 404) throw { errorCode: 'NOT_FOUND', message: 'Server not found.' };
  if (response.status === 403) throw { errorCode: 'FORBIDDEN', message: 'Access denied.' };
  throw { errorCode: 'UNKNOWN', message: `Unexpected HTTP ${response.status}` };
};

export const patchMcpServer = async (
  serverId: string,
  request: SaveMcpServerRequest
): Promise<SaveMcpServerResponseDto> => {
  let response: Response;
  try {
    response = await fetch(`${BASE}/${serverId}`, {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${getToken() ?? ''}`,
      },
      body: JSON.stringify(request),
    });
  } catch {
    throw { errorCode: 'NETWORK', message: 'Network error' };
  }

  if (response.status === 200) {
    return (await response.json()) as SaveMcpServerResponseDto;
  }

  const errorBody = await response.json().catch(() => null);
  if (response.status === 404) throw { errorCode: 'NOT_FOUND', message: 'Server not found.' };
  if (response.status === 403) throw { errorCode: 'FORBIDDEN', message: 'Access denied.' };
  if (response.status === 409) throw { errorCode: 'DUPLICATE_NAME', message: errorBody?.message ?? '' };
  if (response.status === 400)
    throw { errorCode: 'VALIDATION_ERROR', message: errorBody?.message ?? 'Validation failed.' };
  throw { errorCode: 'UNKNOWN', message: `Unexpected HTTP ${response.status}` };
};
