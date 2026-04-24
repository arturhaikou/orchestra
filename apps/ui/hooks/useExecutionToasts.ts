import { useState, useEffect, useRef, useCallback } from 'react';
import { AgentExecutionCompletedEvent, ExecutionToastData } from '../types';
import { onAgentExecutionCompleted, offAgentExecutionCompleted } from '../services/signalRService';
import { getAgents } from '../services/agentService';

const AUTO_DISMISS_MS = 10_000;
const FADE_OUT_MS = 300;

export const isValidReviewUrl = (url: string | null | undefined): boolean => {
  if (!url) return false;
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
};

let toastCounter = 0;

export const useExecutionToasts = (
  workspaceId: string,
): {
  toasts: ExecutionToastData[];
  dismiss: (id: string) => void;
} => {
  const [toasts, setToasts] = useState<ExecutionToastData[]>([]);
  const timersRef = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map());

  const dismiss = useCallback((id: string) => {
    const timer = timersRef.current.get(id);
    if (timer) {
      clearTimeout(timer);
      timersRef.current.delete(id);
    }
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  const scheduleAutoDismiss = useCallback(
    (id: string) => {
      const timer = setTimeout(() => {
        timersRef.current.delete(id);
        setToasts((prev) => prev.filter((t) => t.id !== id));
      }, AUTO_DISMISS_MS + FADE_OUT_MS);
      timersRef.current.set(id, timer);
    },
    [],
  );

  useEffect(() => {
    const handleEvent = (event: AgentExecutionCompletedEvent) => {
      if (event.workspaceId !== workspaceId) return;

      const id = `toast-${++toastCounter}`;
      const toast: ExecutionToastData = {
        id,
        agentId: event.agentId,
        agentName: event.agentName || 'Unknown Agent',
        ticketId: event.ticketId,
        ticketTitle: event.ticketTitle || 'Unknown Ticket',
        status: event.status,
        reviewUrl: isValidReviewUrl(event.reviewUrl) ? event.reviewUrl : null,
        createdAt: Date.now(),
      };

      setToasts((prev) => [...prev, toast]);
      scheduleAutoDismiss(id);

      getAgents(workspaceId).catch((err) =>
        console.error('Failed to refresh agents after execution event:', err),
      );
    };

    onAgentExecutionCompleted(handleEvent);

    return () => {
      offAgentExecutionCompleted();
      timersRef.current.forEach((timer) => clearTimeout(timer));
      timersRef.current.clear();
    };
  }, [workspaceId, scheduleAutoDismiss]);

  return { toasts, dismiss };
};
