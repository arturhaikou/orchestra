import { useState, useEffect, useCallback } from 'react';
import { McpServer } from '../types';
import { deleteMcpServer, getMcpServers, fetchDeleteImpact, McpServerForbiddenError, McpServerNotFoundError } from '../services/mcpServerService';

export interface DeleteServerOutcome {
  success: boolean;
  errorMessage: string | null;
  affectedAgentCount?: number;
}

interface UseMcpServersResult {
  servers: McpServer[];
  isLoading: boolean;
  hasError: boolean;
  retry: () => void;
  removeServer: (id: string) => void;
  deleteServer: (id: string) => Promise<DeleteServerOutcome>;
  fetchImpact: (serverId: string) => Promise<number>;
}

export const useMcpServers = (workspaceId: string | undefined): UseMcpServersResult => {
  const [servers, setServers] = useState<McpServer[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);

  const fetchServers = useCallback(async () => {
    if (!workspaceId) return;
    setIsLoading(true);
    setHasError(false);
    try {
      const data = await getMcpServers(workspaceId);
      setServers(data);
      setHasError(false);
    } catch {
      setHasError(true);
    } finally {
      setIsLoading(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    fetchServers();
  }, [fetchServers]);

  const removeServer = useCallback((id: string) => {
    setServers(prev => prev.filter(s => s.id !== id));
  }, []);

  const deleteServer = useCallback(
    async (id: string): Promise<DeleteServerOutcome> => {
      try {
        const response = await deleteMcpServer(id);
        removeServer(id);
        return { success: true, errorMessage: null, affectedAgentCount: response.affectedAgentCount };
      } catch (error) {
        if (error instanceof McpServerNotFoundError) {
          removeServer(id);
          return { success: true, errorMessage: null };
        }
        if (error instanceof McpServerForbiddenError) {
          return { success: false, errorMessage: 'You do not have permission to delete this MCP server.' };
        }
        const message = error instanceof Error ? error.message : 'Unknown error';
        return { success: false, errorMessage: message };
      }
    },
    [removeServer],
  );

  return { servers, isLoading, hasError, retry: fetchServers, removeServer, deleteServer, fetchImpact: fetchDeleteImpact };
};
