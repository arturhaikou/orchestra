import { useEffect, useState, useCallback } from 'react';
import { AgentQuestion } from '../types';
import { getPendingQuestions, onQuestionAnswered, onQuestionsRefreshNeeded } from '../services/agentQuestionService';
import { onAgentQuestionAsked, onAgentQuestionResolved, onWorkflowExecutionStatusChanged, onJobStatusChanged } from '../services/signalRService';

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
    const unsubCancelled = onWorkflowExecutionStatusChanged((event) => {
      if (event.status === 'Cancelled') refetch();
    });
    const unsubJobCancelled = onJobStatusChanged((data) => {
      if (data.newStatus === 'Cancelled') refetch();
    });
    return () => {
      unsubAsked();
      unsubResolved();
      unsubCancelled();
      unsubJobCancelled();
    };
  }, [refetch]);

  useEffect(() => {
    return onQuestionAnswered((questionId) => {
      setQuestions(prev => prev.filter(q => q.id !== questionId));
    });
  }, []);

  useEffect(() => {
    return onQuestionsRefreshNeeded(() => refetch());
  }, [refetch]);

  return { questions, pendingCount: questions.length, isLoading, refetch };
}
