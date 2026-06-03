import React, { useState, useMemo, useRef, useEffect, Component } from 'react';
import { useNavigate, useParams, useBlocker } from 'react-router-dom';
import {
  McpServerTransportType,
  McpServerHttpFields,
  McpServerStdioFields,
  ApiKeyEditState,
  HttpFieldTouched,
  StdioFieldTouched,
  EnvVarValueEditState,
  EnvVarEditStateMap,
} from '../../types';
import { useHttpFieldsValidation } from '../../hooks/useHttpFieldsValidation';
import { useStdioFieldsValidation } from '../../hooks/useStdioFieldsValidation';
import { useConnectMcpServer } from '../../hooks/useConnectMcpServer';
import { useSaveMcpServer } from '../../hooks/useSaveMcpServer';
import McpServerBreadcrumb from '../mcp/McpServerBreadcrumb';
import McpServerForm from '../mcp/McpServerForm';
import UnsavedChangesDialog from '../mcp/UnsavedChangesDialog';
import ConnectButton from '../mcp/ConnectButton';
import ConnectErrorBanner from '../mcp/ConnectErrorBanner';
import StaleConnectionBanner from '../mcp/StaleConnectionBanner';
import ToolPreviewSection from '../mcp/ToolPreviewSection';
import { SaveErrorBanner } from '../mcp/SaveErrorBanner';

const DEFAULT_HTTP_FIELDS: McpServerHttpFields = {
  url: '',
  authType: 'none',
  apiKey: '',
};

const DEFAULT_STDIO_FIELDS: McpServerStdioFields = {
  command: '',
  args: [],
  envVars: [],
};

interface CreateMcpServerPageProps {
  _initialTransportType?: McpServerTransportType;
  _initialConnectionVerified?: boolean;
}

const CreateMcpServerPage: React.FC<CreateMcpServerPageProps> = ({
  _initialTransportType,
  _initialConnectionVerified = false,
}) => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  const [serverName, setServerName] = useState('');
  const [transportType, setTransportType] = useState<McpServerTransportType>(_initialTransportType ?? 'http');
  const [httpFields, setHttpFields] = useState<McpServerHttpFields>(DEFAULT_HTTP_FIELDS);
  const [stdioFields, setStdioFields] = useState<McpServerStdioFields>(DEFAULT_STDIO_FIELDS);
  const [apiKeyEditState, setApiKeyEditState] = useState<ApiKeyEditState>('touched');
  const [envVarEditStateMap, setEnvVarEditStateMap] = useState<EnvVarEditStateMap>({});

  const {
    errors: httpErrors,
    touched: httpTouched,
    isCheckingName,
    isValid: isHttpValid,
    handleBlur: handleHttpBlur,
    clearNameError,
  } = useHttpFieldsValidation(
    { ...httpFields, serverName },
    workspaceId ?? '',
    false,
    undefined,
    apiKeyEditState
  );

  const {
    errors: stdioErrors,
    touched: stdioTouched,
    isCheckingName: stdioIsCheckingName,
    isValid: isStdioValid,
    handleBlur: handleStdioBlur,
  } = useStdioFieldsValidation(
    { ...stdioFields, serverName },
    { workspaceId: workspaceId ?? '', isEditMode: false }
  );

  const {
    connectStatus,
    connectError,
    discoveredTools,
    isConnectionVerified: hookConnectionVerified,
    isStale,
    connect,
    reset: resetConnection,
  } = useConnectMcpServer(
    workspaceId ?? '',
    transportType,
    httpFields,
    stdioFields
  );

  const [propConnectionVerified, setPropConnectionVerified] = useState(_initialConnectionVerified);
  const initialFieldsRef = useRef({ command: stdioFields.command, url: httpFields.url });

  useEffect(() => {
    if (!propConnectionVerified) return;
    const fieldsChanged =
      stdioFields.command !== initialFieldsRef.current.command ||
      httpFields.url !== initialFieldsRef.current.url;
    if (fieldsChanged) setPropConnectionVerified(false);
  }, [stdioFields.command, httpFields.url, propConnectionVerified]);

  const isConnectionVerified = hookConnectionVerified || propConnectionVerified;

  // Guard: redirect if workspaceId is missing
  useEffect(() => {
    if (!workspaceId) {
      navigate('/workspaces/new');
    }
  }, [workspaceId, navigate]);

  const {
    saveStatus,
    saveError,
    isNameConflict,
    save,
    clearError,
  } = useSaveMcpServer({
    workspaceId,
    serverName,
    transportType,
    httpFields,
    stdioFields,
    isConnectionVerified,
    apiKeyEditState,
    envVarEditStateMap,
    saveIntent: 'created',
    successPath: `/workspaces/${workspaceId}/mcp-servers`,
  });

  const isSaving = saveStatus === 'saving';

  const isFormValid = transportType === 'http' ? isHttpValid : isStdioValid;

  const isDirty = useMemo(
    () => computeIsDirty(serverName, transportType, httpFields, stdioFields),
    [serverName, transportType, httpFields, stdioFields]
  );

  const mcpServersPath = `/workspaces/${workspaceId}/mcp-servers`;

  const handleTransportChange = (next: McpServerTransportType) => {
    setTransportType(next);
    setHttpFields(DEFAULT_HTTP_FIELDS);
    setStdioFields(DEFAULT_STDIO_FIELDS);
    resetConnection();
  };

  const handleHttpFieldsChange = (fields: McpServerHttpFields) => {
    setHttpFields(fields);
  };

  const handleStdioFieldsChange = (
    patch: Partial<McpServerStdioFields & { serverName: string }>
  ) => {
    if ('serverName' in patch) setServerName(patch.serverName as string);
    setStdioFields(prev => ({ ...prev, ...patch }));
  };

  const handleEnvVarEditStateChange = (rowIndex: number, state: EnvVarValueEditState) => {
    setEnvVarEditStateMap(prev => ({ ...prev, [rowIndex]: state }));
  };

  const handleCancel = () => navigate(mcpServersPath);

  const connectSlot = buildConnectSlot({
    connectStatus,
    connectError,
    discoveredTools,
    isConnectionVerified,
    isStale,
    isFormValid,
    onConnect: connect,
    onDismissError: resetConnection,
  });

  // Only render if workspaceId is available
  if (!workspaceId) {
    return null;
  }

  return (
    <div className="max-w-2xl mx-auto py-8 px-4">
      <McpServerBreadcrumb workspaceId={workspaceId} />

      <h1 className="text-2xl font-bold bg-gradient-to-r from-text to-textMuted bg-clip-text text-transparent mb-6">Add MCP Server</h1>

      <SaveErrorBanner error={saveError} onDismiss={clearError} />

      <McpServerForm
        serverName={serverName}
        transportType={transportType}
        httpFields={httpFields}
        stdioFields={stdioFields}
        isConnectionVerified={isConnectionVerified}
        onServerNameChange={setServerName}
        onTransportChange={handleTransportChange}
        onHttpFieldsChange={handleHttpFieldsChange}
        onStdioFieldsChange={handleStdioFieldsChange}
        onCancel={handleCancel}
        onSave={save}
        isSaving={isSaving}
        nameError={isNameConflict ? 'A server with this name already exists.' : undefined}
        connectSlot={connectSlot}
        httpErrors={httpErrors}
        httpTouched={httpTouched}
        isCheckingName={isCheckingName}
        isEditMode={false}
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

      <UnsavedChangesGuard isDirty={isDirty} />
    </div>
  );
};

