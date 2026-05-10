import React from 'react';
import { Loader2 } from 'lucide-react';

interface FormFooterProps {
  onCancel: () => void;
  onSave: () => void;
  isSaveDisabled: boolean;
  isSaving: boolean;
}

const FormFooter: React.FC<FormFooterProps> = ({
  onCancel,
  onSave,
  isSaveDisabled,
  isSaving,
}) => (
  <div className="flex items-center justify-end gap-3 px-7 py-4 border-t border-border bg-black/10">
    <button
      type="button"
      onClick={onCancel}
      className="px-4 py-2 text-sm font-medium text-textMuted hover:text-text
                 border border-border hover:border-zinc-500 rounded-md
                 transition-[border-color,color] duration-150"
    >
      Cancel
    </button>

    <button
      type="submit"
      disabled={isSaveDisabled || isSaving}
      title={isSaveDisabled ? 'Please verify the connection first' : undefined}
      className="px-4 py-2 text-sm font-semibold rounded-md bg-primary hover:bg-primary-hover
                 text-white disabled:opacity-40 disabled:cursor-not-allowed
                 transition-[background,opacity] duration-150 inline-flex items-center gap-2"
    >
      {isSaving && <Loader2 className="w-3.5 h-3.5 animate-spin" />}
      Save MCP Server
    </button>
  </div>
);

export default FormFooter;
