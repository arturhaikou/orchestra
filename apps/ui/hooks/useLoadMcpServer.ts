import { useState, useEffect, useCallback } from 'react';
import { getMcpServerById } from '../services/mcpServersApi';
import type {
  GetMcpServerByIdResponseDto,
  LoadMcpServerErrorCode,
  LoadMcpServerStatus,
} from '../types';

export interface UseLoadMcpServerReturn {
  loadStatus: LoadMcpServerStatus;
  serverData: GetMcpServerByIdResponseDto | null;
  loadError: LoadMcpServerErrorCode | null;
  retry: () => void;
}

export function useLoadMcpServer(serverId: string, workspaceId: string): UseLoadMcpServerReturn {
  const [loadStatus, setLoadStatus] = useState<LoadMcpServerStatus>('loading');
  const [serverData, setServerData] = useState<GetMcpServerByIdResponseDto | null>(null);
  const [loadError, setLoadError] = useState<LoadMcpServerErrorCode | null>(null);

  const fetchServer = useCallback(async () => {
    setLoadStatus('loading');
    setLoadError(null);
    try {
      const data = await getMcpServerById(serverId, workspaceId);
      setServerData(data);
      setLoadStatus('loaded');
    } catch (err: unknown) {
      const code = (err as { errorCode?: string })?.errorCode;
      setLoadError((code as LoadMcpServerErrorCode) ?? 'UNKNOWN');
      setLoadStatus('error');
    }
  }, [serverId, workspaceId]);

  useEffect(() => {
    fetchServer();
  }, [fetchServer]);

  return { loadStatus, serverData, loadError, retry: fetchServer };
}
