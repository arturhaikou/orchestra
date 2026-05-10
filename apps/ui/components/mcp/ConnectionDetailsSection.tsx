import React from 'react';
import {
  McpServerTransportType,
  McpServerHttpFields,
  McpServerStdioFields,
  HttpFieldErrors,
  HttpFieldTouched,
  ApiKeyEditState,
  StdioFieldErrors,
  StdioFieldTouched,
  EnvVarValueEditState,
  EnvVarEditStateMap,
} from '../../types';
import TransportTypeSelector from './TransportTypeSelector';
import HttpConnectionFields from './HttpConnectionFields';
import StdioConnectionFields from './StdioConnectionFields';

interface ConnectionDetailsSectionProps {
  transportType: McpServerTransportType;
  httpFields: McpServerHttpFields;
  stdioFields: McpServerStdioFields;
  serverName: string;
  onTransportChange: (transport: McpServerTransportType) => void;
  onHttpFieldsChange: (fields: McpServerHttpFields) => void;
  onStdioFieldsChange: (patch: Partial<McpServerStdioFields & { serverName: string }>) => void;
  httpErrors: HttpFieldErrors;
  httpTouched: HttpFieldTouched;
  stdioErrors: StdioFieldErrors;
  stdioTouched: StdioFieldTouched;
  isCheckingName: boolean;
  stdioIsCheckingName: boolean;
  isEditMode: boolean;
  onServerNameChange?: (name: string) => void;
  onHttpBlur: (field: keyof HttpFieldTouched) => void;
  onStdioBlur: (field: keyof StdioFieldTouched | 'arg' | 'envKey', index?: number) => void;
  clearNameError: () => void;
  onApiKeyEditStateChange?: (state: ApiKeyEditState) => void;
  envVarEditStateMap?: EnvVarEditStateMap;
  onEnvVarEditStateChange?: (rowIndex: number, state: EnvVarValueEditState) => void;
  isDisabled?: boolean;
  connectSlot?: React.ReactNode;
}

const ConnectionDetailsSection: React.FC<ConnectionDetailsSectionProps> = ({
  transportType, httpFields, stdioFields, serverName,
  onTransportChange, onHttpFieldsChange, onStdioFieldsChange,
  httpErrors, httpTouched, stdioErrors, stdioTouched,
  isCheckingName, stdioIsCheckingName, isEditMode,
  onServerNameChange, onHttpBlur, onStdioBlur, clearNameError,
  onApiKeyEditStateChange, envVarEditStateMap, onEnvVarEditStateChange,
  isDisabled, connectSlot,
}) => (
  <div className="px-7 py-6 border-t border-border">
    <h2 className="text-xs font-semibold text-textMuted uppercase tracking-wider mb-5">
      Connection Details
    </h2>

    <TransportTypeSelector
      value={transportType}
      onChange={onTransportChange}
      isDisabled={isDisabled}
    />

    {transportType === 'http' && (
      <HttpConnectionFields
        fields={{ ...httpFields, serverName }}
        errors={httpErrors}
        touched={httpTouched}
        isCheckingName={isCheckingName}
        isEditMode={isEditMode}
        isDisabled={isDisabled}
        onChange={patch => {
          if ('serverName' in patch && onServerNameChange)
            onServerNameChange(patch.serverName as string);
          onHttpFieldsChange({ ...httpFields, ...patch } as McpServerHttpFields);
        }}
        onBlur={onHttpBlur}
        clearNameError={clearNameError}
        onApiKeyEditStateChange={onApiKeyEditStateChange}
      />
    )}

    {transportType === 'stdio' && (
      <StdioConnectionFields
        fields={{ ...stdioFields, serverName }}
        errors={stdioErrors}
        touched={stdioTouched}
        isCheckingName={stdioIsCheckingName}
        isEditMode={isEditMode}
        isDisabled={isDisabled}
        envVarEditStateMap={envVarEditStateMap}
        onChange={patch => {
          if ('serverName' in patch && onServerNameChange)
            onServerNameChange(patch.serverName as string);
          onStdioFieldsChange(patch);
        }}
        onBlur={onStdioBlur}
        onEnvVarEditStateChange={onEnvVarEditStateChange}
      />
    )}

    {connectSlot}
  </div>
);

export default ConnectionDetailsSection;
