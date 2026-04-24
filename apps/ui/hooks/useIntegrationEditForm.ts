import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Integration, IntegrationType } from '../types';
import { getIntegrations, updateIntegration, testIntegrationConnection } from '../services/integrationService';
import { IntegrationFormState } from './useIntegrationForm';
import { validateFilterQuery } from '../utils/filterValidator';

const API_KEY_MASK = '••••••••••••';

const getFilterConfig = (provider: string) => {
  switch (provider) {
    case 'jira':
      return { label: 'JQL QUERY', placeholder: 'e.g. project = "WEB" AND status = "To Do"', hint: 'Limit which tickets are synced by providing a specific Jira Query Language string.' };
    case 'confluence':
      return { label: 'CQL QUERY', placeholder: 'e.g. type = "page" AND space = "ENG"', hint: 'Limit which pages are synced by providing a specific Confluence Query Language string.' };
    case 'github':
      return { label: 'SEARCH FILTER', placeholder: 'e.g. is:open label:bug state:open', hint: 'Limit which issues are synced by providing a GitHub search filter.' };
    case 'gitlab':
      return { label: 'SEARCH FILTER', placeholder: 'e.g. state:opened labels:bug', hint: 'Limit which issues are synced using GitLab search syntax.' };
    default:
      return { label: 'QUERY FILTER', placeholder: 'e.g. status:active type:task', hint: 'Limit which items are synced by providing a specific query string.' };
  }
};

const getAvailableTypesForProvider = (provider: string): IntegrationType[] => {
  switch (provider) {
    case 'jira': return [IntegrationType.TRACKER];
    case 'confluence': return [IntegrationType.KNOWLEDGE_BASE];
    case 'github':
    case 'gitlab': return [IntegrationType.TRACKER, IntegrationType.KNOWLEDGE_BASE, IntegrationType.CODE_SOURCE];
    default: return [IntegrationType.TRACKER];
  }
};

export interface UseIntegrationEditFormReturn {
  integration: Integration | null;
  formState: IntegrationFormState;
  setFormState: React.Dispatch<React.SetStateAction<IntegrationFormState>>;
  isLoading: boolean;
  loadError: string | null;
  isSaving: boolean;
  saveError: string | null;
  validationErrors: Record<string, string>;
  typesError: string | null;
  isTestingConnection: boolean;
  connectionTestError: string | null;
  connectionTestSuccess: boolean;
  testFailed: boolean;
  integrationsListPath: string;
  filterConfig: { label: string; placeholder: string; hint: string };
  availableTypes: IntegrationType[];
  showFilterField: boolean;
  showVectorizeToggle: boolean;
  handleTypeToggle: (type: IntegrationType) => void;
  handleSave: (e: React.FormEvent) => Promise<void>;
  handleCancel: () => void;
  handleTestConnection: () => Promise<void>;
}

export const useIntegrationEditForm = (
  workspaceId: string | undefined,
  integrationId: string | undefined
): UseIntegrationEditFormReturn => {
  const navigate = useNavigate();
  const integrationsListPath = `/workspaces/${workspaceId}/integrations`;

  const [integration, setIntegration] = useState<Integration | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [formState, setFormState] = useState<IntegrationFormState>({
    name: '',
    types: [IntegrationType.TRACKER],
    provider: 'jira',
    url: '',
    username: '',
    apiKey: API_KEY_MASK,
    filterQuery: '',
    vectorize: false,
  });

  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [typesError, setTypesError] = useState<string | null>(null);
  const [isTestingConnection, setIsTestingConnection] = useState(false);
  const [connectionTestError, setConnectionTestError] = useState<string | null>(null);
  const [connectionTestSuccess, setConnectionTestSuccess] = useState(false);
  const [testFailed, setTestFailed] = useState(false);

  useEffect(() => {
    if (!workspaceId || !integrationId) return;
    loadIntegration();
  }, [workspaceId, integrationId]);

  const loadIntegration = async () => {
    setIsLoading(true);
    setLoadError(null);
    try {
      const integrations = await getIntegrations(workspaceId!);
      const found = integrations.find(i => i.id === integrationId);
      if (!found) {
        setLoadError('Integration not found');
        return;
      }
      setIntegration(found);
      setFormState({
        name: found.name,
        types: Array.isArray(found.types) ? found.types : [],
        provider: (found.provider || 'custom').toLowerCase(),
        url: found.url || '',
        username: found.username || '',
        apiKey: API_KEY_MASK,
        filterQuery: found.filterQuery || '',
        vectorize: found.vectorize || false,
      });
    } catch {
      setLoadError('Failed to load integration');
    } finally {
      setIsLoading(false);
    }
  };

  const filterConfig = getFilterConfig(formState.provider);
  const availableTypes = getAvailableTypesForProvider(formState.provider);
  const showFilterField = formState.types.includes(IntegrationType.TRACKER) || formState.types.includes(IntegrationType.KNOWLEDGE_BASE);
  const showVectorizeToggle = formState.types.includes(IntegrationType.KNOWLEDGE_BASE);

  const validateForm = useCallback((): Record<string, string> => {
    const errors: Record<string, string> = {};
    const trimmedName = formState.name.trim();
    if (!trimmedName) {
      errors.name = 'Integration name is required.';
    } else if (trimmedName.length < 2 || trimmedName.length > 100) {
      errors.name = 'Name must be between 2 and 100 characters.';
    }
    if (!formState.url) {
      errors.url = 'Base URL is required.';
    }
    if (formState.filterQuery.trim()) {
      const filterResult = validateFilterQuery(formState.filterQuery, formState.provider);
      if (!filterResult.isValid && filterResult.error) {
        errors.filterQuery = filterResult.error;
      }
    }
    return errors;
  }, [formState]);

  const handleTypeToggle = (type: IntegrationType) => {
    setFormState(prev => {
      const hasType = prev.types.includes(type);
      const newTypes = hasType
        ? prev.types.filter(t => t !== type)
        : [...prev.types, type];
      return { ...prev, types: newTypes };
    });
    setTypesError(null);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();

    if (formState.types.length === 0) {
      setTypesError('At least one integration type must be selected.');
      return;
    }
    setTypesError(null);

    const errors = validateForm();
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return;
    }
    setValidationErrors({});

    setIsSaving(true);
    setSaveError(null);
    try {
      const connected = !testFailed;
      await updateIntegration(integrationId!, {
        ...formState,
        connected,
      });
      navigate(integrationsListPath);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to update integration';
      setSaveError(errorMessage);
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    navigate(integrationsListPath);
  };

  const handleTestConnection = async () => {
    setIsTestingConnection(true);
    setConnectionTestError(null);
    setConnectionTestSuccess(false);
    setTestFailed(false);
    try {
      await testIntegrationConnection({
        provider: formState.provider,
        url: formState.url,
        username: formState.username,
        apiKey: formState.apiKey,
      });
      setConnectionTestSuccess(true);
      setTestFailed(false);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to test connection';
      setConnectionTestError(errorMessage);
      setTestFailed(true);
    } finally {
      setIsTestingConnection(false);
    }
  };

  return {
    integration,
    formState,
    setFormState,
    isLoading,
    loadError,
    isSaving,
    saveError,
    validationErrors,
    typesError,
    isTestingConnection,
    connectionTestError,
    connectionTestSuccess,
    testFailed,
    integrationsListPath,
    filterConfig,
    availableTypes,
    showFilterField,
    showVectorizeToggle,
    handleTypeToggle,
    handleSave,
    handleCancel,
    handleTestConnection,
  };
};
