import { useState, useCallback } from 'react';

interface UseModalActionResult {
  execute: () => Promise<boolean>;
  isLoading: boolean;
  error: string | null;
  resetError: () => void;
}

export function useModalAction(
  apiCall: () => Promise<void>,
  onSuccess: () => void
): UseModalActionResult {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const execute = useCallback(async (): Promise<boolean> => {
    setIsLoading(true);
    setError(null);
    try {
      await apiCall();
      onSuccess();
      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Server error: Unable to process request. Please try again.';
      setError(message);
      return false;
    } finally {
      setIsLoading(false);
    }
  }, [apiCall, onSuccess]);

  const resetError = useCallback(() => {
    setError(null);
  }, []);

  return { execute, isLoading, error, resetError };
}
