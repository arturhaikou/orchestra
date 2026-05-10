import { renderHook, act, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { useMcpServers } from '../useMcpServers';
import { McpServer } from '../../types';

const mockServer = (overrides: Partial<McpServer> = {}): McpServer => ({
  id: 'server-1',
  workspaceId: 'ws-1',
  name: 'Test Server',
  connectionStatus: 'Connected',
  transportType: 'HTTP',
  endpointUrl: 'https://mcp.example.com',
  createdAt: '2026-04-28T10:22:00.000Z',
  ...overrides,
});

vi.mock('../../services/mcpServerService', () => ({
  getMcpServers: vi.fn(),
  deleteMcpServer: vi.fn(),
  fetchDeleteImpact: vi.fn(),
  McpServerNotFoundError: class McpServerNotFoundError extends Error {
    constructor(id: string) { super(`not found: ${id}`); this.name = 'McpServerNotFoundError'; }
  },
  McpServerForbiddenError: class McpServerForbiddenError extends Error {
    constructor() { super('forbidden'); this.name = 'McpServerForbiddenError'; }
  },
}));
import * as mcpServerService from '../../services/mcpServerService';
import {
  McpServerNotFoundError,
  McpServerForbiddenError,
} from '../../services/mcpServerService';
import { DeleteServerOutcome } from '../useMcpServers';

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(mcpServerService.deleteMcpServer).mockResolvedValue({ affectedAgentCount: 0 });
});

