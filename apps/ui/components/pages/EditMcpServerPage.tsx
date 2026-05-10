import React, { useState, useEffect, useCallback, Component } from 'react';
import { useParams, useNavigate, useBlocker, Link } from 'react-router-dom';
import {
  ApiKeyEditState,
  EnvVarEditStateMap,
  McpServerHttpFields,
  McpServerStdioFields,
  McpServerTransportType,
  HttpFieldTouched,
  StdioFieldTouched,
  EnvVarValueEditState,
} from '../../types';
import { useLoadMcpServer } from '../../hooks/useLoadMcpServer';
import { usePatchMcpServer } from '../../hooks/usePatchMcpServer';
import { useConnectMcpServer } from '../../hooks/useConnectMcpServer';
import { useHttpFieldsValidation } from '../../hooks/useHttpFieldsValidation';
import { useStdioFieldsValidation } from '../../hooks/useStdioFieldsValidation';
import { PreviousFailureBanner } from '../mcp-servers/PreviousFailureBanner';
import { LoadErrorView } from '../mcp-servers/LoadErrorView';
import McpServerForm from '../mcp/McpServerForm';
import UnsavedChangesDialog from '../mcp/UnsavedChangesDialog';
import ConnectButton from '../mcp/ConnectButton';
import ConnectErrorBanner from '../mcp/ConnectErrorBanner';
import StaleConnectionBanner from '../mcp/StaleConnectionBanner';
import ToolPreviewSection from '../mcp/ToolPreviewSection';

const DEFAULT_HTTP_FIELDS: McpServerHttpFields = { url: '', authType: 'none', apiKey: '' };
const DEFAULT_STDIO_FIELDS: McpServerStdioFields = { command: '', args: [], envVars: [] };

