import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { IntegrationType } from '../types';
import { createIntegration, testIntegrationConnection } from '../services/integrationService';
import { validateFilterQuery } from '../utils/filterValidator';

export interface IntegrationFormState {
  name: string;
  types: IntegrationType[];
  provider: string;
  url: string;
  username: string;
  apiKey: string;
  filterQuery: string;
  vectorize: boolean;
}

export interface UseIntegrationFormReturn {
  formState: IntegrationFormState;
  setFormState: React.Dispatch<React.SetStateAction<IntegrationFormState>>;
  isSaving: boolean;
  saveError: string | null;
  validationErrors: Record<string, string>;
  typesError: string | null;
  isFilterWarningOpen: boolean;
  setIsFilterWarningOpen: React.Dispatch<React.SetStateAction<boolean>>;
  isTestingConnection: boolean;
  connectionTestError: string | null;
  connectionTestSuccess: boolean;
  testFailed: boolean;
  integrationsListPath: string;
  filterConfig: { label: string; placeholder: string; hint: string };
  availableTypes: IntegrationType[];
  showFilterField: boolean;
  showVectorizeToggle: boolean;
  validateForm: () => Record<string, string>;
  handleProviderChange: (provider: string) => void;
  handleTypeToggle: (type: IntegrationType) => void;
  handleSave: (e: React.FormEvent) => Promise<void>;
  performSave: () => Promise<void>;
  handleCancel: () => void;
  handleTestConnection: () => Promise<void>;
}

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

const getDefaultTypesForProvider = (provider: string): IntegrationType[] => {
  switch (provider) {
    case 'jira': return [IntegrationType.TRACKER];
    case 'confluence': return [IntegrationType.KNOWLEDGE_BASE];
    case 'github':
    case 'gitlab': return [IntegrationType.TRACKER, IntegrationType.CODE_SOURCE];
    default: return [IntegrationType.TRACKER];
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

export const useIntegrationForm = (workspaceId: string | undefined): UseIntegrationFormReturn => {
  const navigate = useNavigate();

  const [formState, setFormState] = useState<IntegrationFormState>({
    name: '',
    types: [IntegrationType.TRACKER],
    provider: 'jira',
    url: '',
    username: '',
    apiKey: '',
    filterQuery: '',
    vectorize: false,
  });

  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [typesError, setTypesError] = useState<string | null>(null);
  const [isFilterWarningOpen, setIsFilterWarningOpen] = useState(false);
  const [isTestingConnection, setIsTestingConnection] = useState(false);
  const [connectionTestError, setConnectionTestError] = useState<string | null>(null);
  const [connectionTestSuccess, setConnectionTestSuccess] = useState(false);
  const [testFailed, setTestFailed] = useState(false);

  const integrationsListPath = `/workspaces/${workspaceId}/integrations`;
  const filterConfig = getFilterConfig(formState.provider);
  const availableTypes = getAvailableTypesForProvider(formState.provider);
  const showFilterField = formState.types.includes(IntegrationType.TRACKER) || formState.types.includes(IntegrationType.KNOWLEDGE_BASE);
  const showVectorizeToggle = formState.types.includes(IntegrationType.KNOWLEDGE_BASE);

  const validateForm = (): Record<string, string> => {
    const errors: Record<string, string> = {};
    const trimmedName = formState.name.trim();
    if (!trimmedName) {
      errors.name = 'Integration name is required.';
    } else if (trimmedName.length < 2 || trimmedName.length > 100) {
      errors.name = 'Name must be between 2 and 100 characters.';
    }
    if (!formState.apiKey) {
      errors.apiKey = 'API key is required.';
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
  };

  const shouldShowFilterWarning = (): boolean => {
    const needsFilter = formState.provider === 'jira' || formState.provider === 'confluence';
    return needsFilter && !formState.filterQuery.trim();
  };

  const handleProviderChange = (provider: string) => {
    setFormState(prev => ({
      ...prev,
      provider,
      types: getDefaultTypesForProvider(provider),
      filterQuery: '',
    }));
    setConnectionTestError(null);
    setConnectionTestSuccess(false);
    setTestFailed(false);
  };

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

  const performSave = async () => {
    setIsSaving(true);
    setSaveError(null);
    try {
      const connected = !testFailed;
      await createIntegration({ ...formState, workspaceId: workspaceId!, connected });
      navigate(integrationsListPath);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to save integration';
      setSaveError(errorMessage);
    } finally {
      setIsSaving(false);
    }
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

    if (shouldShowFilterWarning()) {
      setIsFilterWarningOpen(true);
      return;
    }

    await performSave();
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
    formState,
    setFormState,
    isSaving,
    saveError,
    validationErrors,
    typesError,
    isFilterWarningOpen,
    setIsFilterWarningOpen,
    isTestingConnection,
    connectionTestError,
    connectionTestSuccess,
    testFailed,
    integrationsListPath,
    filterConfig,
    availableTypes,
    showFilterField,
    showVectorizeToggle,
    validateForm,
    handleProviderChange,
    handleTypeToggle,
    handleSave,
    performSave,
    handleCancel,
    handleTestConnection,
  };
};