// ─── Connect slot builder ──────────────────────────────────────────────────────

interface ConnectSlotProps {
  connectStatus: ReturnType<typeof useConnectMcpServer>['connectStatus'];
  connectError: ReturnType<typeof useConnectMcpServer>['connectError'];
  discoveredTools: ReturnType<typeof useConnectMcpServer>['discoveredTools'];
  isConnectionVerified: boolean;
  isStale: boolean;
  isFormValid: boolean;
  onConnect: () => void;
  onDismissError: () => void;
}

function buildConnectSlot(props: ConnectSlotProps): React.ReactNode {
  const {
    connectStatus, connectError, discoveredTools,
    isConnectionVerified, isStale, isFormValid,
    onConnect, onDismissError,
  } = props;

  return (
    <div className="space-y-4 mt-6">
      <div className="flex items-center gap-3">
        <ConnectButton
          connectStatus={connectStatus}
          isFormValid={isFormValid}
          onClick={onConnect}
        />
        {connectStatus === 'success' && (
          <div className="flex items-center gap-2">
            <span className="w-2 h-2 rounded-full bg-green-500" />
            <span className="text-green-400 text-sm font-medium">Connection verified</span>
          </div>
        )}
      </div>

      {connectStatus === 'error' && connectError && (
        <ConnectErrorBanner
          errorCode={connectError}
          onDismiss={onDismissError}
        />
      )}

      {connectStatus === 'success' && (
        <ToolPreviewSection tools={discoveredTools} />
      )}

      {isStale && (
        <StaleConnectionBanner />
      )}
    </div>
  );
}

function computeIsDirty(
  serverName: string,
  transportType: McpServerTransportType,
  httpFields: McpServerHttpFields,
  stdioFields: McpServerStdioFields
): boolean {
  if (serverName !== '') return true;
  if (transportType === 'http') return httpFields.url !== '' || httpFields.apiKey !== '';
  return (
    stdioFields.command !== '' ||
    stdioFields.args.some((a) => a.trim() !== '') ||
    stdioFields.envVars.some((e) => e.key !== '' || e.value !== '')
  );
}

// ── Unsaved changes guard — isolated to contain useBlocker errors in data router envs ─

interface UnsavedChangesGuardProps {
  isDirty: boolean;
}

const UnsavedChangesGuardInner: React.FC<UnsavedChangesGuardProps> = ({ isDirty }) => {
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      isDirty && currentLocation.pathname !== nextLocation.pathname
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

const UnsavedChangesGuard: React.FC<UnsavedChangesGuardProps> = ({ isDirty }) => (
  <BlockerErrorBoundary>
    <UnsavedChangesGuardInner isDirty={isDirty} />
  </BlockerErrorBoundary>
);

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

export default CreateMcpServerPage;