describe('useMcpServers', () => {
  describe('initial loading state', () => {
    it('IsLoading_OnMount_IsTrue', async () => {
      vi.mocked(mcpServerService.getMcpServers).mockImplementation(
        () => new Promise(() => {}) // never resolves — keeps loading state
      );

      const { result } = renderHook(() => useMcpServers('ws-1'));

      expect(result.current.isLoading).toBe(true);
      expect(result.current.servers).toEqual([]);
      expect(result.current.hasError).toBe(false);
    });

    it('IsLoading_WhenWorkspaceIdUndefined_StaysFalse', () => {
      const { result } = renderHook(() => useMcpServers(undefined));

      expect(result.current.isLoading).toBe(false);
      expect(result.current.servers).toEqual([]);
    });
  });

  describe('successful fetch', () => {
    it('Servers_AfterSuccessfulFetch_ReturnsServerList', async () => {
      const servers = [mockServer({ id: 'server-1' }), mockServer({ id: 'server-2' })];
      vi.mocked(mcpServerService.getMcpServers).mockResolvedValue(servers);

      const { result } = renderHook(() => useMcpServers('ws-1'));

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.servers).toHaveLength(2);
      expect(result.current.hasError).toBe(false);
    });

    it('IsLoading_AfterSuccessfulFetch_IsFalse', async () => {
      vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([]);

      const { result } = renderHook(() => useMcpServers('ws-1'));

      await waitFor(() => expect(result.current.isLoading).toBe(false));
    });

    it('Servers_AfterEmptyFetch_ReturnsEmptyList', async () => {
      vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([]);

      const { result } = renderHook(() => useMcpServers('ws-1'));

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.servers).toEqual([]);
      expect(result.current.hasError).toBe(false);
    });
  });

  describe('error state', () => {
    it('HasError_WhenFetchFails_IsTrue', async () => {
      vi.mocked(mcpServerService.getMcpServers).mockRejectedValue(new Error('HTTP 500'));

      const { result } = renderHook(() => useMcpServers('ws-1'));

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.hasError).toBe(true);
      expect(result.current.servers).toEqual([]);
    });

    it('IsLoading_WhenFetchFails_IsFalse', async () => {
      vi.mocked(mcpServerService.getMcpServers).mockRejectedValue(new Error('HTTP 403'));

      const { result } = renderHook(() => useMcpServers('ws-1'));

      await waitFor(() => expect(result.current.isLoading).toBe(false));

      expect(result.current.isLoading).toBe(false);
    });
  });

  describe('retry', () => {
    it('Retry_AfterError_RefetchesServers', async () => {
      vi.mocked(mcpServerService.getMcpServers)
        .mockRejectedValueOnce(new Error('HTTP 500'))
        .mockResolvedValue([mockServer()]);

      const { result } = renderHook(() => useMcpServers('ws-1'));

      await waitFor(() => expect(result.current.hasError).toBe(true));

      act(() => { result.current.retry(); });

      await waitFor(() => expect(result.current.hasError).toBe(false));
      expect(result.current.servers).toHaveLength(1);
    });

    it('IsLoading_DuringRetry_IsTrue', async () => {
      vi.mocked(mcpServerService.getMcpServers)
        .mockRejectedValueOnce(new Error('HTTP 500'))
        .mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => useMcpServers('ws-1'));

      await waitFor(() => expect(result.current.hasError).toBe(true));

      act(() => { result.current.retry(); });

      expect(result.current.isLoading).toBe(true);
    });

    it('HasError_ResetToFalse_WhenRetryStarts', async () => {
      vi.mocked(mcpServerService.getMcpServers)
        .mockRejectedValueOnce(new Error('HTTP 500'))
        .mockImplementation(() => new Promise(() => {}));

      const { result } = renderHook(() => useMcpServers('ws-1'));

      await waitFor(() => expect(result.current.hasError).toBe(true));

      act(() => { result.current.retry(); });

      expect(result.current.hasError).toBe(false);
    });
  });

  describe('removeServer', () => {
    it('RemoveServer_WithExistingId_FiltersItOut', async () => {
      const servers = [mockServer({ id: 'server-1' }), mockServer({ id: 'server-2' })];
      vi.mocked(mcpServerService.getMcpServers).mockResolvedValue(servers);

      const { result } = renderHook(() => useMcpServers('ws-1'));
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      act(() => { result.current.removeServer('server-1'); });

      expect(result.current.servers).toHaveLength(1);
      expect(result.current.servers[0].id).toBe('server-2');
    });

    it('RemoveServer_LastServer_ProducesEmptyList', async () => {
      vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([mockServer({ id: 'server-1' })]);

      const { result } = renderHook(() => useMcpServers('ws-1'));
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      act(() => { result.current.removeServer('server-1'); });

      expect(result.current.servers).toHaveLength(0);
    });

    it('RemoveServer_WithUnknownId_LeavesListUnchanged', async () => {
      const servers = [mockServer({ id: 'server-1' })];
      vi.mocked(mcpServerService.getMcpServers).mockResolvedValue(servers);

      const { result } = renderHook(() => useMcpServers('ws-1'));
      await waitFor(() => expect(result.current.isLoading).toBe(false));

      act(() => { result.current.removeServer('non-existent-id'); });

      expect(result.current.servers).toHaveLength(1);
    });
  });
});
describe('deleteServer', () => {
  const server1 = mockServer({ id: 'server-1', name: 'Analytics Server' });
  const server2 = mockServer({ id: 'server-2', name: 'Secondary Server' });

  it('DeleteServer_OnSuccess_ReturnsSuccessOutcome', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([server1, server2]);
    vi.mocked(mcpServerService.deleteMcpServer).mockResolvedValue({ affectedAgentCount: 0 });

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: DeleteServerOutcome;
    await act(async () => {
      outcome = await result.current.deleteServer('server-1');
    });

    expect(outcome!.success).toBe(true);
    expect(outcome!.errorMessage).toBeNull();
  });

  it('DeleteServer_OnSuccess_RemovesServerFromList', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([server1, server2]);
    vi.mocked(mcpServerService.deleteMcpServer).mockResolvedValue({ affectedAgentCount: 0 });

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.deleteServer('server-1');
    });

    expect(result.current.servers).toHaveLength(1);
    expect(result.current.servers[0].id).toBe('server-2');
  });

  it('DeleteServer_LastServer_ServersBecomesEmpty', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([server1]);
    vi.mocked(mcpServerService.deleteMcpServer).mockResolvedValue({ affectedAgentCount: 0 });

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.deleteServer('server-1');
    });

    expect(result.current.servers).toHaveLength(0);
  });

  it('DeleteServer_OnNetworkError_ReturnsFailureOutcome', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([server1]);
    vi.mocked(mcpServerService.deleteMcpServer).mockRejectedValue(
      new Error('Network request failed'),
    );

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: DeleteServerOutcome;
    await act(async () => {
      outcome = await result.current.deleteServer('server-1');
    });

    expect(outcome!.success).toBe(false);
    expect(outcome!.errorMessage).toBe('Network request failed');
  });

  it('DeleteServer_OnNetworkError_DoesNotRemoveServer', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([server1]);
    vi.mocked(mcpServerService.deleteMcpServer).mockRejectedValue(
      new Error('Network request failed'),
    );

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => {
      await result.current.deleteServer('server-1');
    });

    expect(result.current.servers).toHaveLength(1);
    expect(result.current.servers[0].id).toBe('server-1');
  });

  it('DeleteServer_On404NotFound_TreatsAsSuccess', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([server1]);
    vi.mocked(mcpServerService.deleteMcpServer).mockRejectedValue(
      new McpServerNotFoundError('server-1'),
    );

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: DeleteServerOutcome;
    await act(async () => {
      outcome = await result.current.deleteServer('server-1');
    });

    expect(outcome!.success).toBe(true);
    expect(outcome!.errorMessage).toBeNull();
    expect(result.current.servers).toHaveLength(0);
  });

  it('DeleteServer_On403Forbidden_ReturnsPermissionError', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([server1]);
    vi.mocked(mcpServerService.deleteMcpServer).mockRejectedValue(
      new McpServerForbiddenError(),
    );

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: DeleteServerOutcome;
    await act(async () => {
      outcome = await result.current.deleteServer('server-1');
    });

    expect(outcome!.success).toBe(false);
    expect(outcome!.errorMessage).toBe(
      'You do not have permission to delete this MCP server.',
    );
    expect(result.current.servers).toHaveLength(1);
  });
});

