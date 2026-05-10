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
import ServerIdentitySection from './ServerIdentitySection';
import ConnectionDetailsSection from './ConnectionDetailsSection';
import FormFooter from './FormFooter';

interface McpServerFormProps {
  serverName: string;
  transportType: McpServerTransportType;
  httpFields: McpServerHttpFields;
  stdioFields: McpServerStdioFields;
  isConnectionVerified: boolean;
  onServerNameChange: (name: string) => void;
  onTransportChange: (transport: McpServerTransportType) => void;
  onHttpFieldsChange: (fields: McpServerHttpFields) => void;
  onStdioFieldsChange: (patch: Partial<McpServerStdioFields & { serverName: string }>) => void;
  onCancel: () => void;
  onSave: () => void;
  isSaving: boolean;
  connectSlot?: React.ReactNode;
  nameError?: string;
  httpErrors: HttpFieldErrors;
  httpTouched: HttpFieldTouched;
  stdioErrors: StdioFieldErrors;
  stdioTouched: StdioFieldTouched;
  isCheckingName: boolean;
  stdioIsCheckingName: boolean;
  isEditMode: boolean;
  onHttpBlur: (field: keyof HttpFieldTouched) => void;
  onStdioBlur: (field: keyof StdioFieldTouched | 'arg' | 'envKey', index?: number) => void;
  clearNameError: () => void;
  onApiKeyEditStateChange?: (state: ApiKeyEditState) => void;
  envVarEditStateMap?: EnvVarEditStateMap;
  onEnvVarEditStateChange?: (rowIndex: number, state: EnvVarValueEditState) => void;
}

const McpServerForm: React.FC<McpServerFormProps> = ({
  serverName, transportType, httpFields, stdioFields, isConnectionVerified,
  onServerNameChange, onTransportChange, onHttpFieldsChange, onStdioFieldsChange,
  onCancel, onSave, isSaving, connectSlot, nameError,
  httpErrors, httpTouched, stdioErrors, stdioTouched,
  isCheckingName, stdioIsCheckingName, isEditMode,
  onHttpBlur, onStdioBlur, clearNameError, onApiKeyEditStateChange,
  envVarEditStateMap, onEnvVarEditStateChange,
}) => (
  <form
    onSubmit={e => { e.preventDefault(); onSave(); }}
    className="bg-surface border border-border rounded-lg overflow-hidden"
  >
    <ServerIdentitySection
      serverName={serverName}
      nameError={nameError}
      onChange={onServerNameChange}
    />

    <ConnectionDetailsSection
      transportType={transportType}
      httpFields={httpFields}
      stdioFields={stdioFields}
      serverName={serverName}
      onTransportChange={onTransportChange}
      onHttpFieldsChange={onHttpFieldsChange}
      onStdioFieldsChange={onStdioFieldsChange}
      httpErrors={httpErrors}
      httpTouched={httpTouched}
      stdioErrors={stdioErrors}
      stdioTouched={stdioTouched}
      isCheckingName={isCheckingName}
      stdioIsCheckingName={stdioIsCheckingName}
      isEditMode={isEditMode}
      onServerNameChange={onServerNameChange}
      onHttpBlur={onHttpBlur}
      onStdioBlur={onStdioBlur}
      clearNameError={clearNameError}
      onApiKeyEditStateChange={onApiKeyEditStateChange}
      envVarEditStateMap={envVarEditStateMap}
      onEnvVarEditStateChange={onEnvVarEditStateChange}
      connectSlot={connectSlot}
    />

    {!isConnectionVerified && (
      <p className="px-7 py-2 text-xs text-amber-400/80 text-right">
        Please reconnect to verify the connection before saving.
      </p>
    )}

    <FormFooter
      onCancel={onCancel}
      onSave={onSave}
      isSaveDisabled={!isConnectionVerified}
      isSaving={isSaving}
    />
  </form>
);

export default McpServerForm;
