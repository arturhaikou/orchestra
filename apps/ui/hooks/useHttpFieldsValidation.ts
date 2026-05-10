import { Dispatch, SetStateAction, useMemo, useState } from 'react';
import {
  ApiKeyEditState,
  HttpFieldErrors,
  HttpFieldTouched,
  McpServerHttpFields,
} from '../types';
import { checkMcpServerNameUnique } from '../services/mcpServerService';

export interface UseHttpFieldsValidationReturn {
  errors: HttpFieldErrors;
  touched: HttpFieldTouched;
  isCheckingName: boolean;
  isValid: boolean;
  handleBlur: (field: keyof HttpFieldTouched) => Promise<void>;
  validateAllForConnect: () => Promise<boolean>;
  clearNameError: () => void;
}

export function useHttpFieldsValidation(
  fields: McpServerHttpFields & { serverName: string },
  workspaceId: string,
  isEditMode: boolean,
  excludeId?: string,
  apiKeyEditState?: ApiKeyEditState
): UseHttpFieldsValidationReturn {
  const [errors, setErrors]               = useState<HttpFieldErrors>({});
  const [touched, setTouched]             = useState<HttpFieldTouched>({
    serverName: false,
    url: false,
    apiKey: false,
  });
  const [isCheckingName, setIsCheckingName] = useState(false);

  const isValid = useMemo<boolean>(() => {
    const nameOk   = !errors.serverName && fields.serverName.trim().length >= 2;
    const urlOk    = !errors.url && fields.url.trim().length > 0;
    const apiKeyOk = resolveApiKeyOk(fields, isEditMode, apiKeyEditState, errors.apiKey);
    return nameOk && urlOk && apiKeyOk && !isCheckingName;
  }, [errors, fields, isEditMode, apiKeyEditState, isCheckingName]);

  const handleBlur = async (field: keyof HttpFieldTouched): Promise<void> => {
    setTouched(prev => ({ ...prev, [field]: true }));
    if (field === 'serverName') await handleNameBlur();
    if (field === 'url')        handleUrlBlur();
  };

  const handleNameBlur = async (): Promise<void> => {
    const localError = validateNameLocally(fields.serverName);
    if (localError) {
      setErrors(prev => ({ ...prev, serverName: localError }));
      return;
    }
    setErrors(prev => ({ ...prev, serverName: undefined }));
    await checkNameUniqueness(fields.serverName);
  };

  const handleUrlBlur = (): void => {
    const error = validateUrl(fields.url);
    setErrors(prev => ({ ...prev, url: error }));
  };

  const checkNameUniqueness = async (name: string): Promise<void> => {
    setIsCheckingName(true);
    try {
      const { isUnique } = await checkMcpServerNameUnique(workspaceId, name.trim(), excludeId);
      if (!isUnique) {
        setErrors(prev => ({ ...prev, serverName: 'A server with this name already exists.' }));
      }
    } catch {
      // Network errors silently ignored — Connect attempt will re-validate.
    } finally {
      setIsCheckingName(false);
    }
  };

  const validateAllForConnect = async (): Promise<boolean> => {
    setTouched({ serverName: true, url: true, apiKey: true });

    const hasLocalErrors = applyLocalValidationErrors(fields, isEditMode, apiKeyEditState, setErrors);
    if (hasLocalErrors) return false;

    setIsCheckingName(true);
    let nameUniqueError: string | undefined;
    try {
      const { isUnique } = await checkMcpServerNameUnique(
        workspaceId, fields.serverName.trim(), excludeId
      );
      if (!isUnique) nameUniqueError = 'A server with this name already exists.';
    } catch {
      // Network error — allow the Connect attempt to surface the problem server-side.
    } finally {
      setIsCheckingName(false);
    }

    if (nameUniqueError) {
      setErrors(prev => ({ ...prev, serverName: nameUniqueError }));
      return false;
    }
    return true;
  };

  const clearNameError = (): void =>
    setErrors(prev => ({ ...prev, serverName: undefined }));

  return { errors, touched, isCheckingName, isValid, handleBlur, validateAllForConnect, clearNameError };
}

function applyLocalValidationErrors(
  fields: McpServerHttpFields & { serverName: string },
  isEditMode: boolean,
  apiKeyEditState: ApiKeyEditState | undefined,
  setErrors: Dispatch<SetStateAction<HttpFieldErrors>>
): boolean {
  const nameLocalError = validateNameLocally(fields.serverName);
  const urlError       = validateUrl(fields.url);
  const apiKeyError    = resolveApiKeyRequiredError(fields, isEditMode, apiKeyEditState);

  if (!nameLocalError && !urlError && !apiKeyError) return false;

  setErrors({ serverName: nameLocalError, url: urlError, apiKey: apiKeyError });
  return true;
}

export function validateNameLocally(name: string): string | undefined {
  const trimmed = name.trim();
  if (trimmed.length === 0)   return 'Server name is required.';
  if (trimmed.length < 2)     return 'Server name must be at least 2 characters.';
  if (trimmed.length > 100)   return 'Server name cannot exceed 100 characters.';
  return undefined;
}

export function validateUrl(url: string): string | undefined {
  const trimmed = url.trim();
  if (trimmed.length === 0)   return 'Endpoint URL is required.';
  if (trimmed.length > 2048)  return 'URL is too long (max 2048 characters).';
  try {
    const parsed = new URL(trimmed);
    if (parsed.protocol !== 'https:') return 'URL must use HTTPS.';
  } catch {
    return 'Please enter a valid URL.';
  }
  return undefined;
}

export function resolveApiKeyOk(
  fields: McpServerHttpFields & { serverName: string },
  isEditMode: boolean,
  apiKeyEditState: ApiKeyEditState | undefined,
  apiKeyError: string | undefined
): boolean {
  if (fields.authType !== 'api_key') return true;
  if (apiKeyError) return false;
  if (isEditMode && apiKeyEditState === 'masked') return true;
  return fields.apiKey.trim().length > 0;
}

export function resolveApiKeyRequiredError(
  fields: McpServerHttpFields & { serverName: string },
  isEditMode: boolean,
  apiKeyEditState: ApiKeyEditState | undefined
): string | undefined {
  if (fields.authType !== 'api_key') return undefined;
  if (isEditMode && apiKeyEditState === 'masked') return undefined;
  if (fields.apiKey.trim().length === 0) {
    return 'API Key is required when using API Key authentication.';
  }
  return undefined;
}
