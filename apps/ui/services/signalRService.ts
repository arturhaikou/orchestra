
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
import { getToken } from './authService';
import { ModelPullProgressEvent, ModelPullCompletedEvent, ModelPullFailedEvent, AgentExecutionCompletedEvent, TicketStatusChangedEvent, ConnectionStatus } from '../types';

export type { ConnectionStatus };

export interface GlobalAgentQuestionEvent {
  workspaceId: string;
  workspaceName: string;
  questionId: string;
  ticketId: string | null;
  ticketTitle: string | null;
  agentName: string;
  questionsJson: string;
  createdAt: string;
}

const HUB_URL = `${import.meta.env.VITE_API_URL}/hubs/notifications`;

let activeConnection: HubConnection | null = null;
let activeWorkspaceId: string | null = null;
let connectionStatus: ConnectionStatus = 'disconnected';
let connectionStatusHandlers: Set<(status: ConnectionStatus) => void> = new Set();
let executionCompletedHandler: ((event: AgentExecutionCompletedEvent) => void) | null = null;
let ticketStatusChangedHandlers: Set<(event: TicketStatusChangedEvent) => void> = new Set();
let reconnectedHandlers: Set<() => void> = new Set();
let globalQuestionHandlers: Set<(event: GlobalAgentQuestionEvent) => void> = new Set();
let globalQuestionResolvedHandlers: Set<(event: { questionId: string }) => void> = new Set();

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
      await connection.invoke('JoinUserGroup');
    } catch (err) {
      console.error('SignalR: Failed to re-join groups after reconnect:', err);
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

  connection.on('GlobalAgentQuestionAsked', (event: GlobalAgentQuestionEvent) => {
    globalQuestionHandlers.forEach(h => h(event));
  });

  connection.on('GlobalAgentQuestionResolved', (event: { questionId: string }) => {
    globalQuestionResolvedHandlers.forEach(h => h(event));
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
    await connection.invoke('JoinUserGroup');
    emitConnectionStatus('connected');
  } catch (err) {
    console.error('SignalR: Failed to join groups:', err);
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

export const onAgentQuestionAsked = (handler: (data: { workspaceId: string; questionId: string }) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('AgentQuestionAsked', handler);
  return () => activeConnection?.off('AgentQuestionAsked', handler);
};

export const onAgentQuestionResolved = (handler: (data: { workspaceId: string; questionId: string }) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('AgentQuestionResolved', handler);
  return () => activeConnection?.off('AgentQuestionResolved', handler);
};

export interface WorkflowStepStartedEvent {
  workflowExecutionId: string;
  ticketId: string;
  stepIndex: number;
}

export interface WorkflowStepCompletedEvent {
  workflowExecutionId: string;
  ticketId: string;
  stepIndex: number;
  status: string;
}

export interface WorkflowExecutionStatusChangedEvent {
  workflowExecutionId: string;
  ticketId: string;
  status: string;
}

export interface WorkflowStepJobAssignedEvent {
  workflowExecutionId: string;
  ticketId: string;
  stepIndex: number;
  jobId: string;
}

export interface WorkflowTicketSwitchedEvent {
  workspaceId: string;
  workflowExecutionId: string;
  previousTicketId: string;
  newTicketId: string;
  externalTicketKey: string;
}

export const onWorkflowStepStarted = (handler: (event: WorkflowStepStartedEvent) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('WorkflowStepStarted', handler);
  return () => activeConnection?.off('WorkflowStepStarted', handler);
};

export const onWorkflowStepCompleted = (handler: (event: WorkflowStepCompletedEvent) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('WorkflowStepCompleted', handler);
  return () => activeConnection?.off('WorkflowStepCompleted', handler);
};

export const onWorkflowExecutionStatusChanged = (handler: (event: WorkflowExecutionStatusChangedEvent) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('WorkflowExecutionStatusChanged', handler);
  return () => activeConnection?.off('WorkflowExecutionStatusChanged', handler);
};

export const onWorkflowStepJobAssigned = (handler: (event: WorkflowStepJobAssignedEvent) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('WorkflowStepJobAssigned', handler);
  return () => activeConnection?.off('WorkflowStepJobAssigned', handler);
};

export const onWorkflowTicketSwitched = (handler: (event: WorkflowTicketSwitchedEvent) => void): (() => void) => {
  if (!activeConnection) return () => {};
  activeConnection.on('WorkflowTicketSwitched', handler);
  return () => activeConnection?.off('WorkflowTicketSwitched', handler);
};

export const onGlobalAgentQuestionAsked = (handler: (event: GlobalAgentQuestionEvent) => void): (() => void) => {
  globalQuestionHandlers.add(handler);
  return () => globalQuestionHandlers.delete(handler);
};

export const onGlobalAgentQuestionResolved = (handler: (event: { questionId: string }) => void): (() => void) => {
  globalQuestionResolvedHandlers.add(handler);
  return () => globalQuestionResolvedHandlers.delete(handler);
};