describe('deleteServer — affectedAgentCount propagation', () => {
  it('DeleteServer_WhenSuccessful_ReturnsAffectedCount', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([mockServer({ id: 'srv-5' })]);
    vi.mocked(mcpServerService.deleteMcpServer).mockResolvedValue({ affectedAgentCount: 3 });

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: DeleteServerOutcome | null = null;
    await act(async () => {
      outcome = await result.current.deleteServer('srv-5');
    });

    expect(outcome!.success).toBe(true);
    expect(outcome!.affectedAgentCount).toBe(3);
  });

  it('DeleteServer_WhenSuccessful_WithZeroAgents_ReturnsZeroCount', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([mockServer({ id: 'srv-6' })]);
    vi.mocked(mcpServerService.deleteMcpServer).mockResolvedValue({ affectedAgentCount: 0 });

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: DeleteServerOutcome | null = null;
    await act(async () => {
      outcome = await result.current.deleteServer('srv-6');
    });

    expect(outcome!.affectedAgentCount).toBe(0);
  });

  it('DeleteServer_WhenServerNotFound_StillReturnsSuccess_WithoutCount', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([mockServer({ id: 'srv-7' })]);
    vi.mocked(mcpServerService.deleteMcpServer).mockRejectedValue(
      new McpServerNotFoundError('srv-7'),
    );

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: DeleteServerOutcome | null = null;
    await act(async () => {
      outcome = await result.current.deleteServer('srv-7');
    });

    expect(outcome!.success).toBe(true);
    expect(outcome!.affectedAgentCount).toBeUndefined();
  });

  it('DeleteServer_WhenForbidden_ReturnsFailureWithoutCount', async () => {
    vi.mocked(mcpServerService.getMcpServers).mockResolvedValue([mockServer({ id: 'srv-8' })]);
    vi.mocked(mcpServerService.deleteMcpServer).mockRejectedValue(
      new McpServerForbiddenError(),
    );

    const { result } = renderHook(() => useMcpServers('ws-1'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    let outcome: DeleteServerOutcome | null = null;
    await act(async () => {
      outcome = await result.current.deleteServer('srv-8');
    });

    expect(outcome!.success).toBe(false);
    expect(outcome!.affectedAgentCount).toBeUndefined();
  });
});