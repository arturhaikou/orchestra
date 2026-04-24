
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getToken } from './authService';
import { ModelPullProgressEvent, ModelPullCompletedEvent, ModelPullFailedEvent, AgentExecutionCompletedEvent, ConnectionStatus } from '../types';

const HUB_URL = `${import.meta.env.VITE_API_URL}/hubs/notifications`;

let activeConnection: HubConnection | null = null;
let activeWorkspaceId: string | null = null;
let connectionStatus: ConnectionStatus = 'disconnected';
let connectionStatusHandler: ((status: ConnectionStatus) => void) | null = null;
let executionCompletedHandler: ((event: AgentExecutionCompletedEvent) => void) | null = null;

const RETRY_DELAYS = [0, 2000, 5000, 10000, 30000];

const emitConnectionStatus = (status: ConnectionStatus): void => {
  connectionStatus = status;
  connectionStatusHandler?.(status);
};

export const connect = async (workspaceId: string): Promise<void> => {
  const connection = new HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory: () => getToken() ?? '',
      withCredentials: false,
    })
    .withAutomaticReconnect(RETRY_DELAYS)
    .build();

  connection.onreconnecting(() => {
    emitConnectionStatus('reconnecting');
  });

  connection.onreconnected(async () => {
    emitConnectionStatus('connected');
    try {
      if (activeWorkspaceId) {
        await connection.invoke('JoinWorkspaceGroup', activeWorkspaceId);
      }
    } catch (err) {
      console.error('SignalR: Failed to re-join workspace group after reconnect:', err);
    }
  });

  connection.onclose(() => {
    emitConnectionStatus('disconnected');
  });

  connection.on('AgentExecutionCompleted', (event: AgentExecutionCompletedEvent) => {
    if (event.workspaceId !== activeWorkspaceId) return;
    executionCompletedHandler?.(event);
  });

  activeConnection = connection;
  activeWorkspaceId = workspaceId;

  try {
    await connection.start();
  } catch (err) {
    console.error('SignalR: Failed to start connection:', err);
    activeConnection = null;
    activeWorkspaceId = null;
    emitConnectionStatus('disconnected');
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

  if (activeConnection.state === HubConnectionState.Connected) {
    try {
      await activeConnection.invoke('LeaveWorkspaceGroup', workspaceId);
    } catch (err) {
      console.error('SignalR: Failed to leave workspace group:', err);
    }
  }

  await activeConnection.stop();
  activeConnection = null;
  activeWorkspaceId = null;
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

export const onConnectionStatusChange = (handler: (status: ConnectionStatus) => void): void => {
  connectionStatusHandler = handler;
};

export const getConnectionStatus = (): ConnectionStatus => {
  return connectionStatus;
};
