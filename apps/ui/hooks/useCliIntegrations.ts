import { useState, useEffect, useCallback } from 'react';
import { AiCliIntegration } from '../types';
import { getCliIntegrations, deleteCliIntegration } from '../services/cliIntegrationService';

interface UseCliIntegrationsResult {
  integrations: AiCliIntegration[];
  isLoading: boolean;
  hasError: boolean;
  retry: () => void;
  deleteIntegration: (id: string) => Promise<void>;
}

export const useCliIntegrations = (workspaceId: string | undefined): UseCliIntegrationsResult => {
  const [integrations, setIntegrations] = useState<AiCliIntegration[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);

  const fetchIntegrations = useCallback(async () => {
    if (!workspaceId) return;
    setIsLoading(true);
    setHasError(false);
    try {
      const data = await getCliIntegrations(workspaceId);
      setIntegrations(data);
    } catch {
      setHasError(true);
    } finally {
      setIsLoading(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    fetchIntegrations();
  }, [fetchIntegrations]);

  const deleteIntegration = useCallback(
    async (id: string) => {
      if (!workspaceId) return;
      await deleteCliIntegration(workspaceId, id);
      setIntegrations(prev => prev.filter(i => i.id !== id));
    },
    [workspaceId],
  );

  return {
    integrations,
    isLoading,
    hasError,
    retry: fetchIntegrations,
    deleteIntegration,
  };
};
