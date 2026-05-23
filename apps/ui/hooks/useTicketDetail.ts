import { useState, useEffect, useCallback } from 'react';
import { getTicketById } from '../services/ticketService';
import type { Ticket, TicketStatusChangedEvent } from '../types';
import { onTicketStatusChanged } from '../services/signalRService';

interface UseTicketDetailReturn {
  ticket: Ticket | null;
  isLoading: boolean;
  loadError: string | null;
}

export const useTicketDetail = (
  workspaceId: string | undefined,
  ticketId: string | undefined
): UseTicketDetailReturn => {
  const [ticket, setTicket] = useState<Ticket | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const loadTicket = useCallback(async () => {
    if (!workspaceId || !ticketId) return;
    setIsLoading(true);
    setLoadError(null);
    try {
      const data = await getTicketById(ticketId);
      setTicket(data);
    } catch {
      setLoadError('Failed to load ticket');
    } finally {
      setIsLoading(false);
    }
  }, [workspaceId, ticketId]);

  const refreshTicket = useCallback(async () => {
    if (!workspaceId || !ticketId) return;
    try {
      const data = await getTicketById(ticketId);
      setTicket(data);
    } catch {
      // Silent background refresh — ignore errors
    }
  }, [workspaceId, ticketId]);

  useEffect(() => {
    loadTicket();
  }, [loadTicket]);

  useEffect(() => {
    if (!ticketId) return;
    const unsubscribe = onTicketStatusChanged((event: TicketStatusChangedEvent) => {
      if (event.ticketId !== ticketId) return;
      refreshTicket();
    });
    return unsubscribe;
  }, [ticketId, refreshTicket]);

  return { ticket, isLoading, loadError };
};
