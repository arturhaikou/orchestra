import { renderHook, act } from '@testing-library/react';
import { vi, beforeEach } from 'vitest';
import { ConnectionStatus } from '../../types';
import { useConnectionStatus } from '../useConnectionStatus';

let capturedHandler: ((status: ConnectionStatus) => void) | null = null;

vi.mock('../../services/signalRService', () => ({
  getConnectionStatus: vi.fn(() => 'disconnected' as ConnectionStatus),
  onConnectionStatusChange: vi.fn((handler: (status: ConnectionStatus) => void) => {
    capturedHandler = handler;
  }),
}));

vi.mock('../../services/agentService', () => ({ getAgents: vi.fn(() => Promise.resolve([])) }));
vi.mock('../../services/ticketService', () => ({ getTickets: vi.fn(() => Promise.resolve([])) }));

beforeEach(() => {
  capturedHandler = null;
  vi.clearAllMocks();
});

describe('useConnectionStatus', () => {
  it('DefaultStatus_OnInit_ReturnsDisconnected', () => {
    const { result } = renderHook(() => useConnectionStatus());
    expect(result.current.status).toBe('disconnected');
  });

  it('Status_AfterConnectionEstablished_ReturnsConnected', () => {
    const { result } = renderHook(() => useConnectionStatus());
    act(() => { capturedHandler?.('connected'); });
    expect(result.current.status).toBe('connected');
  });

  it('Status_DuringReconnection_ReturnsReconnecting', () => {
    const { result } = renderHook(() => useConnectionStatus());
    act(() => { capturedHandler?.('reconnecting'); });
    expect(result.current.status).toBe('reconnecting');
  });

  it('Status_AfterDisconnect_ReturnsDisconnected', () => {
    const { result } = renderHook(() => useConnectionStatus());
    act(() => { capturedHandler?.('connected'); });
    act(() => { capturedHandler?.('disconnected'); });
    expect(result.current.status).toBe('disconnected');
  });

  it('Status_AfterReconnectCompletes_RefreshesState', () => {
    const { result } = renderHook(() => useConnectionStatus('ws-1'));
    act(() => { capturedHandler?.('reconnecting'); });
    act(() => { capturedHandler?.('connected'); });
    expect(result.current.status).toBe('connected');
  });
});
