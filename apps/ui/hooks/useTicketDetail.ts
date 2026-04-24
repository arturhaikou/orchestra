import { useState, useEffect } from 'react';
import { getTicketById } from '../services/ticketService';
import type { Ticket } from '../types';

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

  useEffect(() => {
    if (!workspaceId || !ticketId) return;

    const loadTicket = async () => {
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
    };

    loadTicket();
  }, [workspaceId, ticketId]);

  return { ticket, isLoading, loadError };
};
