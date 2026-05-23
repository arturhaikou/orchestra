import { useState, useEffect } from 'react';
import { ConnectionStatus, getConnectionStatus, onConnectionStatusChange } from '../services/signalRService';

export const useSignalRReady = (): boolean => {
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    const currentStatus = getConnectionStatus();
    setIsReady(currentStatus === 'connected');

    const handleStatusChange = (status: ConnectionStatus) => {
      setIsReady(status === 'connected');
    };

    const unsubscribe = onConnectionStatusChange(handleStatusChange);

    return () => {
      unsubscribe();
    };
  }, []);

  return isReady;
};
