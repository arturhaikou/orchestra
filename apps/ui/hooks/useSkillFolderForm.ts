import { useState, useCallback } from 'react';

export interface SkillFolderFormState {
  name: string;
  folderPath: string;
}

const initialState: SkillFolderFormState = {
  name: '',
  folderPath: '',
};

interface UseSkillFolderFormResult {
  formState: SkillFolderFormState;
  setFormState: React.Dispatch<React.SetStateAction<SkillFolderFormState>>;
  validationErrors: Record<string, string>;
  setValidationErrors: React.Dispatch<React.SetStateAction<Record<string, string>>>;
  clearFieldError: (field: string) => void;
  validateForm: () => Record<string, string>;
  resetForm: () => void;
}

export const useSkillFolderForm = (initial?: Partial<SkillFolderFormState>): UseSkillFolderFormResult => {
  const [formState, setFormState] = useState<SkillFolderFormState>({
    ...initialState,
    ...initial,
  });
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  const clearFieldError = useCallback((field: string) => {
    setValidationErrors(prev => {
      const next = { ...prev };
      delete next[field];
      return next;
    });
  }, []);

  const validateForm = useCallback((): Record<string, string> => {
    const errors: Record<string, string> = {};
    if (!formState.name.trim()) {
      errors.name = 'Name is required.';
    } else if (formState.name.trim().length > 200) {
      errors.name = 'Name must be 200 characters or less.';
    }
    if (!formState.folderPath.trim()) {
      errors.folderPath = 'Folder path is required.';
    }
    return errors;
  }, [formState]);

  const resetForm = useCallback(() => {
    setFormState(initialState);
    setValidationErrors({});
  }, []);

  return { formState, setFormState, validationErrors, setValidationErrors, clearFieldError, validateForm, resetForm };
};
