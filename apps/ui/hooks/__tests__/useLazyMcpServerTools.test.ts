import { renderHook, act, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { useLazyMcpServerTools } from '../useLazyMcpServerTools';
import * as mcpServerService from '../../services/mcpServerService';
import { McpServerToolsResponse } from '../../types';

vi.mock('../../services/mcpServerService', () => ({
  fetchMcpServerTools: vi.fn(),
}));

const mockFetch = vi.mocked(mcpServerService.fetchMcpServerTools);

const WORKSPACE_ID = 'workspace-123';
const SERVER_A_ID  = 'server-aaa';
const SERVER_B_ID  = 'server-bbb';

const makeSuccessResponse = (tools: { name: string; description: string; dangerLevel: string }[]): McpServerToolsResponse => ({
  isSuccess: true,
  tools: tools.map(t => ({ ...t, dangerLevel: t.dangerLevel as 'Safe' | 'Moderate' | 'Destructive' })),
  errorType: null,
  errorMessage: null,
});

const makeErrorResponse = (errorType: 'Unreachable' | 'AuthFailed', message = 'Server error'): McpServerToolsResponse => ({
  isSuccess: false,
  tools: null,
  errorType,
  errorMessage: message,
});

const makeEmptyResponse = (): McpServerToolsResponse => ({
  isSuccess: false,
  tools: null,
  errorType: 'Empty',
  errorMessage: null,
});

describe('useLazyMcpServerTools', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ── Initial state ──────────────────────────────────────────────────────────

  it('starts in idle state', () => {
    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    expect(result.current.fetchState.status).toBe('idle');
  });

  // ── Scenario 1: Tools loaded successfully ─────────────────────────────────

  it('transitions to loading then success when tools are returned', async () => {
    mockFetch.mockResolvedValueOnce(
      makeSuccessResponse([
        { name: 'query_data', description: 'Queries data', dangerLevel: 'Safe' },
      ])
    );

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    act(() => result.current.fetchForServer(SERVER_A_ID));
    expect(result.current.fetchState.status).toBe('loading');

    await waitFor(() =>
      expect(result.current.fetchState.status).toBe('success')
    );

    const state = result.current.fetchState;
    expect(state.status).toBe('success');
    if (state.status === 'success') {
      expect(state.tools).toHaveLength(1);
      expect(state.tools[0].name).toBe('query_data');
    }
  });

  it('passes workspaceId and serverId to fetchMcpServerTools', async () => {
    mockFetch.mockResolvedValueOnce(makeSuccessResponse([]));

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));
    await act(async () => result.current.fetchForServer(SERVER_A_ID));

    expect(mockFetch).toHaveBeenCalledWith(
      SERVER_A_ID,
      WORKSPACE_ID,
      expect.any(AbortSignal)
    );
  });

  // ── Scenario 2: Empty tool list ───────────────────────────────────────────

  it('transitions to empty state when server returns 0 tools', async () => {
    mockFetch.mockResolvedValueOnce(makeEmptyResponse());

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));
    await act(async () => result.current.fetchForServer(SERVER_A_ID));

    await waitFor(() =>
      expect(result.current.fetchState.status).toBe('empty')
    );
  });

  // ── Scenario 3: Server unreachable / error ────────────────────────────────

  it('transitions to error state when server is unreachable', async () => {
    mockFetch.mockResolvedValueOnce(
      makeErrorResponse('Unreachable', 'Unable to reach this server')
    );

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));
    await act(async () => result.current.fetchForServer(SERVER_A_ID));

    await waitFor(() => expect(result.current.fetchState.status).toBe('error'));

    const state = result.current.fetchState;
    if (state.status === 'error') {
      expect(state.message).toBeTruthy();
    }
  });

  it('retry re-fetches the last selected server', async () => {
    mockFetch
      .mockResolvedValueOnce(makeErrorResponse('Unreachable', 'Offline'))
      .mockResolvedValueOnce(makeSuccessResponse([
        { name: 'read_data', description: 'Reads data', dangerLevel: 'Safe' },
      ]));

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));
    await act(async () => result.current.fetchForServer(SERVER_A_ID));

    await waitFor(() => expect(result.current.fetchState.status).toBe('error'));

    await act(async () => result.current.retry());

    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it('retry is a no-op if no server has been selected', () => {
    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    act(() => result.current.retry());

    expect(mockFetch).not.toHaveBeenCalled();
    expect(result.current.fetchState.status).toBe('idle');
  });

  // ── Scenario 4: Authentication failure ───────────────────────────────────

  it('transitions to auth_failed state when server returns AuthFailed', async () => {
    mockFetch.mockResolvedValueOnce(makeErrorResponse('AuthFailed'));

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));
    await act(async () => result.current.fetchForServer(SERVER_A_ID));

    await waitFor(() =>
      expect(result.current.fetchState.status).toBe('auth_failed')
    );
  });

  // ── Scenario 5: Rapid server switching cancels previous request ───────────

  it('cancels previous in-flight fetch when a new server is clicked', async () => {
    let resolveA!: (v: McpServerToolsResponse) => void;
    const promiseA = new Promise<McpServerToolsResponse>(resolve => { resolveA = resolve; });

    mockFetch
      .mockReturnValueOnce(promiseA)   // Server A — will be cancelled
      .mockResolvedValueOnce(          // Server B — resolves normally
        makeSuccessResponse([{ name: 'tool_b', description: 'B tool', dangerLevel: 'Safe' }])
      );

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    act(() => result.current.fetchForServer(SERVER_A_ID));
    expect(result.current.fetchState.status).toBe('loading');

    // Switch to server B before A resolves
    act(() => result.current.fetchForServer(SERVER_B_ID));

    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));

    // The AbortSignal for A should have been aborted
    const [, , signalA] = mockFetch.mock.calls[0];
    expect(signalA?.aborted).toBe(true);

    // Only server B's tool appears
    const state = result.current.fetchState;
    if (state.status === 'success') {
      expect(state.tools[0].name).toBe('tool_b');
    }

    // Clean up hanging promise
    resolveA(makeSuccessResponse([]));
  });

  it('shows loading state for server B immediately after switching', async () => {
    let resolveA!: (v: McpServerToolsResponse) => void;
    const promiseA = new Promise<McpServerToolsResponse>(resolve => { resolveA = resolve; });
    const promiseB = new Promise<McpServerToolsResponse>(() => {}); // never resolves

    mockFetch
      .mockReturnValueOnce(promiseA)
      .mockReturnValueOnce(promiseB);

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    act(() => result.current.fetchForServer(SERVER_A_ID));
    act(() => result.current.fetchForServer(SERVER_B_ID));

    expect(result.current.fetchState.status).toBe('loading');
    resolveA(makeSuccessResponse([])); // A resolves but should be ignored
  });

  // ── Scenario 6: Destructive tool visible but unchecked ───────────────────

  it('includes destructive tools in success state tools list', async () => {
    mockFetch.mockResolvedValueOnce(
      makeSuccessResponse([
        { name: 'delete_all_records', description: 'Deletes everything', dangerLevel: 'Destructive' },
        { name: 'read_records', description: 'Reads data', dangerLevel: 'Safe' },
      ])
    );

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));
    await act(async () => result.current.fetchForServer(SERVER_A_ID));

    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));

    const state = result.current.fetchState;
    if (state.status === 'success') {
      const destructive = state.tools.find(t => t.dangerLevel === 'Destructive');
      expect(destructive).toBeDefined();
      expect(destructive?.name).toBe('delete_all_records');
    }
  });

  // ── Network / fetch exception ─────────────────────────────────────────────

  it('transitions to error state when fetchMcpServerTools throws', async () => {
    mockFetch.mockRejectedValueOnce(new TypeError('Network error'));

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));
    await act(async () => result.current.fetchForServer(SERVER_A_ID));

    await waitFor(() => expect(result.current.fetchState.status).toBe('error'));
  });

  it('does not update state when AbortError is thrown (cancelled request)', async () => {
    const abortError = new DOMException('Aborted', 'AbortError');
    mockFetch.mockRejectedValueOnce(abortError);

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));
    act(() => result.current.fetchForServer(SERVER_A_ID));

    // Should stay in loading or idle — not transition to error
    await waitFor(() =>
      expect(result.current.fetchState.status).not.toBe('error')
    );
  });

  // ── getServerState tests ──────────────────────────────────────────────────

  it('getServerState_returns_idle_for_unknown_server', () => {
    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    const unknownServerState = result.current.getServerState('unknown-server-id');
    expect(unknownServerState.status).toBe('idle');
  });

  it('getServerState_returns_success_state_after_fetch_completes', async () => {
    mockFetch.mockResolvedValueOnce(
      makeSuccessResponse([
        { name: 'tool-x', description: 'desc', dangerLevel: 'Safe' }
      ])
    );

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    await act(async () => result.current.fetchForServer(SERVER_A_ID));
    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));

    const serverState = result.current.getServerState(SERVER_A_ID);
    expect(serverState.status).toBe('success');
  });

  it('getServerState_returns_error_state_after_fetch_fails', async () => {
    mockFetch.mockResolvedValueOnce(makeErrorResponse('Unreachable', 'timeout'));

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    await act(async () => result.current.fetchForServer(SERVER_A_ID));
    await waitFor(() => expect(result.current.fetchState.status).toBe('error'));

    const serverState = result.current.getServerState(SERVER_A_ID);
    expect(serverState.status).toBe('error');
  });

  // ── Per-server caching — BDD Scenario 3 ───────────────────────────────────

  it('fetchForServer_does_not_call_api_if_server_is_already_in_success_cache', async () => {
    mockFetch.mockResolvedValueOnce(
      makeSuccessResponse([
        { name: 'tool-y', description: 'y', dangerLevel: 'Safe' }
      ])
    );

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    // First call — should trigger API
    await act(async () => result.current.fetchForServer(SERVER_A_ID));
    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));
    const callCountAfterFirst = mockFetch.mock.calls.length;

    // Second call for the same server — should NOT trigger API
    act(() => result.current.fetchForServer(SERVER_A_ID));
    expect(mockFetch.mock.calls.length).toBe(callCountAfterFirst);
  });

  it('fetchForServer_restores_cached_state_when_switching_back_to_visited_server', async () => {
    // Step 1: fetch server A successfully
    mockFetch.mockResolvedValueOnce(
      makeSuccessResponse([
        { name: 'cached-tool', description: 'from A', dangerLevel: 'Safe' }
      ])
    );

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    await act(async () => result.current.fetchForServer(SERVER_A_ID));
    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));

    // Step 2: fetch server B
    mockFetch.mockResolvedValueOnce(
      makeSuccessResponse([
        { name: 'server-b-tool', description: 'from B', dangerLevel: 'Moderate' }
      ])
    );

    await act(async () => result.current.fetchForServer(SERVER_B_ID));
    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));

    // Step 3: switch back to server A — should NOT call API again
    const callCountBeforeSwitch = mockFetch.mock.calls.length;
    act(() => result.current.fetchForServer(SERVER_A_ID));
    expect(mockFetch.mock.calls.length).toBe(callCountBeforeSwitch);

    // Step 4: fetchState should reflect server A's tools
    await waitFor(() => {
      const state = result.current.fetchState;
      if (state.status === 'success') {
        const hasCachedTool = (state.tools ?? []).some(
          (t: { name: string }) => t.name === 'cached-tool'
        );
        expect(hasCachedTool).toBe(true);
      } else {
        expect(state.status).toBe('success');
      }
    });
  });

  it('fetchForServer_always_fetches_new_server_not_in_cache', async () => {
    // Server A is in cache
    mockFetch.mockResolvedValueOnce(makeSuccessResponse([]));
    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    await act(async () => result.current.fetchForServer(SERVER_A_ID));
    await waitFor(() => expect(result.current.fetchState.status).toBe('empty'));

    // Server B has never been fetched — should trigger API
    mockFetch.mockResolvedValueOnce(
      makeSuccessResponse([
        { name: 'new-tool', description: 'new', dangerLevel: 'Safe' }
      ])
    );

    await act(async () => result.current.fetchForServer(SERVER_B_ID));
    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));

    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it('getServerState_reflects_current_fetchState_for_active_server', async () => {
    mockFetch.mockResolvedValueOnce(
      makeSuccessResponse([
        { name: 'z', description: 'z tool', dangerLevel: 'Safe' }
      ])
    );

    const { result } = renderHook(() => useLazyMcpServerTools(WORKSPACE_ID));

    await act(async () => result.current.fetchForServer(SERVER_A_ID));
    await waitFor(() => expect(result.current.fetchState.status).toBe('success'));

    // getServerState and fetchState should agree on the active server
    const serverStateForA = result.current.getServerState(SERVER_A_ID);
    expect(serverStateForA.status).toBe(result.current.fetchState.status);
  });
});
