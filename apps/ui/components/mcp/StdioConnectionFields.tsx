import React from 'react';
import {
  McpServerStdioFields,
  StdioFieldErrors,
  StdioFieldTouched,
  EnvVarValueEditState,
  EnvVarEditStateMap,
} from '../../types';
import ServerNameInput from './ServerNameInput';
import CommandInput from './CommandInput';
import ArgListEditor from './ArgListEditor';
import EnvVarEditor from './EnvVarEditor';

interface StdioConnectionFieldsProps {
  fields: McpServerStdioFields & { serverName: string };
  errors: StdioFieldErrors;
  touched: StdioFieldTouched;
  onChange: (patch: Partial<McpServerStdioFields & { serverName: string }>) => void;
  onBlur: (field: keyof StdioFieldTouched | 'arg' | 'envKey', index?: number) => void;
  isEditMode: boolean;
  envVarEditStateMap?: EnvVarEditStateMap;
  onEnvVarEditStateChange?: (rowIndex: number, state: EnvVarValueEditState) => void;
  isCheckingName: boolean;
  isDisabled?: boolean;
}

const StdioConnectionFields: React.FC<StdioConnectionFieldsProps> = ({
  fields, errors, touched, onChange, onBlur, isEditMode,
  envVarEditStateMap, onEnvVarEditStateChange, isCheckingName, isDisabled,
}) => (
  <div className="space-y-4 mt-4">
    <ServerNameInput
      value={fields.serverName}
      error={errors.serverName}
      touched={touched.serverName}
      isChecking={isCheckingName}
      isDisabled={isDisabled}
      onChange={patch => onChange(patch as Partial<McpServerStdioFields & { serverName: string }>)}
      onBlur={() => onBlur('serverName')}
      clearNameError={() => onChange({ serverName: fields.serverName })}
    />

    <CommandInput
      value={fields.command}
      error={errors.command}
      isTouched={touched.command}
      isDisabled={isDisabled}
      onChange={value => onChange({ command: value })}
      onBlur={() => onBlur('command')}
    />

    <ArgListEditor
      args={fields.args}
      argErrors={errors.argErrors ?? {}}
      argTouched={touched.argTouched}
      isDisabled={isDisabled}
      onChange={args => onChange({ args })}
      onArgBlur={index => onBlur('arg', index)}
    />

    <EnvVarEditor
      envVars={fields.envVars}
      envKeyErrors={errors.envKeyErrors ?? {}}
      envKeyTouched={touched.envKeyTouched}
      envTotalSizeError={errors.envTotalSize}
      isEditMode={isEditMode}
      envVarEditStateMap={envVarEditStateMap}
      isDisabled={isDisabled}
      onChange={envVars => onChange({ envVars })}
      onKeyBlur={index => onBlur('envKey', index)}
      onValueEditStateChange={onEnvVarEditStateChange}
    />
  </div>
);

export default StdioConnectionFields;
