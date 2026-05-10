import { useState, useEffect } from 'react';
import { getMcpServers } from '../services/mcpServerService';
import { McpServer } from '../types';

export interface UseToolPickerMcpServersResult {
  servers: McpServer[];
  isLoading: boolean;
  hasError: boolean;
}

export function useToolPickerMcpServers(
  workspaceId: string,
  isOpen: boolean
): UseToolPickerMcpServersResult {
  const [servers, setServers] = useState<McpServer[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    if (!isOpen || !workspaceId) return;

    let cancelled = false;
    setIsLoading(true);
    setHasError(false);

    getMcpServers(workspaceId)
      .then(data => { if (!cancelled) setServers(data); })
      .catch(() => { if (!cancelled) setHasError(true); })
      .finally(() => { if (!cancelled) setIsLoading(false); });

    return () => { cancelled = true; };
  }, [workspaceId, isOpen]);

  return { servers, isLoading, hasError };
}
