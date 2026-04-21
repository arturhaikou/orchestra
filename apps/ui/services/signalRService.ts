
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getToken } from './authService';
import { ModelPullProgressEvent, ModelPullCompletedEvent, ModelPullFailedEvent } from '../types';

const HUB_URL = `${import.meta.env.VITE_API_URL}/hubs/notifications`;

let activeConnection: HubConnection | null = null;
let activeWorkspaceId: string | null = null;

export const connect = async (workspaceId: string): Promise<void> => {
  const connection = new HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory: () => getToken() ?? '',
      withCredentials: false,
    })
    .withAutomaticReconnect()
    .build();

  connection.onreconnected(async () => {
    try {
      await connection.invoke('JoinWorkspaceGroup', workspaceId);
    } catch (err) {
      console.error('SignalR: Failed to re-join workspace group after reconnect:', err);
    }
  });

  activeConnection = connection;
  activeWorkspaceId = workspaceId;

  try {
    await connection.start();
  } catch (err) {
    console.error('SignalR: Failed to start connection:', err);
    activeConnection = null;
    activeWorkspaceId = null;
    throw err;
  }

  try {
    await connection.invoke('JoinWorkspaceGroup', workspaceId);
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
