import { useState, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { saveMcpServer } from '../services/mcpServersApi';
import type { SaveMcpServerRequest } from '../services/mcpServersApi';
import type {
  McpServerHttpFields,
  McpServerStdioFields,
  ApiKeyEditState,
  EnvVarEditStateMap,
  SaveStatus,
  SaveMcpServerError,
  McpServerSavedLocationState,
} from '../types';

export interface UseSaveMcpServerOptions {
  workspaceId: string;
  serverName: string;
  transportType: 'http' | 'stdio';
  httpFields: McpServerHttpFields;
  stdioFields: McpServerStdioFields;
  isConnectionVerified: boolean;
  apiKeyEditState?: ApiKeyEditState;
  envVarEditStateMap?: EnvVarEditStateMap;
  saveIntent: 'created' | 'updated';
  successPath?: string;
}

export interface UseSaveMcpServerReturn {
  saveStatus: SaveStatus;
  saveError: SaveMcpServerError | null;
  isNameConflict: boolean;
  save: () => Promise<void>;
  clearError: () => void;
}

export const useSaveMcpServer = ({
  workspaceId,
  serverName,
  transportType,
  httpFields,
  stdioFields,
  isConnectionVerified,
  apiKeyEditState,
  envVarEditStateMap,
  saveIntent,
  successPath = '/mcp-servers',
}: UseSaveMcpServerOptions): UseSaveMcpServerReturn => {
  const navigate = useNavigate();
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle');
  const [saveError, setSaveError] = useState<SaveMcpServerError | null>(null);

  const save = useCallback(async () => {
    if (saveStatus === 'saving') return;
    if (!isConnectionVerified) return;

    setSaveStatus('saving');
    setSaveError(null);

    try {
      const body = buildRequestBody(
        workspaceId, serverName, transportType,
        httpFields, stdioFields, apiKeyEditState, envVarEditStateMap
      );
      await saveMcpServer(body);
      setSaveStatus('success');
      navigateWithToast(navigate, successPath, saveIntent, serverName.trim());
    } catch (err: unknown) {
      setSaveStatus('error');
      setSaveError(mapErrorToSaveError(err as { errorCode: string; message: string }));
    }
  }, [
    saveStatus, isConnectionVerified, workspaceId, serverName, transportType,
    httpFields, stdioFields, apiKeyEditState, envVarEditStateMap,
    navigate, successPath, saveIntent,
  ]);

  const clearError = useCallback(() => {
    setSaveError(null);
    setSaveStatus('idle');
  }, []);

  return {
    saveStatus,
    saveError,
    isNameConflict: saveError?.code === 'DUPLICATE_NAME',
    save,
    clearError,
  };
};

function buildRequestBody(
  workspaceId: string,
  serverName: string,
  transportType: 'http' | 'stdio',
  httpFields: McpServerHttpFields,
  stdioFields: McpServerStdioFields,
  apiKeyEditState: ApiKeyEditState | undefined,
  envVarEditStateMap: EnvVarEditStateMap | undefined
): SaveMcpServerRequest {
  const base = {
    workspaceId,
    name: serverName.trim(),
    transportType: transportType.toUpperCase() as 'HTTP' | 'STDIO',
  };

  if (transportType === 'http') {
    return { ...base, http: buildHttpPayload(httpFields, apiKeyEditState) };
  }

  return { ...base, stdio: buildStdioPayload(stdioFields, envVarEditStateMap) };
}

function buildHttpPayload(
  fields: McpServerHttpFields,
  apiKeyEditState: ApiKeyEditState | undefined
): { url: string; authType: 'NONE' | 'API_KEY' | 'BEARER_TOKEN'; apiKey?: string | null } {
  const authType = fields.authType.toUpperCase() as 'NONE' | 'API_KEY' | 'BEARER_TOKEN';

  if (authType === 'NONE') {
    return { url: fields.url, authType };
  }

  if (apiKeyEditState === 'masked') {
    return { url: fields.url, authType, apiKey: null };
  }

  return { url: fields.url, authType, apiKey: fields.apiKey ?? '' };
}

function buildStdioPayload(
  fields: McpServerStdioFields,
  envVarEditStateMap: EnvVarEditStateMap | undefined
): { command: string; args: string[]; envVars: { key: string; value: string | null }[] } {
  const envVars = fields.envVars.map((envVar, index) => {
    const editState = envVarEditStateMap?.[index];
    return editState === 'masked'
      ? { key: envVar.key, value: null }
      : { key: envVar.key, value: envVar.value };
  });

  return { command: fields.command, args: fields.args, envVars };
}

function navigateWithToast(
  navigate: ReturnType<typeof useNavigate>,
  path: string,
  intent: 'created' | 'updated',
  serverName: string
): void {
  const locationState: McpServerSavedLocationState = {
    toast: { intent, serverName },
  };
  navigate(path, { state: locationState });
}

const SAVE_ERROR_MESSAGES: Record<string, string> = {
  DUPLICATE_NAME: 'A server with this name already exists. Please choose a different name.',
  VALIDATION_ERROR: 'Some fields are invalid. Please review the form and try again.',
  NETWORK: 'Failed to save. Please check your connection and try again.',
  UNKNOWN: 'An unexpected error occurred. Please try again.',
};

function mapErrorToSaveError(dto: { errorCode: string; message: string }): SaveMcpServerError {
  switch (dto.errorCode) {
    case 'DUPLICATE_NAME':
      return { code: 'DUPLICATE_NAME', message: SAVE_ERROR_MESSAGES.DUPLICATE_NAME };
    case 'VALIDATION_ERROR':
      return { code: 'VALIDATION', message: SAVE_ERROR_MESSAGES.VALIDATION_ERROR };
    case 'NETWORK':
      return { code: 'NETWORK', message: SAVE_ERROR_MESSAGES.NETWORK };
    default:
      return { code: 'UNKNOWN', message: SAVE_ERROR_MESSAGES.UNKNOWN };
  }
}
