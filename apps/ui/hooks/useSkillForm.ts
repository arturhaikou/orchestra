import { useState, useCallback } from 'react';

export interface SkillFormState {
  name: string;
  description: string;
  instructions: string;
}

const initialSkillFormState: SkillFormState = {
  name: '',
  description: '',
  instructions: '',
};

interface UseSkillFormResult {
  formState: SkillFormState;
  setFormState: React.Dispatch<React.SetStateAction<SkillFormState>>;
  validationErrors: Record<string, string>;
  setValidationErrors: React.Dispatch<React.SetStateAction<Record<string, string>>>;
  clearFieldError: (field: string) => void;
  validateForm: () => Record<string, string>;
  resetForm: () => void;
}

export const useSkillForm = (initial?: Partial<SkillFormState>): UseSkillFormResult => {
  const [formState, setFormState] = useState<SkillFormState>({
    ...initialSkillFormState,
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
    } else if (formState.name.trim().length > 64) {
      errors.name = 'Name must be 64 characters or less.';
    }
    if (!formState.description.trim()) {
      errors.description = 'Description is required.';
    } else if (formState.description.trim().length > 1024) {
      errors.description = 'Description must be 1024 characters or less.';
    }
    if (!formState.instructions.trim()) {
      errors.instructions = 'Instructions are required.';
    }
    return errors;
  }, [formState]);

  const resetForm = useCallback(() => {
    setFormState(initialSkillFormState);
    setValidationErrors({});
  }, []);

  return {
    formState,
    setFormState,
    validationErrors,
    setValidationErrors,
    clearFieldError,
    validateForm,
    resetForm,
  };
};
