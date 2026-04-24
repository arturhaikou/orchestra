import { useState, useEffect } from 'react';
import { ConnectionStatus } from '../types';
import { onConnectionStatusChange, getConnectionStatus } from '../services/signalRService';
import { getAgents } from '../services/agentService';
import { getTickets } from '../services/ticketService';

export const useConnectionStatus = (
  workspaceId?: string,
): { status: ConnectionStatus } => {
  const [status, setStatus] = useState<ConnectionStatus>(getConnectionStatus());

  useEffect(() => {
    let previousStatus: ConnectionStatus = getConnectionStatus();

    const handleStatusChange = (newStatus: ConnectionStatus) => {
      setStatus(newStatus);

      if (newStatus === 'connected' && previousStatus === 'reconnecting' && workspaceId) {
        getAgents(workspaceId).catch((err) =>
          console.warn('Failed to refresh agents on reconnect:', err),
        );
        getTickets(workspaceId).catch((err) =>
          console.warn('Failed to refresh tickets on reconnect:', err),
        );
      }

      previousStatus = newStatus;
    };

    onConnectionStatusChange(handleStatusChange);

    return () => {
      onConnectionStatusChange(() => {});
    };
  }, [workspaceId]);

  return { status };
};