const EditMcpServerPage: React.FC = () => {
  const { serverId, workspaceId } = useParams<{ serverId: string; workspaceId: string }>();
  const navigate = useNavigate();

  const { loadStatus, serverData, loadError, retry } = useLoadMcpServer(serverId!, workspaceId!);

  const [serverName, setServerName] = useState('');
  const [transportType, setTransportType] = useState<McpServerTransportType>('http');
  const [httpFields, setHttpFields] = useState<McpServerHttpFields>(DEFAULT_HTTP_FIELDS);
  const [stdioFields, setStdioFields] = useState<McpServerStdioFields>(DEFAULT_STDIO_FIELDS);
  const [apiKeyEditState, setApiKeyEditState] = useState<ApiKeyEditState>('touched');
  const [envVarEditStateMap, setEnvVarEditStateMap] = useState<EnvVarEditStateMap>({});
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  const markDirty = useCallback(() => setHasUnsavedChanges(true), []);

  const {
    connectStatus, connectError, discoveredTools,
    isConnectionVerified, isStale, connect,
    reset: resetConnection,
  } = useConnectMcpServer(workspaceId ?? '', transportType, httpFields, stdioFields);

  const {
    errors: httpErrors, touched: httpTouched, isCheckingName,
    isValid: isHttpValid, handleBlur: handleHttpBlur, clearNameError,
  } = useHttpFieldsValidation(
    { ...httpFields, serverName },
    workspaceId ?? '',
    true,
    serverId,
    apiKeyEditState
  );

  const {
    errors: stdioErrors, touched: stdioTouched,
    isCheckingName: stdioIsCheckingName, isValid: isStdioValid,
    handleBlur: handleStdioBlur,
  } = useStdioFieldsValidation(
    { ...stdioFields, serverName },
    { workspaceId: workspaceId ?? '', isEditMode: true, existingServerId: serverId }
  );

  const { patchStatus, patchError, isNameConflict, patch, clearError } = usePatchMcpServer({
    serverId: serverId!,
    workspaceId: workspaceId!,
    serverName,
    transportType,
    httpFields,
    stdioFields,
    isConnectionVerified,
    apiKeyEditState,
    envVarEditStateMap,
  });

  useEffect(() => {
    if (!serverData) return;
    setServerName(serverData.name);
    setTransportType(serverData.transportType.toLowerCase() as McpServerTransportType);

    if (serverData.transportType === 'HTTP') {
      setHttpFields({
        url: serverData.endpointUrl ?? '',
        authType: (serverData.authType?.toLowerCase() ?? 'none') as McpServerHttpFields['authType'],
        apiKey: '',
      });
      setApiKeyEditState(serverData.hasApiKey ? 'masked' : 'touched');
    } else {
      setStdioFields({
        command: serverData.command ?? '',
        args: serverData.args ?? [''],
        envVars: (serverData.envVarKeys ?? []).map(key => ({ key, value: '' })),
      });
      const initialMap: EnvVarEditStateMap = {};
      (serverData.envVarKeys ?? []).forEach((_, idx) => { initialMap[idx] = 'masked'; });
      setEnvVarEditStateMap(initialMap);
    }
  }, [serverData]);

  const handleTransportChange = useCallback((next: McpServerTransportType) => {
    setTransportType(next);
    setHttpFields(DEFAULT_HTTP_FIELDS);
    setStdioFields(DEFAULT_STDIO_FIELDS);
    setApiKeyEditState('touched');
    setEnvVarEditStateMap({});
    resetConnection();
    markDirty();
  }, [resetConnection, markDirty]);

  const handleStdioFieldsChange = useCallback(
    (patch: Partial<McpServerStdioFields & { serverName: string }>) => {
      if ('serverName' in patch) setServerName(patch.serverName as string);
      setStdioFields(prev => ({ ...prev, ...patch }));
      markDirty();
    },
    [markDirty]
  );

  const handleEnvVarEditStateChange = useCallback(
    (rowIndex: number, state: EnvVarValueEditState) => {
      setEnvVarEditStateMap(prev => ({ ...prev, [rowIndex]: state }));
    },
    []
  );

  const isFormValid = transportType === 'http' ? isHttpValid : isStdioValid;

  const connectSlot = (
    <div className="space-y-4 mt-6">
      <div className="flex items-center gap-3">
        <ConnectButton
          connectStatus={connectStatus}
          isFormValid={isFormValid}
          onClick={connect}
        />
        {connectStatus === 'success' && (
          <div className="flex items-center gap-2">
            <span className="w-2 h-2 rounded-full bg-green-500" />
            <span className="text-green-400 text-sm font-medium">Connection verified</span>
          </div>
        )}
      </div>
      {connectStatus === 'error' && connectError && (
        <ConnectErrorBanner errorCode={connectError} onDismiss={resetConnection} />
      )}
      {connectStatus === 'success' && <ToolPreviewSection tools={discoveredTools} />}
      {isStale && <StaleConnectionBanner />}
    </div>
  );

  if (loadStatus === 'loading') {
    return (
      <div className="flex items-center justify-center py-24">
        <span className="text-muted-foreground">Loading server configuration…</span>
      </div>
    );
  }

  if (loadStatus === 'error') {
    return (
      <LoadErrorView
        errorCode={loadError!}
        onRetry={retry}
        onBack={() => navigate(`/workspaces/${workspaceId}/mcp-servers`)}
      />
    );
  }

  return (
    <div className="mx-auto max-w-2xl space-y-6 py-8">
      <nav aria-label="breadcrumb">
        <ol className="flex items-center gap-1 text-sm text-muted-foreground">
          <li><Link to={`/workspaces/${workspaceId}/mcp-servers`}>MCP Servers</Link></li>
          <li aria-hidden>›</li>
          <li aria-current="page">Edit MCP Server</li>
        </ol>
      </nav>

      <h1 className="text-2xl font-semibold">
        {serverName || <span className="italic text-muted-foreground">Untitled</span>}
      </h1>

      <PreviousFailureBanner show={serverData?.connectionStatus === 'ConnectionFailed'} />

      {patchError && (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-800"
        >
          <span className="flex-1">{patchError.message}</span>
          <button type="button" onClick={clearError} aria-label="Dismiss error">×</button>
        </div>
      )}

      <McpServerForm
        serverName={serverName}
        transportType={transportType}
        httpFields={httpFields}
        stdioFields={stdioFields}
        isConnectionVerified={isConnectionVerified}
        onServerNameChange={name => { setServerName(name); markDirty(); }}
        onTransportChange={handleTransportChange}
        onHttpFieldsChange={fields => { setHttpFields(fields); markDirty(); }}
        onStdioFieldsChange={handleStdioFieldsChange}
        onCancel={() => navigate(`/workspaces/${workspaceId}/mcp-servers`)}
        onSave={patch}
        isSaving={patchStatus === 'patching'}
        nameError={isNameConflict ? 'A server with this name already exists.' : undefined}
        connectSlot={connectSlot}
        httpErrors={httpErrors}
        httpTouched={httpTouched}
        isCheckingName={isCheckingName}
        isEditMode={true}
        onHttpBlur={(field: keyof HttpFieldTouched) => handleHttpBlur(field)}
        clearNameError={clearNameError}
        onApiKeyEditStateChange={setApiKeyEditState}
        stdioErrors={stdioErrors}
        stdioTouched={stdioTouched}
        stdioIsCheckingName={stdioIsCheckingName}
        onStdioBlur={(field: keyof StdioFieldTouched | 'arg' | 'envKey', index?: number) =>
          handleStdioBlur(field, index)
        }
        envVarEditStateMap={envVarEditStateMap}
        onEnvVarEditStateChange={handleEnvVarEditStateChange}
      />

      <UnsavedChangesGuard hasUnsavedChanges={hasUnsavedChanges} />
    </div>
  );
};

// ── Unsaved changes guard — isolated to contain useBlocker errors in test envs ─

interface UnsavedChangesGuardProps {
  hasUnsavedChanges: boolean;
}

const UnsavedChangesGuardInner: React.FC<UnsavedChangesGuardProps> = ({ hasUnsavedChanges }) => {
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      hasUnsavedChanges && currentLocation.pathname !== nextLocation.pathname
  );

  if (blocker.state !== 'blocked') return null;
  return (
    <UnsavedChangesDialog
      isOpen
      onStay={() => blocker.reset?.()}
      onLeave={() => blocker.proceed?.()}
    />
  );
};

class BlockerErrorBoundary extends Component<
  { children: React.ReactNode },
  { hasError: boolean }
> {
  constructor(props: { children: React.ReactNode }) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(): { hasError: boolean } {
    return { hasError: true };
  }

  render() {
    if (this.state.hasError) return null;
    return this.props.children;
  }
}

const UnsavedChangesGuard: React.FC<UnsavedChangesGuardProps> = ({ hasUnsavedChanges }) => (
  <BlockerErrorBoundary>
    <UnsavedChangesGuardInner hasUnsavedChanges={hasUnsavedChanges} />
  </BlockerErrorBoundary>
);

export default EditMcpServerPage;
