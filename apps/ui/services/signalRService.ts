
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getToken } from './authService';
import { ModelPullProgressEvent, ModelPullCompletedEvent, ModelPullFailedEvent, AgentExecutionCompletedEvent, TicketStatusChangedEvent, ConnectionStatus } from '../types';

export type { ConnectionStatus };

const HUB_URL = `${import.meta.env.VITE_API_URL}/hubs/notifications`;

let activeConnection: HubConnection | null = null;
let activeWorkspaceId: string | null = null;
let connectionStatus: ConnectionStatus = 'disconnected';
let connectionStatusHandlers: Set<(status: ConnectionStatus) => void> = new Set();
let executionCompletedHandler: ((event: AgentExecutionCompletedEvent) => void) | null = null;
let ticketStatusChangedHandlers: Set<(event: TicketStatusChangedEvent) => void> = new Set();
let reconnectedHandlers: Set<() => void> = new Set();

const RETRY_DELAYS = [0, 2000, 5000, 10000, 30000];

const emitConnectionStatus = (status: ConnectionStatus): void => {
  connectionStatus = status;
  connectionStatusHandlers.forEach(handler => handler(status));
};

export const connect = async (workspaceId: string): Promise<void> => {
  if (activeConnection?.state === HubConnectionState.Connected && activeWorkspaceId === workspaceId) return;

  const connection = new HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory: () => getToken() ?? '',
      withCredentials: false,
    })
    .withAutomaticReconnect(RETRY_DELAYS)
    .build();

  connection.onreconnecting(() => {
    if (activeConnection !== connection) return;
    emitConnectionStatus('reconnecting');
  });

  connection.onreconnected(async () => {
    if (activeConnection !== connection) return;
    emitConnectionStatus('connected');
    try {
      if (activeWorkspaceId) {
        await connection.invoke('JoinWorkspaceGroup', activeWorkspaceId);
      }
    } catch (err) {
      console.error('SignalR: Failed to re-join workspace group after reconnect:', err);
    }
    reconnectedHandlers.forEach(h => h());
  });

  connection.onclose(() => {
    if (activeConnection !== connection) return;
    emitConnectionStatus('disconnected');
  });

  connection.on('AgentExecutionCompleted', (event: AgentExecutionCompletedEvent) => {
    if (event.workspaceId !== activeWorkspaceId) return;
    executionCompletedHandler?.(event);
  });

  connection.on('TicketStatusChanged', (event: TicketStatusChangedEvent) => {
    if (event.workspaceId !== activeWorkspaceId) return;
    ticketStatusChangedHandlers.forEach(h => h(event));
  });

  activeConnection = connection;
  activeWorkspaceId = workspaceId;

  try {
    await connection.start();
  } catch (err) {
    console.error('SignalR: Failed to start connection:', err);
    if (activeConnection === connection) {
      activeConnection = null;
      activeWorkspaceId = null;
      emitConnectionStatus('disconnected');
    }
    throw err;
  }

  try {
    await connection.invoke('JoinWorkspaceGroup', workspaceId);
    emitConnectionStatus('connected');
  } catch (err) {
    console.error('SignalR: Failed to join workspace group:', err);
    throw err;
  }
};

export const disconnect = async (workspaceId: string): Promise<void> => {
  if (!activeConnection) return;

  const connection = activeConnection;

  if (connection.state === HubConnectionState.Connected) {
    try {
      await connection.invoke('LeaveWorkspaceGroup', workspaceId);
    } catch (err) {
      console.error('SignalR: Failed to leave workspace group:', err);
    }
  }

  await connection.stop();

  if (activeConnection === connection) {
    activeConnection = null;
    activeWorkspaceId = null;
  }
};

export const switchWorkspace = async (fromId: string, toId: string): Promise<void> => {
  if (fromId === toId) return;

  if (!activeConnection || activeConnection.state !== HubConnectionState.Connected) {
    await disconnect(fromId);
    await connect(toId);
    return;
  }

  try {
    await activeConnection.invoke('LeaveWorkspaceGroup', fromId);
  } catch (err) {
    console.error('SignalR: Failed to leave workspace group:', err);
  }

  activeWorkspaceId = toId;

  try {
    await activeConnection.invoke('JoinWorkspaceGroup', toId);
  } catch (err) {
    console.error('SignalR: Failed to join new workspace group:', err);
    throw err;
  }
};

export const onModelPullProgress = (handler: (event: ModelPullProgressEvent) => void): void => {
  if (!activeConnection) return;
  activeConnection.off('ModelPullProgress');
  activeConnection.on('ModelPullProgress', handler);
};

export const onModelPullCompleted = (handler: (event: ModelPullCompletedEvent) => void): void => {
  if (!activeConnection) return;
  activeConnection.off('ModelPullCompleted');
  activeConnection.on('ModelPullCompleted', handler);
};

export const onModelPullFailed = (handler: (event: ModelPullFailedEvent) => void): void => {
  if (!activeConnection) return;
  activeConnection.off('ModelPullFailed');
  activeConnection.on('ModelPullFailed', handler);
};

export const onAgentExecutionCompleted = (handler: (event: AgentExecutionCompletedEvent) => void): void => {
  executionCompletedHandler = handler;
};

export const offAgentExecutionCompleted = (): void => {
  executionCompletedHandler = null;
};

export const onTicketStatusChanged = (handler: (event: TicketStatusChangedEvent) => void): (() => void) => {
  ticketStatusChangedHandlers.add(handler);
  return () => ticketStatusChangedHandlers.delete(handler);
};

export const offTicketStatusChanged = (): void => {
  ticketStatusChangedHandlers.clear();
};

export const onReconnected = (handler: () => void): (() => void) => {
  reconnectedHandlers.add(handler);
  return () => reconnectedHandlers.delete(handler);
};

export const offReconnected = (): void => {
  reconnectedHandlers.clear();
};

export const onConnectionStatusChange = (handler: (status: ConnectionStatus) => void): (() => void) => {
  connectionStatusHandlers.add(handler);
  return () => connectionStatusHandlers.delete(handler);
};

export const getConnectionStatus = (): ConnectionStatus => {
  return connectionStatus;
};

export const onJobCreated = (handler: (data: any) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('JobCreated', handler);
  return () => activeConnection?.off('JobCreated', handler);
};

export const onJobStatusChanged = (handler: (data: any) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('JobStatusChanged', handler);
  return () => activeConnection?.off('JobStatusChanged', handler);
};

export const onJobStepAdded = (handler: (data: any) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('JobStepAdded', handler);
  return () => activeConnection?.off('JobStepAdded', handler);
};
