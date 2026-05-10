import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useLoadMcpServer } from '../useLoadMcpServer';
import type { GetMcpServerByIdResponseDto } from '../../types';

// ── Mock the API module ──────────────────────────────────────────────────────
vi.mock('../../services/mcpServersApi', () => ({
  getMcpServerById: vi.fn(),
}));

import { getMcpServerById } from '../../services/mcpServersApi';
const mockGetMcpServerById = vi.mocked(getMcpServerById);

const mockServerData: GetMcpServerByIdResponseDto = {
  id: 'server-id-1',
  workspaceId: 'workspace-id-1',
  name: 'Analytics MCP',
  transportType: 'HTTP',
  connectionStatus: 'Connected',
  endpointUrl: 'https://analytics.example.com/mcp',
  authType: 'API_KEY',
  hasApiKey: true,
  command: null,
  args: null,
  envVarKeys: null,
};

describe('useLoadMcpServer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('initiallyHasLoadingStatus', () => {
    mockGetMcpServerById.mockReturnValue(new Promise(() => {})); // never resolves
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));
    expect(result.current.loadStatus).toBe('loading');
  });

  it('transitionsToLoadedOnSuccess', async () => {
    mockGetMcpServerById.mockResolvedValue(mockServerData);
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('loaded'));
  });

  it('populatesServerDataOnSuccess', async () => {
    mockGetMcpServerById.mockResolvedValue(mockServerData);
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.serverData).not.toBeNull());

    expect(mockGetMcpServerById).toHaveBeenCalledWith('server-id-1', 'workspace-id-1');
    expect(result.current.serverData?.name).toBe('Analytics MCP');
    expect(result.current.serverData?.hasApiKey).toBe(true);
  });

  it('setsNotFoundErrorOn404', async () => {
    mockGetMcpServerById.mockRejectedValue({ errorCode: 'NOT_FOUND', message: 'Not found' });
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('error'));

    expect(result.current.loadError).toBe('NOT_FOUND');
    expect(result.current.serverData).toBeNull();
  });

  it('setsForbiddenErrorOn403', async () => {
    mockGetMcpServerById.mockRejectedValue({ errorCode: 'FORBIDDEN', message: 'Forbidden' });
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('error'));

    expect(result.current.loadError).toBe('FORBIDDEN');
  });

  it('setsNetworkErrorOnNetworkFailure', async () => {
    mockGetMcpServerById.mockRejectedValue({ errorCode: 'NETWORK', message: 'Network error' });
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('error'));

    expect(result.current.loadError).toBe('NETWORK');
  });

  it('setsUnknownErrorOnUnexpectedCode', async () => {
    mockGetMcpServerById.mockRejectedValue({ errorCode: undefined, message: 'Crash' });
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('error'));

    expect(result.current.loadError).toBe('UNKNOWN');
  });

  it('loadErrorIsNullWhenLoaded', async () => {
    mockGetMcpServerById.mockResolvedValue(mockServerData);
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('loaded'));

    expect(result.current.loadError).toBeNull();
  });

  it('serverDataIsNullWhenError', async () => {
    mockGetMcpServerById.mockRejectedValue({ errorCode: 'NOT_FOUND' });
    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('error'));

    expect(result.current.serverData).toBeNull();
  });

  it('retryTransitionsBackToLoading', async () => {
    // First call fails, second call never resolves (stays loading)
    mockGetMcpServerById
      .mockRejectedValueOnce({ errorCode: 'NETWORK' })
      .mockReturnValueOnce(new Promise(() => {}));

    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('error'));

    act(() => { result.current.retry(); });

    expect(result.current.loadStatus).toBe('loading');
  });

  it('retryReFetchesServer', async () => {
    mockGetMcpServerById
      .mockRejectedValueOnce({ errorCode: 'NETWORK' })
      .mockResolvedValueOnce(mockServerData);

    const { result } = renderHook(() => useLoadMcpServer('server-id-1', 'workspace-id-1'));

    await waitFor(() => expect(result.current.loadStatus).toBe('error'));

    act(() => { result.current.retry(); });

    await waitFor(() => expect(result.current.loadStatus).toBe('loaded'));

    expect(mockGetMcpServerById).toHaveBeenCalledTimes(2);
    expect(result.current.serverData?.name).toBe('Analytics MCP');
  });
});
