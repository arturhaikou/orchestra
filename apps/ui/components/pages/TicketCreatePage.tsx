import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { getTicketStatuses, getTicketPriorities, createTicket } from '../../services/ticketService';
import { TicketStatus, TicketPriority } from '../../types';
import { ArrowLeft, Save, Loader2, Activity, Flag, ChevronDown } from 'lucide-react';
import Toast from '../Toast';

const TITLE_MAX_LENGTH = 500;
const DESCRIPTION_MAX_LENGTH = 10000;

const TicketCreatePage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();

  const [statuses, setStatuses] = useState<TicketStatus[]>([]);
  const [priorities, setPriorities] = useState<TicketPriority[]>([]);

  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [statusId, setStatusId] = useState('');
  const [priorityId, setPriorityId] = useState('');

  const [errors, setErrors] = useState<{ title?: string; description?: string }>({});
  const [isSaving, setIsSaving] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [toastError, setToastError] = useState<string | null>(null);

  useEffect(() => {
    const loadDropdownData = async () => {
      try {
        const [statusList, priorityList] = await Promise.all([
          getTicketStatuses(),
          getTicketPriorities(),
        ]);
        setStatuses(statusList);
        setPriorities(priorityList);
        if (statusList.length > 0) setStatusId(statusList[0].id);
        const medium = priorityList.find(p => p.name === 'Medium');
        if (medium) setPriorityId(medium.id);
        else if (priorityList.length > 0) setPriorityId(priorityList[0].id);
      } catch {
        setToastError('Failed to load form data.');
      } finally {
        setIsLoading(false);
      }
    };
    loadDropdownData();
  }, []);

  const validateForm = (): boolean => {
    const newErrors: { title?: string; description?: string } = {};

    if (!title.trim()) {
      newErrors.title = 'Title is required and must not exceed 500 characters.';
    } else if (title.length > TITLE_MAX_LENGTH) {
      newErrors.title = 'Title is required and must not exceed 500 characters.';
    }

    if (!description.trim()) {
      newErrors.description = 'Description is required.';
    } else if (description.length > DESCRIPTION_MAX_LENGTH) {
      newErrors.description = 'Description must not exceed 10,000 characters.';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSave = async () => {
    if (!validateForm()) return;

    setIsSaving(true);
    setToastError(null);

    try {
      await createTicket(workspaceId!, {
        title: title.trim(),
        description: description.trim(),
        statusId,
        priorityId,
      });
      navigate(`/workspaces/${workspaceId}/tickets`);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'An error occurred while creating the ticket.';
      setToastError(message);
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancel = () => {
    navigate(`/workspaces/${workspaceId}/tickets`);
  };

  if (isLoading) {
    return (
      <div className="flex-1 p-6 max-w-3xl mx-auto flex items-center justify-center">
        <Loader2 className="w-6 h-6 animate-spin text-textMuted" />
      </div>
    );
  }

  return (
    <div className="flex-1 p-6 max-w-3xl mx-auto">
      <button
        onClick={handleCancel}
        className="flex items-center gap-2 text-sm text-textMuted hover:text-text transition-colors mb-6"
      >
        <ArrowLeft className="w-4 h-4" />
        Back to Tickets
      </button>

      <h1 className="text-2xl font-bold text-text mb-6">Create New Ticket</h1>

      <div className="space-y-6">
        <div className="space-y-1.5">
          <label htmlFor="ticket-title" className="text-[10px] font-bold text-textMuted uppercase tracking-wider">
            Title
          </label>
          <input
            id="ticket-title"
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="e.g., Fix login page styling"
            className={`w-full bg-background border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all shadow-sm placeholder:text-textMuted/40 ${errors.title ? 'border-red-500' : 'border-border'}`}
          />
          <div className="flex justify-between items-center">
            {errors.title && <p className="text-xs text-red-500">{errors.title}</p>}
            <p className={`text-xs ml-auto ${title.length > TITLE_MAX_LENGTH ? 'text-red-500' : 'text-textMuted'}`}>
              {title.length} / 500
            </p>
          </div>
        </div>

        <div className="space-y-1.5">
          <label htmlFor="ticket-description" className="text-[10px] font-bold text-textMuted uppercase tracking-wider">
            Description
          </label>
          <textarea
            id="ticket-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Describe the issue in detail..."
            className={`w-full bg-background border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary min-h-[200px] resize-none shadow-sm placeholder:text-textMuted/40 ${errors.description ? 'border-red-500' : 'border-border'}`}
          />
          <div className="flex justify-between items-center">
            {errors.description && <p className="text-xs text-red-500">{errors.description}</p>}
            <p className={`text-xs ml-auto ${description.length > DESCRIPTION_MAX_LENGTH ? 'text-red-500' : 'text-textMuted'}`}>
              {description.length} / 10,000
            </p>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-1.5">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider flex items-center gap-1.5">
              <Activity className="w-3.5 h-3.5" /> Status
            </label>
            <div className="relative">
              <select
                value={statusId}
                onChange={(e) => setStatusId(e.target.value)}
                className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
              >
                {statuses.map(s => (
                  <option key={s.id} value={s.id}>{s.name}</option>
                ))}
              </select>
              <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
            </div>
          </div>
          <div className="space-y-1.5">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider flex items-center gap-1.5">
              <Flag className="w-3.5 h-3.5" /> Priority
            </label>
            <div className="relative">
              <select
                value={priorityId}
                onChange={(e) => setPriorityId(e.target.value)}
                className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
              >
                {priorities.map(p => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
              <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
            </div>
          </div>
        </div>

        <div className="pt-4 border-t border-border flex gap-4">
          <button
            type="button"
            onClick={handleCancel}
            className="flex-1 px-4 py-2.5 border border-border rounded-lg text-sm font-bold text-text hover:bg-surfaceHighlight transition-all active:scale-[0.98]"
            disabled={isSaving}
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={isSaving}
            className="flex-1 px-4 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-lg text-sm font-bold transition-all shadow-lg shadow-primary/20 flex items-center justify-center gap-2 active:scale-[0.98]"
          >
            {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            {isSaving ? 'Saving...' : 'Save'}
          </button>
        </div>
      </div>

      {toastError && (
        <Toast message={toastError} type="error" onClose={() => setToastError(null)} />
      )}
    </div>
  );
};

export default TicketCreatePage;
