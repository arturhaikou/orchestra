import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { AiCliProviderType } from '../types';
import { getCliIntegration, createCliIntegration, updateCliIntegration, discoverCopilotModels } from '../services/cliIntegrationService';

interface FormState {
  name: string;
  provider: AiCliProviderType;
  credential: string;
  useLoggedInUser: boolean;
  workingDirectory: string;
  modelId: string;
  cliPath: string;
}

interface ValidationErrors {
  name?: string;
  credential?: string;
  workingDirectory?: string;
  cliPath?: string;
}

interface UseCliIntegrationFormResult {
  formState: FormState;
  setField: <K extends keyof FormState>(field: K, value: FormState[K]) => void;
  isLoading: boolean;
  isSaving: boolean;
  saveError: string | null;
  validationErrors: ValidationErrors;
  handleSave: () => Promise<void>;
  handleCancel: () => void;
  isEditMode: boolean;
  availableModels: string[];
  isLoadingModels: boolean;
  modelDiscoveryError: string | null;
  loadModels: () => Promise<void>;
}

const DEFAULT_FORM_STATE: FormState = {
  name: '',
  provider: AiCliProviderType.GITHUB_COPILOT,
  credential: '',
  useLoggedInUser: false,
  workingDirectory: '',
  modelId: '',
  cliPath: '',
};

const validate = (state: FormState, isEditMode: boolean): ValidationErrors => {
  const errors: ValidationErrors = {};
  if (!state.name.trim()) errors.name = 'Name is required.';
  if (!state.workingDirectory.trim()) errors.workingDirectory = 'Working directory is required.';
  if (!state.cliPath.trim()) errors.cliPath = 'CLI path is required.';
  if (!state.useLoggedInUser && !state.credential.trim() && !isEditMode) {
    errors.credential = 'Token is required when not using logged-in user.';
  }
  return errors;
};

export const useCliIntegrationForm = (
  workspaceId: string,
  integrationId?: string,
): UseCliIntegrationFormResult => {
  const navigate = useNavigate();
  const isEditMode = !!integrationId;

  const [formState, setFormState] = useState<FormState>(DEFAULT_FORM_STATE);
  const [isLoading, setIsLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<ValidationErrors>({});
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [isLoadingModels, setIsLoadingModels] = useState(false);
  const [modelDiscoveryError, setModelDiscoveryError] = useState<string | null>(null);

  useEffect(() => {
    if (!integrationId || !workspaceId) return;
    setIsLoading(true);
    getCliIntegration(workspaceId, integrationId)
      .then(integration => {
        setFormState({
          name: integration.name,
          provider: integration.provider,
          credential: '',
          useLoggedInUser: integration.useLoggedInUser,
          workingDirectory: integration.workingDirectory,
          modelId: integration.modelId ?? '',
          cliPath: integration.cliPath ?? '',
        });
      })
      .catch(() => setSaveError('Failed to load integration.'))
      .finally(() => setIsLoading(false));
  }, [integrationId, workspaceId]);

  const setField = useCallback(<K extends keyof FormState>(field: K, value: FormState[K]) => {
    setFormState(prev => ({ ...prev, [field]: value }));
    setValidationErrors(prev => ({ ...prev, [field]: undefined }));
  }, []);

  const loadModels = useCallback(async () => {
    if (!formState.workingDirectory.trim()) {
      setModelDiscoveryError('Working directory is required to load models.');
      return;
    }
    if (!formState.useLoggedInUser && !formState.credential.trim() && !isEditMode) {
      setModelDiscoveryError('Enter your GitHub token first to load models.');
      return;
    }

    setIsLoadingModels(true);
    setModelDiscoveryError(null);

    try {
      const models = await discoverCopilotModels(
        formState.credential.trim() || null,
        formState.useLoggedInUser,
        formState.workingDirectory.trim(),
        formState.cliPath.trim() || null,
      );
      setAvailableModels(models);
    } catch (err) {
      setModelDiscoveryError(err instanceof Error ? err.message : 'Failed to load models.');
      setAvailableModels([]);
    } finally {
      setIsLoadingModels(false);
    }
  }, [formState.credential, formState.useLoggedInUser, formState.workingDirectory, formState.cliPath, isEditMode]);

  const listPath = `/workspaces/${workspaceId}/cli-integrations`;

  const handleSave = useCallback(async () => {
    const errors = validate(formState, isEditMode);
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return;
    }

    setIsSaving(true);
    setSaveError(null);

    try {
      if (isEditMode && integrationId) {
        await updateCliIntegration(integrationId, {
          workspaceId,
          name: formState.name.trim(),
          credential: formState.credential.trim() || undefined,
          useLoggedInUser: formState.useLoggedInUser,
          workingDirectory: formState.workingDirectory.trim(),
          modelId: formState.modelId.trim() || null,
          cliPath: formState.cliPath.trim() || null,
        });
      } else {
        await createCliIntegration({
          workspaceId,
          name: formState.name.trim(),
          provider: formState.provider,
          credential: formState.credential.trim() || undefined,
          useLoggedInUser: formState.useLoggedInUser,
          workingDirectory: formState.workingDirectory.trim(),
          modelId: formState.modelId.trim() || null,
          cliPath: formState.cliPath.trim() || null,
        });
      }
      navigate(listPath);
    } catch (err) {
      setSaveError(err instanceof Error ? err.message : 'An unexpected error occurred.');
    } finally {
      setIsSaving(false);
    }
  }, [formState, isEditMode, integrationId, workspaceId, listPath, navigate]);

  const handleCancel = useCallback(() => {
    navigate(listPath);
  }, [navigate, listPath]);

  return {
    formState,
    setField,
    isLoading,
    isSaving,
    saveError,
    validationErrors,
    handleSave,
    handleCancel,
    isEditMode,
    availableModels,
    isLoadingModels,
    modelDiscoveryError,
    loadModels,
  };
};

