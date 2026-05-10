import { renderHook, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { useAgentMcpAssignments } from './useAgentMcpAssignments';
import * as agentService from '../services/agentService';
import { AgentToolAssignmentsResponse } from '../services/agentService';

vi.mock('../services/agentService', () => ({
  getAgentMcpAssignments: vi.fn(),
}));

const mockGetAgentMcpAssignments = vi.mocked(agentService.getAgentMcpAssignments);

const makeAssignmentsResponse = (
  mcpAssignments: { mcpServerId: string; toolNames: string[] }[]
): AgentToolAssignmentsResponse => ({
  nativeToolActionIds: [],
  mcpAssignments,
});

describe('useAgentMcpAssignments', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns_empty_assignments_and_isLoading_false_when_isOpen_is_false', () => {
    const { result } = renderHook(() => useAgentMcpAssignments('agent-123', false));

    expect(result.current.assignments).toEqual({});
    expect(result.current.isLoading).toBe(false);
    expect(result.current.hasError).toBe(false);
    expect(mockGetAgentMcpAssignments).not.toHaveBeenCalled();
  });

  it('fetches_assignments_when_isOpen_is_true_scenario_2', async () => {
    mockGetAgentMcpAssignments.mockResolvedValueOnce(
      makeAssignmentsResponse([{ mcpServerId: 'server-abc', toolNames: ['tool1', 'tool2'] }])
    );

    const { result } = renderHook(() => useAgentMcpAssignments('agent-123', true));

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.assignments['server-abc']).toEqual(['tool1', 'tool2']);
    expect(result.current.hasError).toBe(false);
  });

  it('sets_isLoading_true_while_fetch_is_in_flight', () => {
    mockGetAgentMcpAssignments.mockImplementation(() => new Promise(() => {}));

    const { result } = renderHook(() => useAgentMcpAssignments('agent-123', true));

    expect(result.current.isLoading).toBe(true);
  });

  it('sets_hasError_true_when_fetch_fails', async () => {
    mockGetAgentMcpAssignments.mockRejectedValueOnce(new Error('Network error'));

    const { result } = renderHook(() => useAgentMcpAssignments('agent-123', true));

    await waitFor(() => expect(result.current.hasError).toBe(true));

    expect(result.current.assignments).toEqual({});
    expect(result.current.isLoading).toBe(false);
  });

  it('does_not_fetch_when_agentId_is_undefined', () => {
    const { result } = renderHook(() => useAgentMcpAssignments(undefined, true));

    expect(mockGetAgentMcpAssignments).not.toHaveBeenCalled();
    expect(result.current.assignments).toEqual({});
  });

  it('re_fetches_when_isOpen_changes_from_false_to_true', async () => {
    mockGetAgentMcpAssignments.mockResolvedValue(makeAssignmentsResponse([]));

    const { rerender } = renderHook(
      ({ open }: { open: boolean }) => useAgentMcpAssignments('agent-123', open),
      { initialProps: { open: false } }
    );

    expect(mockGetAgentMcpAssignments).toHaveBeenCalledTimes(0);

    rerender({ open: true });

    await waitFor(() => expect(mockGetAgentMcpAssignments).toHaveBeenCalledTimes(1));
  });

  it('maps_multiple_mcp_server_assignments_keyed_by_serverId', async () => {
    mockGetAgentMcpAssignments.mockResolvedValueOnce(
      makeAssignmentsResponse([
        { mcpServerId: 'server-abc', toolNames: ['tool1'] },
        { mcpServerId: 'server-xyz', toolNames: ['toolA', 'toolB', 'toolC'] },
      ])
    );

    const { result } = renderHook(() => useAgentMcpAssignments('agent-456', true));

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.assignments['server-abc']).toEqual(['tool1']);
    expect(result.current.assignments['server-xyz']).toEqual(['toolA', 'toolB', 'toolC']);
    expect(result.current.hasError).toBe(false);
  });

  it('returns_empty_assignments_when_no_mcp_assignments_exist', async () => {
    mockGetAgentMcpAssignments.mockResolvedValueOnce(makeAssignmentsResponse([]));

    const { result } = renderHook(() => useAgentMcpAssignments('agent-789', true));

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.assignments).toEqual({});
    expect(result.current.hasError).toBe(false);
  });
});
