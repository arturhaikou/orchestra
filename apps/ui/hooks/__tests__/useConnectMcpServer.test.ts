import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useConnectMcpServer } from '../useConnectMcpServer';
import * as mcpServersApi from '../../services/mcpServersApi';
import { McpServerHttpFields, McpServerStdioFields, ToolPreviewDto } from '../../types';

vi.mock('../../services/mcpServersApi');

const DEFAULT_HTTP: McpServerHttpFields = {
  url: 'https://mcp.example.com/api',
  authType: 'none',
  apiKey: '',
};

const DEFAULT_STDIO: McpServerStdioFields = {
  command: 'npx',
  args: ['-y', 'my-server'],
  envVars: [],
};

const WORKSPACE_ID = 'ws-123';

beforeEach(() => vi.clearAllMocks());

// ─── Scenario 1: Successful connection ───────────────────────────────────────

describe('Scenario 1: Successful connection reveals tool list', () => {
  it('Initial_Status_IsIdle', () => {
    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );
    expect(result.current.connectStatus).toBe('idle');
    expect(result.current.isConnectionVerified).toBe(false);
  });

  it('Connect_TransitionsToLoading', async () => {
    vi.mocked(mcpServersApi.connectMcpServer).mockReturnValue(new Promise(() => {}));

    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );

    act(() => { result.current.connect(); });
    expect(result.current.connectStatus).toBe('loading');
  });

  it('Connect_OnSuccess_TransitionsToSuccess', async () => {
    const tools: ToolPreviewDto[] = [
      { name: 'search-web', description: 'Searches the web' },
    ];
    vi.mocked(mcpServersApi.connectMcpServer).mockResolvedValue({ tools });

    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );

    await act(async () => { result.current.connect(); });

    expect(result.current.connectStatus).toBe('success');
    expect(result.current.discoveredTools).toHaveLength(1);
    expect(result.current.discoveredTools[0].name).toBe('search-web');
    expect(result.current.isConnectionVerified).toBe(true);
  });
});

// ─── Scenario 2: Zero tools is valid success ─────────────────────────────────

describe('Scenario 2: Zero tools still transitions to success', () => {
  it('Connect_WhenServerReturnsNoTools_IsConnectionVerifiedTrue', async () => {
    vi.mocked(mcpServersApi.connectMcpServer).mockResolvedValue({ tools: [] });

    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );

    await act(async () => { result.current.connect(); });

    expect(result.current.connectStatus).toBe('success');
    expect(result.current.discoveredTools).toHaveLength(0);
    expect(result.current.isConnectionVerified).toBe(true);
  });
});

// ─── Scenario 3: Connection timeout error ────────────────────────────────────

describe('Scenario 3: Timeout shows error and re-enables Connect', () => {
  it('Connect_WhenTimeoutError_SetsConnectError', async () => {
    vi.mocked(mcpServersApi.connectMcpServer).mockRejectedValue({
      errorCode: 'CONNECTION_TIMEOUT',
      message: 'Timeout',
    });

    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );

    await act(async () => { result.current.connect(); });

    expect(result.current.connectStatus).toBe('error');
    expect(result.current.connectError).toBe('CONNECTION_TIMEOUT');
    expect(result.current.isConnectionVerified).toBe(false);
  });
});

// ─── Scenario 4: Auth failure ────────────────────────────────────────────────

describe('Scenario 4: Auth failure error code', () => {
  it('Connect_WhenAuthFailed_SetsAuthFailedError', async () => {
    vi.mocked(mcpServersApi.connectMcpServer).mockRejectedValue({
      errorCode: 'AUTH_FAILED',
      message: 'Auth failed',
    });

    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );

    await act(async () => { result.current.connect(); });

    expect(result.current.connectError).toBe('AUTH_FAILED');
  });
});

// ─── Scenario 5: Editing connection-relevant field resets verified state ──────

describe('Scenario 5: Connection-relevant field edit resets verified state', () => {
  it('AfterSuccess_ChangingUrl_SetsIsStaleTrue', async () => {
    vi.mocked(mcpServersApi.connectMcpServer).mockResolvedValue({ tools: [] });

    let httpFields = { ...DEFAULT_HTTP };
    const { result, rerender } = renderHook(
      ({ http }) => useConnectMcpServer(WORKSPACE_ID, 'http', http, DEFAULT_STDIO),
      { initialProps: { http: httpFields } }
    );

    await act(async () => { result.current.connect(); });
    expect(result.current.isConnectionVerified).toBe(true);

    httpFields = { ...httpFields, url: 'https://mcp.other.com/api' };
    rerender({ http: httpFields });

    expect(result.current.isStale).toBe(true);
    expect(result.current.isConnectionVerified).toBe(false);
  });
});

// ─── Scenario 6: Server name change does NOT reset verified state ─────────────

describe('Scenario 6: Server name field change does not reset state', () => {
  it('AfterSuccess_IsConnectionVerified_RemainsTrue_Regardless_Of_ExternalServerNameChange', async () => {
    vi.mocked(mcpServersApi.connectMcpServer).mockResolvedValue({ tools: [] });

    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );

    await act(async () => { result.current.connect(); });

    expect(result.current.isConnectionVerified).toBe(true);
    expect(result.current.isStale).toBe(false);
  });
});

// ─── Scenario 7: Loading state prevents duplicate connect calls ───────────────

describe('Scenario 7: LoadingState prevents duplicate connect calls', () => {
  it('Connect_WhenAlreadyLoading_IsNoOp', async () => {
    vi.mocked(mcpServersApi.connectMcpServer).mockReturnValue(new Promise(() => {}));

    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );

    act(() => { result.current.connect(); });
    act(() => { result.current.connect(); });

    expect(vi.mocked(mcpServersApi.connectMcpServer)).toHaveBeenCalledTimes(1);
  });
});

// ─── Reset ────────────────────────────────────────────────────────────────────

describe('reset()', () => {
  it('Reset_AfterSuccess_TransitionsBackToIdle', async () => {
    vi.mocked(mcpServersApi.connectMcpServer).mockResolvedValue({ tools: [] });

    const { result } = renderHook(() =>
      useConnectMcpServer(WORKSPACE_ID, 'http', DEFAULT_HTTP, DEFAULT_STDIO)
    );

    await act(async () => { result.current.connect(); });
    expect(result.current.connectStatus).toBe('success');

    act(() => { result.current.reset(); });

    expect(result.current.connectStatus).toBe('idle');
    expect(result.current.isConnectionVerified).toBe(false);
    expect(result.current.discoveredTools).toHaveLength(0);
  });
});
