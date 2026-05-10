import { useState, useMemo } from 'react';
import {
  McpServerStdioFields,
  StdioFieldErrors,
  StdioFieldTouched,
  EnvVar,
} from '../types';
import { checkMcpServerNameUnique } from '../services/mcpServerService';

const SHELL_OPERATOR_REGEX = /[&|><;`]|\$\(/;
const MAX_COMMAND_LENGTH = 500;
const MAX_ARG_LENGTH = 1000;
const MAX_ENV_TOTAL_SIZE = 4096;
const MIN_NAME_LENGTH = 2;
const MAX_NAME_LENGTH = 100;

export function validateCommand(command: string): string | undefined {
  const trimmed = command.trim();
  if (!trimmed) return 'Command is required.';
  if (trimmed.length > MAX_COMMAND_LENGTH) return 'Command must not exceed 500 characters.';
  if (SHELL_OPERATOR_REGEX.test(trimmed))
    return 'Shell operators are not allowed. Enter a single executable name.';
  return undefined;
}

export function validateArgument(arg: string): string | undefined {
  if (arg.length > MAX_ARG_LENGTH)
    return 'Argument exceeds the maximum length of 1,000 characters.';
  return undefined;
}

export function validateEnvKey(
  key: string,
  allKeys: string[],
  rowIndex: number
): string | undefined {
  if (!key) return 'Key is required.';
  if (!/^[A-Za-z0-9_]+$/.test(key))
    return 'Key must contain only letters, numbers, and underscores.';
  if (isDuplicateKey(key, allKeys, rowIndex))
    return "Key must be unique within this server's environment variables.";
  return undefined;
}

export function calculateEnvTotalSize(envVars: EnvVar[]): number {
  return envVars.reduce((sum, { key, value }) => sum + key.length + value.length, 0);
}

export function isStdioFormValid(
  fields: McpServerStdioFields & { serverName: string },
  errors: StdioFieldErrors,
  isCheckingName: boolean
): boolean {
  if (isCheckingName) return false;
  if (errors.serverName || errors.command || errors.envTotalSize) return false;
  if (hasIndexedErrors(errors.argErrors)) return false;
  if (hasIndexedErrors(errors.envKeyErrors)) return false;
  return fields.serverName.trim().length >= MIN_NAME_LENGTH && fields.command.trim().length > 0;
}

function isDuplicateKey(key: string, allKeys: string[], rowIndex: number): boolean {
  return allKeys.filter((k, i) => i !== rowIndex && k === key).length > 0;
}

function hasIndexedErrors(record: Record<number, string> | undefined): boolean {
  return record !== undefined && Object.keys(record).length > 0;
}

function validateNameLocally(name: string): string | undefined {
  if (!name.trim()) return 'Server name is required.';
  if (name.trim().length < MIN_NAME_LENGTH)
    return 'Server name must be at least 2 characters.';
  if (name.trim().length > MAX_NAME_LENGTH)
    return 'Server name must not exceed 100 characters.';
  return undefined;
}

export function reIndexMap<T>(
  map: Record<number, T>,
  removedIndex: number
): Record<number, T> {
  const result: Record<number, T> = {};
  Object.entries(map).forEach(([key, val]) => {
    const i = Number(key);
    if (i < removedIndex) result[i] = val;
    else if (i > removedIndex) result[i - 1] = val;
  });
  return result;
}

export function swapMapEntries<T>(
  map: Record<number, T>,
  indexA: number,
  indexB: number
): Record<number, T> {
  const result = { ...map };
  const tmp = result[indexA];
  result[indexA] = result[indexB];
  result[indexB] = tmp;
  return result;
}

export interface UseStdioFieldsValidationReturn {
  errors: StdioFieldErrors;
  touched: StdioFieldTouched;
  isCheckingName: boolean;
  isValid: boolean;
  handleBlur: (
    field: keyof StdioFieldTouched | 'arg' | 'envKey',
    index?: number
  ) => Promise<void>;
  touchAll: () => void;
}

export function useStdioFieldsValidation(
  fields: McpServerStdioFields & { serverName: string },
  options?: {
    isEditMode?: boolean;
    existingServerId?: string;
    workspaceId: string;
  }
): UseStdioFieldsValidationReturn {
  const [errors, setErrors] = useState<StdioFieldErrors>({});
  const [touched, setTouched] = useState<StdioFieldTouched>({
    serverName: false,
    command: false,
    argTouched: {},
    envKeyTouched: {},
  });
  const [isCheckingName, setIsCheckingName] = useState(false);

  const isValid = useMemo(
    () => isStdioFormValid(fields, errors, isCheckingName),
    [fields, errors, isCheckingName]
  );

  const handleBlur = async (
    field: keyof StdioFieldTouched | 'arg' | 'envKey',
    index?: number
  ): Promise<void> => {
    if (field === 'serverName') await handleServerNameBlur();
    if (field === 'command') handleCommandBlur();
    if (field === 'arg' && index !== undefined) handleArgBlur(index);
    if (field === 'envKey' && index !== undefined) handleEnvKeyBlur(index);
  };

  const touchAll = (): void => {
    const argTouched = buildAllTouchedMap(fields.args.length);
    const envKeyTouched = buildAllTouchedMap(fields.envVars.length);
    setTouched({ serverName: true, command: true, argTouched, envKeyTouched });
    applyAllValidationErrors();
  };

  const handleServerNameBlur = async (): Promise<void> => {
    setTouched(prev => ({ ...prev, serverName: true }));
    const localError = validateNameLocally(fields.serverName);
    if (localError) {
      setErrors(prev => ({ ...prev, serverName: localError }));
      return;
    }
    setErrors(prev => ({ ...prev, serverName: undefined }));
    await checkNameUniqueness(fields.serverName);
  };

  const handleCommandBlur = (): void => {
    setTouched(prev => ({ ...prev, command: true }));
    const error = validateCommand(fields.command);
    setErrors(prev => ({ ...prev, command: error }));
  };

  const handleArgBlur = (index: number): void => {
    setTouched(prev => ({
      ...prev,
      argTouched: { ...prev.argTouched, [index]: true },
    }));
    const error = validateArgument(fields.args[index] ?? '');
    setErrors(prev => {
      const newArgErrors = { ...(prev.argErrors ?? {}) };
      if (error) {
        newArgErrors[index] = error;
      } else {
        delete newArgErrors[index];
      }
      return { ...prev, argErrors: newArgErrors };
    });
  };

  const handleEnvKeyBlur = (index: number): void => {
    setTouched(prev => ({
      ...prev,
      envKeyTouched: { ...prev.envKeyTouched, [index]: true },
    }));
    const allKeys = fields.envVars.map(ev => ev.key);
    const error = validateEnvKey(fields.envVars[index]?.key ?? '', allKeys, index);
    const totalSize = calculateEnvTotalSize(fields.envVars);
    const sizeError =
      totalSize > MAX_ENV_TOTAL_SIZE
        ? 'Environment variables exceed the maximum allowed size.'
        : undefined;
    setErrors(prev => {
      const newEnvKeyErrors = { ...(prev.envKeyErrors ?? {}) };
      if (error) {
        newEnvKeyErrors[index] = error;
      } else {
        delete newEnvKeyErrors[index];
      }
      return {
        ...prev,
        envKeyErrors: newEnvKeyErrors,
        envTotalSize: sizeError,
      };
    });
  };

  const checkNameUniqueness = async (name: string): Promise<void> => {
    setIsCheckingName(true);
    try {
      const { isUnique } = await checkMcpServerNameUnique(
        options?.workspaceId ?? '',
        name.trim(),
        options?.existingServerId
      );
      if (!isUnique)
        setErrors(prev => ({ ...prev, serverName: 'A server with this name already exists.' }));
    } catch {
    } finally {
      setIsCheckingName(false);
    }
  };

  const applyAllValidationErrors = (): void => {
    const commandError = validateCommand(fields.command);
    const argErrors = buildArgErrors(fields.args);
    const envKeyErrors = buildEnvKeyErrors(fields.envVars);
    const envSize = calculateEnvTotalSize(fields.envVars);
    setErrors(prev => ({
      ...prev,
      command: commandError,
      argErrors,
      envKeyErrors,
      envTotalSize:
        envSize > MAX_ENV_TOTAL_SIZE
          ? 'Environment variables exceed the maximum allowed size.'
          : undefined,
    }));
  };

  return { errors, touched, isCheckingName, isValid, handleBlur, touchAll };
}

function buildAllTouchedMap(length: number): Record<number, boolean> {
  const map: Record<number, boolean> = {};
  for (let i = 0; i < length; i++) map[i] = true;
  return map;
}

function buildArgErrors(args: string[]): Record<number, string> {
  const errors: Record<number, string> = {};
  args.forEach((arg, i) => {
    const err = validateArgument(arg);
    if (err) errors[i] = err;
  });
  return errors;
}

function buildEnvKeyErrors(envVars: EnvVar[]): Record<number, string> {
  const errors: Record<number, string> = {};
  const allKeys = envVars.map(ev => ev.key);
  envVars.forEach((ev, i) => {
    const err = validateEnvKey(ev.key, allKeys, i);
    if (err) errors[i] = err;
  });
  return errors;
}
