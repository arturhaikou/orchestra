import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Ticket, TicketStatus, TicketPriority } from '../types';
import { getTicketById, getTicketStatuses, getTicketPriorities, updateTicket } from '../services/ticketService';

export const TITLE_MAX_LENGTH = 500;
export const DESCRIPTION_MAX_LENGTH = 10000;

export interface TicketEditFormState {
  title: string;
  description: string;
  statusId: string;
  priorityId: string;
}

export interface UseTicketEditFormReturn {
  formState: TicketEditFormState;
  setFormState: React.Dispatch<React.SetStateAction<TicketEditFormState>>;
  statuses: TicketStatus[];
  priorities: TicketPriority[];
  isLoading: boolean;
  loadError: string | null;
  isSaving: boolean;
  saveError: string | null;
  validationErrors: Record<string, string>;
  ticketsListPath: string;
  handleSave: (e: React.FormEvent) => Promise<void>;
  handleCancel: () => void;
}

export const useTicketEditForm = (
  workspaceId: string | undefined,
  ticketId: string | undefined
): UseTicketEditFormReturn => {
  const navigate = useNavigate();
  const ticketsListPath = `/workspaces/${workspaceId}/tickets`;

  const [formState, setFormState] = useState<TicketEditFormState>({
    title: '',
    description: '',
    statusId: '',
    priorityId: '',
  });

  const [statuses, setStatuses] = useState<TicketStatus[]>([]);
  const [priorities, setPriorities] = useState<TicketPriority[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!workspaceId || !ticketId) return;

    const loadData = async () => {
      setIsLoading(true);
      setLoadError(null);
      try {
        const [ticket, statusList, priorityList] = await Promise.all([
          getTicketById(ticketId),
          getTicketStatuses(),
          getTicketPriorities(),
        ]);

        setStatuses(statusList);
        setPriorities(priorityList);
        setFormState({
          title: ticket.title,
          description: ticket.description || '',
          statusId: ticket.status?.id || (statusList.length > 0 ? statusList[0].id : ''),
          priorityId: ticket.priority?.id || (priorityList.length > 0 ? priorityList[0].id : ''),
        });
      } catch (error) {
        const message = error instanceof Error ? error.message : 'Failed to load ticket';
        setLoadError(message);
      } finally {
        setIsLoading(false);
      }
    };

    loadData();
  }, [workspaceId, ticketId]);

  const validateForm = useCallback((): Record<string, string> => {
    const errors: Record<string, string> = {};

    if (!formState.title.trim()) {
      errors.title = 'Title is required.';
    } else if (formState.title.length > TITLE_MAX_LENGTH) {
      errors.title = `Title must not exceed ${TITLE_MAX_LENGTH} characters.`;
    }

    if (formState.description.length > DESCRIPTION_MAX_LENGTH) {
      errors.description = `Description must not exceed ${DESCRIPTION_MAX_LENGTH.toLocaleString()} characters.`;
    }

    return errors;
  }, [formState.title, formState.description]);

  const executeSave = async () => {
    setIsSaving(true);
    setSaveError(null);
    try {
      await updateTicket(ticketId!, {
        title: formState.title.trim(),
        description: formState.description.trim(),
        statusId: formState.statusId,
        priorityId: formState.priorityId,
      } as Partial<Ticket>);
      navigate(ticketsListPath);
    } catch (error) {
      setSaveError(error instanceof Error ? error.message : 'Failed to update ticket');
    } finally {
      setIsSaving(false);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    const errors = validateForm();
    if (Object.keys(errors).length > 0) {
      setValidationErrors(errors);
      return;
    }
    setValidationErrors({});
    await executeSave();
  };

  const handleCancel = () => {
    navigate(ticketsListPath);
  };

  return {
    formState,
    setFormState,
    statuses,
    priorities,
    isLoading,
    loadError,
    isSaving,
    saveError,
    validationErrors,
    ticketsListPath,
    handleSave,
    handleCancel,
  };
};
