import React from 'react';
import { useParams } from 'react-router-dom';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { useCliIntegrationForm } from '../../hooks/useCliIntegrationForm';
import { FolderPickerInput } from '../../components/cli/FolderPickerInput';

const CreateCliIntegrationPage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();

  const {
    formState,
    setField,
    isSaving,
    saveError,
    validationErrors,
    handleSave,
    handleCancel,
  } = useCliIntegrationForm(workspaceId ?? '');

  return (
    <div className="max-w-3xl mx-auto py-8 px-4">
      <div className="bg-surface border border-border rounded-xl shadow-xl shadow-primary/5 overflow-hidden">

        <div className="px-6 py-4 border-b border-border-elevated">
          <h1 className="text-lg font-semibold text-text">Add CLI Connection</h1>
          <p className="text-sm text-textMuted mt-0.5">Connect an AI CLI to your workspace.</p>
        </div>

        <div className="p-6 space-y-6">

          {/* Provider — read-only, only GitHub Copilot is functional */}
          <div className="space-y-2">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Provider</label>
            <div className="inline-flex items-center gap-2 px-3 py-2 bg-surfaceHighlight border border-border rounded-lg text-sm text-text">
              <span className="w-2 h-2 rounded-full bg-emerald-500" />
              GitHub Copilot
            </div>
            <p className="text-xs text-textMuted">Additional providers coming soon.</p>
          </div>

          {/* Name */}
          <div className="space-y-2">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-widest">
              Connection Name
            </label>
            <input
              type="text"
              value={formState.name}
              onChange={e => setField('name', e.target.value)}
              placeholder="e.g. My Copilot CLI"
              className="w-full bg-background border border-border rounded-lg px-3 py-2.5 text-sm text-text placeholder:text-textMuted focus:outline-none focus:border-primary"
            />
            {validationErrors.name && (
              <FieldError message={validationErrors.name} />
            )}
          </div>

          {/* Authentication */}
          <div className="space-y-3">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Authentication</label>

            <label className="flex items-center gap-3 cursor-pointer select-none">
              <div
                role="checkbox"
                aria-checked={formState.useLoggedInUser}
                tabIndex={0}
                onClick={() => setField('useLoggedInUser', !formState.useLoggedInUser)}
                onKeyDown={e => e.key === ' ' && setField('useLoggedInUser', !formState.useLoggedInUser)}
                className={`w-9 h-5 rounded-full transition-colors duration-150 relative cursor-pointer focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/60 ${formState.useLoggedInUser ? 'bg-primary' : 'bg-border'}`}
              >
                <span
                  className={`absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white shadow transition-transform duration-150 ${formState.useLoggedInUser ? 'translate-x-4' : 'translate-x-0'}`}
                />
              </div>
              <div>
                <span className="text-sm font-medium text-text">Use logged-in user</span>
                <p className="text-xs text-textMuted">Use the GitHub account currently authenticated on this machine.</p>
              </div>
            </label>

            {!formState.useLoggedInUser && (
              <div className="space-y-2">
                <label className="text-[10px] font-bold text-textMuted uppercase tracking-widest">GitHub Token</label>
                <input
                  type="password"
                  value={formState.credential}
                  onChange={e => setField('credential', e.target.value)}
                  placeholder="ghp_••••••••••••••••••••••••••••••••••••••"
                  autoComplete="new-password"
                  className="w-full bg-background border border-border rounded-lg px-3 py-2.5 text-sm text-text placeholder:text-textMuted focus:outline-none focus:border-primary font-mono"
                />
                {validationErrors.credential && (
                  <FieldError message={validationErrors.credential} />
                )}
              </div>
            )}
          </div>

          {/* Working Directory */}
          <div className="space-y-2">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Working Directory</label>
            <FolderPickerInput
              value={formState.workingDirectory}
              onChange={value => setField('workingDirectory', value)}
              placeholder="/home/user/projects or C:\projects"
              className="w-full bg-background border border-border rounded-lg px-3 py-2.5 text-sm text-text placeholder:text-textMuted focus:outline-none focus:border-primary font-mono"
            />
            <p className="text-xs text-textMuted">Absolute path to the working directory on this machine.</p>
            {validationErrors.workingDirectory && (
              <FieldError message={validationErrors.workingDirectory} />
            )}
          </div>

          {/* CLI Path */}
          <div className="space-y-2">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-widest">CLI Path</label>
            <input
              type="text"
              value={formState.cliPath}
              onChange={e => setField('cliPath', e.target.value)}
              placeholder="/usr/local/bin/gh or C:\Program Files\GitHub CLI\gh.exe"
              className="w-full bg-background border border-border rounded-lg px-3 py-2.5 text-sm text-text placeholder:text-textMuted focus:outline-none focus:border-primary font-mono"
            />
            <p className="text-xs text-textMuted">Absolute path to the GitHub Copilot CLI binary (<code className="bg-surfaceHighlight px-1 rounded">gh</code>) on this machine.</p>
            {validationErrors.cliPath && (
              <FieldError message={validationErrors.cliPath} />
            )}
          </div>

          {saveError && (
            <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
              <AlertTriangle size={14} />
              {saveError}
            </div>
          )}

        </div>

        <div className="px-6 py-4 border-t border-border flex justify-end gap-3">
          <button
            type="button"
            onClick={handleCancel}
            disabled={isSaving}
            className="px-4 py-2 text-sm text-textMuted hover:text-text border border-border rounded-lg hover:bg-surfaceHighlight transition-colors disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={isSaving}
            className="flex items-center gap-2 px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primaryHover transition-colors disabled:opacity-50"
          >
            {isSaving && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
            {isSaving ? 'Saving…' : 'Save Connection'}
          </button>
        </div>

      </div>
    </div>
  );
};

const FieldError: React.FC<{ message: string }> = ({ message }) => (
  <p className="text-xs text-red-400 flex items-center gap-1">
    <AlertTriangle size={11} />
    {message}
  </p>
);

export default CreateCliIntegrationPage;
