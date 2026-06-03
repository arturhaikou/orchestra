import React, { useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import { Save, Loader2, AlertTriangle, ArrowLeft, Activity, Flag, ChevronDown } from 'lucide-react';
import { useTicketEditForm, TITLE_MAX_LENGTH, DESCRIPTION_MAX_LENGTH } from '../../hooks/useTicketEditForm';
import Toast from '../Toast';

const TicketEditPage: React.FC = () => {
  const { workspaceId, ticketId } = useParams<{ workspaceId: string; ticketId: string }>();

  const {
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
  } = useTicketEditForm(workspaceId, ticketId);

  const handleTitleChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setFormState(prev => ({ ...prev, title: e.target.value }));
  }, [setFormState]);

  const handleDescriptionChange = useCallback((e: React.ChangeEvent<HTMLTextAreaElement>) => {
    setFormState(prev => ({ ...prev, description: e.target.value }));
  }, [setFormState]);

  const handleStatusChange = useCallback((e: React.ChangeEvent<HTMLSelectElement>) => {
    setFormState(prev => ({ ...prev, statusId: e.target.value }));
  }, [setFormState]);

  const handlePriorityChange = useCallback((e: React.ChangeEvent<HTMLSelectElement>) => {
    setFormState(prev => ({ ...prev, priorityId: e.target.value }));
  }, [setFormState]);

  if (isLoading) {
    return (
      <div className="flex-1 p-6 max-w-3xl mx-auto">
        <Link to={ticketsListPath} className="flex items-center gap-2 text-sm text-textMuted hover:text-text transition-colors mb-6">
          <ArrowLeft className="w-4 h-4" /> Back to Tickets
        </Link>
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
        </div>
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="flex-1 p-6 max-w-3xl mx-auto">
        <div className="text-center py-20">
          <AlertTriangle className="w-12 h-12 text-red-400 mx-auto mb-4" />
          <h2 className="text-xl font-bold text-text mb-2">Ticket not found</h2>
          <p className="text-textMuted mb-6">The ticket you're looking for doesn't exist or has been removed.</p>
          <Link to={ticketsListPath} className="inline-flex items-center gap-2 px-4 py-2 bg-primary hover:bg-primaryHover text-white rounded-lg text-sm font-bold transition-all">
            Return to Tickets
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="flex-1 p-6 max-w-3xl mx-auto">
      <Link to={ticketsListPath} className="flex items-center gap-2 text-sm text-textMuted hover:text-text transition-colors mb-6">
        <ArrowLeft className="w-4 h-4" /> Back to Tickets
      </Link>

      <h1 className="text-2xl font-bold bg-gradient-to-r from-text to-textMuted bg-clip-text text-transparent mb-6">Edit Ticket</h1>

      <form onSubmit={handleSave} className="space-y-6">
        <div className="space-y-1.5">
          <label htmlFor="ticket-title" className="text-[10px] font-bold text-textMuted uppercase tracking-wider">
            Title
          </label>
          <input
            id="ticket-title"
            type="text"
            value={formState.title}
            onChange={handleTitleChange}
            placeholder="e.g., Fix login page styling"
            disabled={isSaving}
            className={`w-full bg-background border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all shadow-sm placeholder:text-textMuted/40 ${validationErrors.title ? 'border-red-500' : 'border-border'}`}
          />
          <div className="flex justify-between items-center">
            {validationErrors.title && <p className="text-xs text-red-500">{validationErrors.title}</p>}
            <p className={`text-xs ml-auto ${formState.title.length > TITLE_MAX_LENGTH ? 'text-red-500' : 'text-textMuted'}`}>
              {formState.title.length} / {TITLE_MAX_LENGTH}
            </p>
          </div>
        </div>

        <div className="space-y-1.5">
          <label htmlFor="ticket-description" className="text-[10px] font-bold text-textMuted uppercase tracking-wider">
            Description
          </label>
          <textarea
            id="ticket-description"
            value={formState.description}
            onChange={handleDescriptionChange}
            placeholder="Describe the issue in detail..."
            disabled={isSaving}
            className={`w-full bg-background border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary min-h-[200px] resize-none shadow-sm placeholder:text-textMuted/40 ${validationErrors.description ? 'border-red-500' : 'border-border'}`}
          />
          <div className="flex justify-between items-center">
            {validationErrors.description && <p className="text-xs text-red-500">{validationErrors.description}</p>}
            <p className={`text-xs ml-auto ${formState.description.length > DESCRIPTION_MAX_LENGTH ? 'text-red-500' : 'text-textMuted'}`}>
              {formState.description.length} / {DESCRIPTION_MAX_LENGTH.toLocaleString()}
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
                value={formState.statusId}
                onChange={handleStatusChange}
                disabled={isSaving}
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
                value={formState.priorityId}
                onChange={handlePriorityChange}
                disabled={isSaving}
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
            disabled={isSaving}
            className="flex-1 px-4 py-2.5 border border-border rounded-lg text-sm font-bold text-text hover:bg-surfaceHighlight transition-all active:scale-[0.98]"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={isSaving}
            className="flex-1 px-4 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-lg text-sm font-bold transition-all shadow-lg shadow-primary/20 hover:shadow-[0_0_20px_rgba(99,102,241,0.2)] flex items-center justify-center gap-2 active:scale-[0.98]"
          >
            {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
            {isSaving ? 'Saving...' : 'Save Changes'}
          </button>
        </div>
      </form>

      {saveError && (
        <Toast message={saveError} type="error" onClose={() => {}} />
      )}
    </div>
  );
};

export default TicketEditPage;
