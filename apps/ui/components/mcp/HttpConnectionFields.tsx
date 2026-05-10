import React from 'react';
import { McpServerHttpFields, HttpFieldErrors, HttpFieldTouched, ApiKeyEditState } from '../../types';
import ServerNameInput from './ServerNameInput';
import EndpointUrlInput from './EndpointUrlInput';
import AuthTypeToggle from './AuthTypeToggle';
import ApiKeyInput from './ApiKeyInput';

interface HttpConnectionFieldsProps {
  fields: McpServerHttpFields & { serverName: string };
  errors: HttpFieldErrors;
  touched: HttpFieldTouched;
  isCheckingName: boolean;
  isEditMode: boolean;
  isDisabled?: boolean;
  onChange: (patch: Partial<McpServerHttpFields & { serverName: string }>) => void;
  onBlur: (field: keyof HttpFieldTouched) => void;
  clearNameError: () => void;
  onApiKeyEditStateChange?: (state: ApiKeyEditState) => void;
}

const HttpConnectionFields: React.FC<HttpConnectionFieldsProps> = ({
  fields, errors, touched, isCheckingName, isEditMode, isDisabled,
  onChange, onBlur, clearNameError, onApiKeyEditStateChange,
}) => (
  <div className="space-y-4">
    <ServerNameInput
      value={fields.serverName}
      error={errors.serverName}
      touched={touched.serverName}
      isChecking={isCheckingName}
      isDisabled={isDisabled}
      onChange={onChange}
      onBlur={() => onBlur('serverName')}
      clearNameError={clearNameError}
    />
    <EndpointUrlInput
      value={fields.url}
      error={errors.url}
      touched={touched.url}
      isDisabled={isDisabled}
      onChange={onChange}
      onBlur={() => onBlur('url')}
    />
    <AuthTypeToggle
      value={fields.authType}
      isDisabled={isDisabled}
      onChange={onChange}
    />
    {fields.authType === 'api_key' && (
      <div className="overflow-hidden transition-all duration-200">
        <ApiKeyInput
          value={fields.apiKey}
          error={errors.apiKey}
          touched={touched.apiKey}
          isEditMode={isEditMode}
          isDisabled={isDisabled}
          onChange={onChange}
          onBlur={() => onBlur('apiKey')}
          onEditStateChange={onApiKeyEditStateChange}
        />
      </div>
    )}
  </div>
);

export default HttpConnectionFields;
