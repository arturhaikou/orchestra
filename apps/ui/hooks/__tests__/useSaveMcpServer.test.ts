import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useSaveMcpServer } from '../useSaveMcpServer';
import * as mcpServersApi from '../../services/mcpServersApi';
import type {
  McpServerHttpFields,
  McpServerStdioFields,
  SaveMcpServerResponseDto,
} from '../../types';

const mockNavigate = vi.fn();
vi.mock('../../services/mcpServersApi');
vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));

const DEFAULT_HTTP: McpServerHttpFields = {
  url: 'https://mcp.example.com/api',
  authType: 'api_key',
  apiKey: 'my-secret',
};

const DEFAULT_STDIO: McpServerStdioFields = {
  command: 'npx',
  args: ['-y', 'my-server'],
  envVars: [],
};

const WORKSPACE_ID = 'ws-abc-123';
const SERVER_NAME = 'My Test Server';

const MOCK_RESPONSE: SaveMcpServerResponseDto = {
  id: 'int-001',
  workspaceId: WORKSPACE_ID,
  name: SERVER_NAME,
  connectionStatus: 'Connected',
  transportType: 'HTTP',
  endpointUrl: 'https://mcp.example.com/api',
  command: null,
  createdAt: '2026-05-01T12:00:00Z',
};

const baseOptions = {
  workspaceId: WORKSPACE_ID,
  serverName: SERVER_NAME,
  transportType: 'http' as const,
  httpFields: DEFAULT_HTTP,
  stdioFields: DEFAULT_STDIO,
  isConnectionVerified: true,
  saveIntent: 'created' as const,
};

beforeEach(() => {
  vi.clearAllMocks();
  mockNavigate.mockClear();
});

// ─── Scenario 3: Save is not possible without prior Connect verification ──────

describe('Scenario 3: Save is disabled when connection is not verified', () => {
  it('Initial_SaveStatus_IsIdle', () => {
    const { result } = renderHook(() =>
      useSaveMcpServer({ ...baseOptions, isConnectionVerified: false })
    );
    expect(result.current.saveStatus).toBe('idle');
  });

  it('Save_IsNoOp_WhenConnectionNotVerified', async () => {
    const { result } = renderHook(() =>
      useSaveMcpServer({ ...baseOptions, isConnectionVerified: false })
    );

    await act(async () => { await result.current.save(); });

    expect(result.current.saveStatus).toBe('idle');
    expect(mcpServersApi.saveMcpServer).not.toHaveBeenCalled();
  });
});

// ─── Scenario 2: Save button loading state ───────────────────────────────────

describe('Scenario 2: Save button shows loading state while in-flight', () => {
  it('Save_TransitionsToSaving_WhileRequestIsInFlight', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockReturnValue(new Promise(() => {}));

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    act(() => { result.current.save(); });

    expect(result.current.saveStatus).toBe('saving');
  });

  it('Save_IsNoOp_WhenAlreadySaving_PreventDoubleSubmit', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockReturnValue(new Promise(() => {}));

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    act(() => { result.current.save(); });
    await act(async () => { await result.current.save(); }); // second call while saving

    expect(vi.mocked(mcpServersApi.saveMcpServer)).toHaveBeenCalledTimes(1);
  });
});

// ─── Scenario 1: Happy path — create ─────────────────────────────────────────

describe('Scenario 1: Successful save navigates to list with toast', () => {
  it('Save_OnSuccess_TransitionsToSuccess', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockResolvedValue(MOCK_RESPONSE);

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    await act(async () => { await result.current.save(); });

    expect(result.current.saveStatus).toBe('success');
  });

  it('Save_OnSuccess_CallsNavigateWithCreatedToast', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockResolvedValue(MOCK_RESPONSE);

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    await act(async () => { await result.current.save(); });

    expect(mockNavigate).toHaveBeenCalledWith(
      '/mcp-servers',
      expect.objectContaining({
        state: expect.objectContaining({
          toast: expect.objectContaining({
            intent: 'created',
            serverName: SERVER_NAME,
          }),
        }),
      })
    );
  });
});

// ─── Scenario 5: Successful edit save ────────────────────────────────────────

describe('Scenario 5: Successful edit save emits updated toast', () => {
  it('Save_OnSuccess_WhenEditMode_NavigatesWithUpdatedToast', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockResolvedValue({ ...MOCK_RESPONSE, name: 'My Server' });

    const { result } = renderHook(() =>
      useSaveMcpServer({ ...baseOptions, serverName: 'My Server', saveIntent: 'updated' })
    );

    await act(async () => { await result.current.save(); });

    expect(mockNavigate).toHaveBeenCalledWith(
      '/mcp-servers',
      expect.objectContaining({
        state: expect.objectContaining({
          toast: expect.objectContaining({ intent: 'updated', serverName: 'My Server' }),
        }),
      })
    );
  });
});

// ─── Scenario 4: Network error ────────────────────────────────────────────────

describe('Scenario 4: Network error — user stays on form with error banner', () => {
  it('Save_OnNetworkError_TransitionsToError', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockRejectedValue({
      errorCode: 'NETWORK',
      message: 'Network error',
    });

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    await act(async () => { await result.current.save(); });

    expect(result.current.saveStatus).toBe('error');
  });

  it('Save_OnNetworkError_SetsCorrectErrorMessage', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockRejectedValue({
      errorCode: 'NETWORK',
      message: 'Network error',
    });

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    await act(async () => { await result.current.save(); });

    expect(result.current.saveError?.code).toBe('NETWORK');
    expect(result.current.saveError?.message).toBe(
      'Failed to save. Please check your connection and try again.'
    );
  });
});

// ─── Scenario 6: Duplicate name ──────────────────────────────────────────────

describe('Scenario 6: Name conflict — shows name field error', () => {
  it('Save_OnDuplicateName_SetsIsNameConflictTrue', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockRejectedValue({
      errorCode: 'DUPLICATE_NAME',
      message: 'Duplicate',
    });

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    await act(async () => { await result.current.save(); });

    expect(result.current.isNameConflict).toBe(true);
    expect(result.current.saveError?.code).toBe('DUPLICATE_NAME');
  });

  it('Save_OnDuplicateName_ShowsCorrectBannerMessage', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockRejectedValue({
      errorCode: 'DUPLICATE_NAME',
      message: '',
    });

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    await act(async () => { await result.current.save(); });

    expect(result.current.saveError?.message).toBe(
      'A server with this name already exists. Please choose a different name.'
    );
  });
});

// ─── clearError ───────────────────────────────────────────────────────────────

describe('clearError: resets state back to idle', () => {
  it('ClearError_AfterFailure_ResetsStatusToIdle', async () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockRejectedValue({
      errorCode: 'NETWORK',
      message: '',
    });

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    await act(async () => { await result.current.save(); });
    expect(result.current.saveStatus).toBe('error');

    act(() => { result.current.clearError(); });

    expect(result.current.saveStatus).toBe('idle');
    expect(result.current.saveError).toBeNull();
  });
});

// ─── Scenario 7: Cancel disabled while saving ────────────────────────────────

describe('Scenario 7: Cancel button is disabled while save is in-flight', () => {
  it('Save_WhileSaving_SaveStatusIsSaving', () => {
    vi.mocked(mcpServersApi.saveMcpServer).mockReturnValue(new Promise(() => {}));

    const { result } = renderHook(() => useSaveMcpServer(baseOptions));

    act(() => { result.current.save(); });

    // saveStatus === 'saving' drives FormFooter to disable Cancel
    expect(result.current.saveStatus).toBe('saving');
  });
});
