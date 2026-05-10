import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { usePatchMcpServer } from '../usePatchMcpServer';
import type { McpServerHttpFields, McpServerStdioFields } from '../../types';

vi.mock('../../services/mcpServersApi', () => ({
  patchMcpServer: vi.fn(),
}));
vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
}));

import { patchMcpServer } from '../../services/mcpServersApi';
const mockPatch = vi.mocked(patchMcpServer);

const defaultHttpFields: McpServerHttpFields = {
  url: 'https://mcp.example.com',
  authType: 'api_key',
  apiKey: 'my-api-key',
};
const defaultStdioFields: McpServerStdioFields = {
  command: 'npx',
  args: ['--mcp'],
  envVars: [],
};
const patchSuccessResponse = {
  id: 'srv-1',
  workspaceId: 'ws-1',
  name: 'Updated Server',
  connectionStatus: 'Connected' as const,
  transportType: 'HTTP' as const,
  endpointUrl: 'https://mcp.example.com',
  command: null,
  createdAt: new Date().toISOString(),
};

function buildOptions(overrides?: Partial<Parameters<typeof usePatchMcpServer>[0]>) {
  return {
    serverId: 'srv-1',
    workspaceId: 'ws-1',
    serverName: 'Updated Server',
    transportType: 'http' as const,
    httpFields: defaultHttpFields,
    stdioFields: defaultStdioFields,
    isConnectionVerified: true,
    apiKeyEditState: 'touched' as const,
    envVarEditStateMap: {},
    ...overrides,
  };
}

describe('usePatchMcpServer', () => {
  beforeEach(() => vi.clearAllMocks());

  it('initialStatusIsIdle', () => {
    const { result } = renderHook(() => usePatchMcpServer(buildOptions()));
    expect(result.current.patchStatus).toBe('idle');
    expect(result.current.patchError).toBeNull();
    expect(result.current.isNameConflict).toBe(false);
  });

  it('transitionsToPatchingThenSuccessOnHappyPath', async () => {
    mockPatch.mockResolvedValue(patchSuccessResponse);
    const { result } = renderHook(() => usePatchMcpServer(buildOptions()));

    await act(async () => { await result.current.patch(); });

    expect(result.current.patchStatus).toBe('success');
  });

  it('callsPatchMcpServerWithCorrectServerId', async () => {
    mockPatch.mockResolvedValue(patchSuccessResponse);
    const { result } = renderHook(() => usePatchMcpServer(buildOptions({ serverId: 'server-abc' })));

    await act(async () => { await result.current.patch(); });

    expect(mockPatch).toHaveBeenCalledWith('server-abc', expect.any(Object));
  });

  it('sendsMaskedApiKeyAsNull_WhenApiKeyEditStateIsMasked', async () => {
    mockPatch.mockResolvedValue(patchSuccessResponse);
    const { result } = renderHook(() => usePatchMcpServer(buildOptions({ apiKeyEditState: 'masked' })));

    await act(async () => { await result.current.patch(); });

    const [, body] = mockPatch.mock.calls[0];
    expect(body.http?.apiKey).toBeNull();
  });

  it('sendsApiKeyValue_WhenApiKeyEditStateIsTouched', async () => {
    mockPatch.mockResolvedValue(patchSuccessResponse);
    const opts = buildOptions({ apiKeyEditState: 'touched', httpFields: { ...defaultHttpFields, apiKey: 'real-key' } });
    const { result } = renderHook(() => usePatchMcpServer(opts));

    await act(async () => { await result.current.patch(); });

    const [, body] = mockPatch.mock.calls[0];
    expect(body.http?.apiKey).toBe('real-key');
  });

  it('sendsMaskedEnvVarValueAsNull_WhenEnvVarStateIsMasked', async () => {
    mockPatch.mockResolvedValue(patchSuccessResponse);
    const stdioFields: McpServerStdioFields = {
      command: 'node',
      args: [],
      envVars: [{ key: 'SECRET', value: '' }],
    };
    const opts = buildOptions({
      transportType: 'stdio',
      stdioFields,
      envVarEditStateMap: { 0: 'masked' },
    });
    const { result } = renderHook(() => usePatchMcpServer(opts));

    await act(async () => { await result.current.patch(); });

    const [, body] = mockPatch.mock.calls[0];
    expect(body.stdio?.envVars?.[0].value).toBeNull();
  });

  it('setsErrorStateOn409_WithIsNameConflictTrue', async () => {
    mockPatch.mockRejectedValue({ errorCode: 'DUPLICATE_NAME', message: 'Name taken' });
    const { result } = renderHook(() => usePatchMcpServer(buildOptions()));

    await act(async () => { await result.current.patch(); });

    expect(result.current.patchStatus).toBe('error');
    expect(result.current.isNameConflict).toBe(true);
    expect(result.current.patchError?.code).toBe('DUPLICATE_NAME');
  });

  it('setsErrorStateOn404', async () => {
    mockPatch.mockRejectedValue({ errorCode: 'NOT_FOUND', message: 'Not found' });
    const { result } = renderHook(() => usePatchMcpServer(buildOptions()));

    await act(async () => { await result.current.patch(); });

    expect(result.current.patchStatus).toBe('error');
    expect(result.current.patchError?.code).toBe('NOT_FOUND');
    expect(result.current.isNameConflict).toBe(false);
  });

  it('setsErrorStateOnNetworkFailure', async () => {
    mockPatch.mockRejectedValue({ errorCode: 'NETWORK', message: 'Network error' });
    const { result } = renderHook(() => usePatchMcpServer(buildOptions()));

    await act(async () => { await result.current.patch(); });

    expect(result.current.patchStatus).toBe('error');
    expect(result.current.patchError?.code).toBe('NETWORK');
  });

  it('clearErrorResetsToIdle', async () => {
    mockPatch.mockRejectedValue({ errorCode: 'NETWORK', message: 'err' });
    const { result } = renderHook(() => usePatchMcpServer(buildOptions()));

    await act(async () => { await result.current.patch(); });
    expect(result.current.patchStatus).toBe('error');

    act(() => { result.current.clearError(); });

    expect(result.current.patchStatus).toBe('idle');
    expect(result.current.patchError).toBeNull();
  });

  it('setsTransportTypeHTTP_InRequestBody', async () => {
    mockPatch.mockResolvedValue(patchSuccessResponse);
    const { result } = renderHook(() => usePatchMcpServer(buildOptions({ transportType: 'http' })));

    await act(async () => { await result.current.patch(); });

    const [, body] = mockPatch.mock.calls[0];
    expect(body.transportType).toBe('HTTP');
  });

  it('setsTransportTypeSTDIO_InRequestBody', async () => {
    mockPatch.mockResolvedValue(patchSuccessResponse);
    const opts = buildOptions({ transportType: 'stdio', stdioFields: defaultStdioFields });
    const { result } = renderHook(() => usePatchMcpServer(opts));

    await act(async () => { await result.current.patch(); });

    const [, body] = mockPatch.mock.calls[0];
    expect(body.transportType).toBe('STDIO');
    expect(body.stdio?.command).toBe('npx');
  });

  it('isNameConflictIsFalse_WhenStatusIsNotDuplicateName', async () => {
    mockPatch.mockRejectedValue({ errorCode: 'NOT_FOUND' });
    const { result } = renderHook(() => usePatchMcpServer(buildOptions()));

    await act(async () => { await result.current.patch(); });

    expect(result.current.isNameConflict).toBe(false);
  });
});
