import { useEffect, useState, useCallback } from 'react';
import { GlobalAgentQuestion } from '../types';
import { getGlobalPendingQuestions, onQuestionAnswered, onQuestionsRefreshNeeded } from '../services/agentQuestionService';
import { onGlobalAgentQuestionAsked, onGlobalAgentQuestionResolved, onWorkflowExecutionStatusChanged, onJobStatusChanged, GlobalAgentQuestionEvent } from '../services/signalRService';

export function useGlobalAgentQuestions() {
  const [questions, setQuestions] = useState<GlobalAgentQuestion[]>([]);

  const fetchInitial = useCallback(async () => {
    try {
      const data = await getGlobalPendingQuestions();
      setQuestions(data);
    } catch {
      setQuestions([]);
    }
  }, []);

  useEffect(() => {
    fetchInitial();
  }, [fetchInitial]);

  useEffect(() => {
    const unsub = onGlobalAgentQuestionAsked((event: GlobalAgentQuestionEvent) => {
      const newQuestion: GlobalAgentQuestion = {
        workspaceId: event.workspaceId,
        workspaceName: event.workspaceName,
        questionId: event.questionId,
        ticketId: event.ticketId,
        ticketTitle: event.ticketTitle,
        agentName: event.agentName,
        questionsJson: event.questionsJson,
        createdAt: event.createdAt,
      };
      setQuestions(prev => {
        if (prev.some(q => q.questionId === event.questionId)) return prev;
        return [newQuestion, ...prev];
      });
    });
    return unsub;
  }, []);

  const removeQuestion = useCallback((questionId: string) => {
    setQuestions(prev => prev.filter(q => q.questionId !== questionId));
  }, []);

  useEffect(() => {
    const unsub = onGlobalAgentQuestionResolved((event) => {
      removeQuestion(event.questionId);
    });
    return unsub;
  }, [removeQuestion]);

  useEffect(() => {
    return onQuestionAnswered(removeQuestion);
  }, [removeQuestion]);

  useEffect(() => {
    return onQuestionsRefreshNeeded(() => fetchInitial());
  }, [fetchInitial]);

  useEffect(() => {
    return onWorkflowExecutionStatusChanged((event) => {
      if (event.status === 'Cancelled') fetchInitial();
    });
  }, [fetchInitial]);

  useEffect(() => {
    return onJobStatusChanged((data) => {
      if (data.newStatus === 'Cancelled') fetchInitial();
    });
  }, [fetchInitial]);

  return { questions, totalCount: questions.length, removeQuestion };
}
