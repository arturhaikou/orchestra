import { useEffect, useState, useCallback } from 'react';
import { AgentQuestion } from '../types';
import { getPendingQuestions } from '../services/agentQuestionService';
import { onAgentQuestionAsked, onAgentQuestionResolved } from '../services/signalRService';

export function useAgentQuestions(workspaceId: string | undefined) {
  const [questions, setQuestions] = useState<AgentQuestion[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  const refetch = useCallback(async () => {
    if (!workspaceId) return;
    setIsLoading(true);
    try {
      const data = await getPendingQuestions(workspaceId);
      setQuestions(data);
    } catch {
      setQuestions([]);
    } finally {
      setIsLoading(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    refetch();
  }, [refetch]);

  useEffect(() => {
    const unsubAsked = onAgentQuestionAsked(() => refetch());
    const unsubResolved = onAgentQuestionResolved(() => refetch());
    return () => {
      unsubAsked();
      unsubResolved();
    };
  }, [refetch]);

  return { questions, pendingCount: questions.length, isLoading, refetch };
}
