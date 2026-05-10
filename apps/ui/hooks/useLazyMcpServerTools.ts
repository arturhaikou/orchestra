import { useState, useCallback, useRef } from 'react';
import { McpToolFetchState } from '../types';
import { fetchMcpServerTools } from '../services/mcpServerService';

export interface UseLazyMcpServerToolsResult {
  fetchState: McpToolFetchState;
  fetchForServer: (serverId: string) => void;
  retry: () => void;
  getServerState: (serverId: string) => McpToolFetchState;
}

export function useLazyMcpServerTools(workspaceId: string): UseLazyMcpServerToolsResult {
  const [fetchState, setFetchState] = useState<McpToolFetchState>({ status: 'idle' });
  const abortRef       = useRef<AbortController | null>(null);
  const lastServerIdRef = useRef<string | null>(null);
  const serverCacheRef = useRef<Map<string, McpToolFetchState>>(new Map());

  const getServerState = useCallback((serverId: string): McpToolFetchState => {
    return serverCacheRef.current.get(serverId) ?? { status: 'idle' };
  }, []);

  const fetchForServer = useCallback((serverId: string): void => {
    const cached = serverCacheRef.current.get(serverId);
    if (cached) {
      setFetchState(cached);
      lastServerIdRef.current = serverId;
      return;
    }

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current      = controller;
    lastServerIdRef.current = serverId;

    setFetchState({ status: 'loading' });

    fetchMcpServerTools(serverId, workspaceId, controller.signal)
      .then(response => {
        if (controller.signal.aborted) return;

        let newState: McpToolFetchState;
        if (response.errorType === 'AuthFailed') {
          newState = { status: 'auth_failed' };
        } else if (response.isSuccess && response.tools && response.tools.length > 0) {
          newState = { status: 'success', tools: response.tools };
        } else if (response.errorType === 'Empty' || (response.isSuccess && (!response.tools || response.tools.length === 0))) {
          newState = { status: 'empty' };
        } else {
          newState = { status: 'error', message: response.errorMessage ?? 'Unable to reach this server' };
        }

        serverCacheRef.current.set(serverId, newState);
        setFetchState(newState);
      })
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        if (err instanceof DOMException && err.name === 'AbortError') return;
        const message = err instanceof Error ? err.message : 'Unknown error';
        const errorState: McpToolFetchState = { status: 'error', message };
        serverCacheRef.current.set(serverId, errorState);
        setFetchState(errorState);
      });
  }, [workspaceId]);

  const retry = useCallback((): void => {
    if (!lastServerIdRef.current) return;
    serverCacheRef.current.delete(lastServerIdRef.current);
    fetchForServer(lastServerIdRef.current);
  }, [fetchForServer]);

  return { fetchState, fetchForServer, retry, getServerState };
}
