import { useState, useEffect } from 'react';
import { getAgentMcpAssignments } from '../services/agentService';

export interface UseAgentMcpAssignmentsResult {
  assignments: Record<string, string[]>;
  isLoading: boolean;
  hasError: boolean;
}

export function useAgentMcpAssignments(
  agentId: string | undefined,
  isOpen: boolean
): UseAgentMcpAssignmentsResult {
  const [assignments, setAssignments] = useState<Record<string, string[]>>({});
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    if (!isOpen || !agentId) return;

    const controller = new AbortController();
    setIsLoading(true);
    setHasError(false);

    getAgentMcpAssignments(agentId)
      .then((data) => {
        if (controller.signal.aborted) return;
        const map = Object.fromEntries(
          data.mcpAssignments.map((entry) => [entry.mcpServerId, entry.toolNames])
        );
        setAssignments(map);
      })
      .catch(() => {
        if (controller.signal.aborted) return;
        setHasError(true);
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsLoading(false);
      });

    return () => controller.abort();
  }, [agentId, isOpen]);

  return { assignments, isLoading, hasError };
}
