import { useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { patchMcpServer } from '../services/mcpServersApi';
import type { SaveMcpServerRequest } from '../services/mcpServersApi';
import type {
  ApiKeyEditState,
  EnvVarEditStateMap,
  McpServerHttpFields,
  McpServerStdioFields,
  McpServerTransportType,
  PatchMcpServerError,
  PatchStatus,
} from '../types';

export interface UsePatchMcpServerOptions {
  serverId: string;
  workspaceId: string;
  serverName: string;
  transportType: McpServerTransportType;
  httpFields: McpServerHttpFields;
  stdioFields: McpServerStdioFields;
  isConnectionVerified: boolean;
  apiKeyEditState: ApiKeyEditState;
  envVarEditStateMap: EnvVarEditStateMap;
}

export interface UsePatchMcpServerReturn {
  patchStatus: PatchStatus;
  patchError: PatchMcpServerError | null;
  isNameConflict: boolean;
  patch: () => Promise<void>;
  clearError: () => void;
}

export function usePatchMcpServer(opts: UsePatchMcpServerOptions): UsePatchMcpServerReturn {
  const navigate = useNavigate();
  const [patchStatus, setPatchStatus] = useState<PatchStatus>('idle');
  const [patchError, setPatchError] = useState<PatchMcpServerError | null>(null);

  const patch = useCallback(async () => {
    setPatchStatus('patching');
    setPatchError(null);
    try {
      const request = buildPatchRequest(opts);
      const result = await patchMcpServer(opts.serverId, request);
      setPatchStatus('success');
      navigate(`/workspaces/${opts.workspaceId}/mcp-servers`, {
        state: { toast: { intent: 'updated', serverName: result.name } },
      });
    } catch (err: unknown) {
      const code = (err as { errorCode?: string })?.errorCode ?? 'UNKNOWN';
      const message = (err as { message?: string })?.message ?? '';
      setPatchError({ code: code as PatchMcpServerError['code'], message });
      setPatchStatus('error');
    }
  }, [opts, navigate]);

  const clearError = useCallback(() => {
    setPatchError(null);
    setPatchStatus('idle');
  }, []);

  return {
    patchStatus,
    patchError,
    isNameConflict: patchError?.code === 'DUPLICATE_NAME',
    patch,
    clearError,
  };
}

function buildPatchRequest(opts: UsePatchMcpServerOptions): SaveMcpServerRequest {
  if (opts.transportType === 'http') {
    return buildHttpPatchRequest(opts);
  }
  return buildStdioPatchRequest(opts);
}

function buildHttpPatchRequest(opts: UsePatchMcpServerOptions): SaveMcpServerRequest {
  return {
    workspaceId: opts.workspaceId,
    name: opts.serverName,
    transportType: 'HTTP',
    http: {
      url: opts.httpFields.url,
      authType: opts.httpFields.authType.toUpperCase() as 'NONE' | 'API_KEY' | 'BEARER_TOKEN',
      apiKey: opts.apiKeyEditState === 'masked' ? null : opts.httpFields.apiKey,
    },
  };
}

function buildStdioPatchRequest(opts: UsePatchMcpServerOptions): SaveMcpServerRequest {
  return {
    workspaceId: opts.workspaceId,
    name: opts.serverName,
    transportType: 'STDIO',
    stdio: {
      command: opts.stdioFields.command,
      args: opts.stdioFields.args.filter(a => a.trim() !== ''),
      envVars: opts.stdioFields.envVars.map((ev, idx) => ({
        key: ev.key,
        value: (opts.envVarEditStateMap[idx] ?? 'touched') === 'masked' ? null : ev.value,
      })),
    },
  };
}
