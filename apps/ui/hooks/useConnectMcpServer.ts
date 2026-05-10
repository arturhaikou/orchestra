import { useCallback, useEffect, useRef, useState } from 'react';
import { connectMcpServer, ConnectMcpServerRequest } from '../services/mcpServersApi';
import {
  ConnectStatus,
  ConnectErrorCode,
  ConnectMcpServerErrorResponse,
  McpServerHttpFields,
  McpServerStdioFields,
  ToolPreviewDto,
} from '../types';

export interface ConnectionSnapshot {
  transportType: 'http' | 'stdio';
  url?: string;
  authType?: string;
  apiKey?: string;
  command?: string;
  args?: string[];
  envVars?: string;
}

export const buildConnectionSnapshot = (
  transportType: 'http' | 'stdio',
  http: McpServerHttpFields,
  stdio: McpServerStdioFields
): ConnectionSnapshot => {
  if (transportType === 'http') {
    return {
      transportType,
      url: http.url,
      authType: http.authType,
      apiKey: http.authType === 'api_key' ? http.apiKey : undefined,
    };
  }
  return {
    transportType,
    command: stdio.command,
    args: stdio.args,
    envVars: JSON.stringify(stdio.envVars),
  };
};

export const snapshotsEqual = (a: ConnectionSnapshot, b: ConnectionSnapshot): boolean =>
  JSON.stringify(a) === JSON.stringify(b);

export interface UseConnectMcpServerReturn {
  connectStatus: ConnectStatus;
  connectError: ConnectErrorCode | undefined;
  discoveredTools: ToolPreviewDto[];
  isConnectionVerified: boolean;
  isStale: boolean;
  connect: () => void;
  reset: () => void;
}

export const useConnectMcpServer = (
  workspaceId: string,
  transportType: 'http' | 'stdio',
  httpFields: McpServerHttpFields,
  stdioFields: McpServerStdioFields
): UseConnectMcpServerReturn => {
  const [connectStatus, setConnectStatus] = useState<ConnectStatus>('idle');
  const [connectError, setConnectError]   = useState<ConnectErrorCode | undefined>();
  const [discoveredTools, setDiscoveredTools] = useState<ToolPreviewDto[]>([]);
  const [isStale, setIsStale]             = useState(false);

  const abortRef            = useRef<AbortController | null>(null);
  const verifiedSnapshotRef = useRef<ConnectionSnapshot | null>(null);

  const isConnectionVerified = connectStatus === 'success' && !isStale;

  useEffect(() => {
    if (connectStatus !== 'success' || verifiedSnapshotRef.current === null) return;
    const current = buildConnectionSnapshot(transportType, httpFields, stdioFields);
    if (!snapshotsEqual(current, verifiedSnapshotRef.current)) {
      setConnectStatus('idle');
      setIsStale(true);
    }
  }, [
    connectStatus,
    transportType,
    httpFields.url,
    httpFields.authType,
    httpFields.apiKey,
    stdioFields.command,
    stdioFields.args,
    stdioFields.envVars,
  ]);

  const connect = useCallback(async () => {
    if (connectStatus === 'loading') return;

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    setConnectStatus('loading');
    setConnectError(undefined);
    setDiscoveredTools([]);
    setIsStale(false);

    const request = buildConnectRequest(workspaceId, transportType, httpFields, stdioFields);

    try {
      const response = await connectMcpServer(request, controller.signal);
      verifiedSnapshotRef.current = buildConnectionSnapshot(transportType, httpFields, stdioFields);
      setDiscoveredTools(response.tools);
      setConnectStatus('success');
    } catch (err) {
      if ((err as Error).name === 'AbortError') {
        setConnectStatus('idle');
        return;
      }
      const typed = err as ConnectMcpServerErrorResponse;
      setConnectError(typed.errorCode ?? 'UNKNOWN');
      setConnectStatus('error');
    }
  }, [connectStatus, workspaceId, transportType, httpFields, stdioFields]);

  const reset = useCallback(() => {
    abortRef.current?.abort();
    verifiedSnapshotRef.current = null;
    setConnectStatus('idle');
    setConnectError(undefined);
    setDiscoveredTools([]);
    setIsStale(false);
  }, []);

  useEffect(() => () => { abortRef.current?.abort(); }, []);

  return {
    connectStatus,
    connectError,
    discoveredTools,
    isConnectionVerified,
    isStale,
    connect,
    reset,
  };
};

function buildConnectRequest(
  workspaceId: string,
  transportType: 'http' | 'stdio',
  http: McpServerHttpFields,
  stdio: McpServerStdioFields
): ConnectMcpServerRequest {
  if (transportType === 'http') {
    return {
      workspaceId,
      transportType: 'HTTP',
      http: {
        url: http.url,
        authType: mapAuthType(http.authType),
        apiKey: http.authType !== 'none' ? http.apiKey : undefined,
      },
    };
  }

  return {
    workspaceId,
    transportType: 'STDIO',
    stdio: {
      command: stdio.command,
      args: stdio.args,
      envVars: stdio.envVars,
    },
  };
}

function mapAuthType(authType: McpServerHttpFields['authType']): 'NONE' | 'API_KEY' | 'BEARER_TOKEN' {
  switch (authType) {
    case 'api_key':      return 'API_KEY';
    case 'bearer_token': return 'BEARER_TOKEN';
    default:             return 'NONE';
  }
}
