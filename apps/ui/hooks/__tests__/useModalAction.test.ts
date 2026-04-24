import { renderHook, act } from '@testing-library/react';
import { useModalAction } from '../useModalAction';

describe('useModalAction', () => {
  const mockOnSuccess = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('initial state', () => {
    it('should start with isLoading false and no error', () => {
      const apiCall = vi.fn().mockResolvedValue(undefined);
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      expect(result.current.isLoading).toBe(false);
      expect(result.current.error).toBeNull();
    });
  });

  describe('successful execution', () => {
    it('should call apiCall and onSuccess on success', async () => {
      const apiCall = vi.fn().mockResolvedValue(undefined);
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      let success: boolean;
      await act(async () => {
        success = await result.current.execute();
      });

      expect(apiCall).toHaveBeenCalledOnce();
      expect(mockOnSuccess).toHaveBeenCalledOnce();
      expect(success!).toBe(true);
    });

    it('should set isLoading to true during execution', async () => {
      let resolveApi: () => void;
      const apiCall = vi.fn().mockImplementation(() => new Promise<void>((resolve) => {
        resolveApi = resolve;
      }));
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      let executePromise: Promise<boolean>;
      act(() => {
        executePromise = result.current.execute();
      });

      expect(result.current.isLoading).toBe(true);

      await act(async () => {
        resolveApi!();
        await executePromise;
      });

      expect(result.current.isLoading).toBe(false);
    });

    it('should clear any previous error on successful execution', async () => {
      const apiCall = vi.fn()
        .mockRejectedValueOnce(new Error('First failure'))
        .mockResolvedValueOnce(undefined);
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      await act(async () => {
        await result.current.execute();
      });
      expect(result.current.error).not.toBeNull();

      await act(async () => {
        await result.current.execute();
      });
      expect(result.current.error).toBeNull();
      expect(mockOnSuccess).toHaveBeenCalledOnce();
    });
  });

  describe('error handling', () => {
    it('should set error message on API failure', async () => {
      const apiCall = vi.fn().mockRejectedValue(new Error('Agent not found'));
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      await act(async () => {
        await result.current.execute();
      });

      expect(result.current.error).toBe('Agent not found');
      expect(mockOnSuccess).not.toHaveBeenCalled();
    });

    it('should return false on failure', async () => {
      const apiCall = vi.fn().mockRejectedValue(new Error('Forbidden'));
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      let success: boolean;
      await act(async () => {
        success = await result.current.execute();
      });

      expect(success!).toBe(false);
    });

    it('should use fallback message for non-Error exceptions', async () => {
      const apiCall = vi.fn().mockRejectedValue('unknown');
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      await act(async () => {
        await result.current.execute();
      });

      expect(result.current.error).toBe('Server error: Unable to process request. Please try again.');
    });

    it('should set isLoading back to false after failure', async () => {
      const apiCall = vi.fn().mockRejectedValue(new Error('fail'));
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      await act(async () => {
        await result.current.execute();
      });

      expect(result.current.isLoading).toBe(false);
    });
  });

  describe('resetError', () => {
    it('should clear the error state', async () => {
      const apiCall = vi.fn().mockRejectedValue(new Error('some error'));
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      await act(async () => {
        await result.current.execute();
      });
      expect(result.current.error).toBe('some error');

      act(() => {
        result.current.resetError();
      });
      expect(result.current.error).toBeNull();
    });
  });

  describe('retry behavior', () => {
    it('should allow re-execution after failure (retry)', async () => {
      const apiCall = vi.fn()
        .mockRejectedValueOnce(new Error('Temporary error'))
        .mockResolvedValueOnce(undefined);
      const { result } = renderHook(() => useModalAction(apiCall, mockOnSuccess));

      await act(async () => {
        await result.current.execute();
      });
      expect(result.current.error).toBe('Temporary error');

      await act(async () => {
        const success = await result.current.execute();
        expect(success).toBe(true);
      });
      expect(result.current.error).toBeNull();
      expect(mockOnSuccess).toHaveBeenCalledOnce();
    });
  });
});
