import React, { useRef, useEffect } from 'react';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { McpServer } from '../../types';

interface DeleteMcpServerModalProps {
  server: McpServer | null;
  isDeleting: boolean;
  error: string | null;
  onCancel: () => void;
  onConfirm: () => void;
  /**
   * How many agents will lose access when this server is deleted.
   * `null` = the count is still loading (show a loading indicator).
   * `0`    = no agents are affected.
   * `N`    = N agents will be impacted.
   */
  affectedAgentCount: number | null;
}

const FOCUSABLE_SELECTORS =
  'button:not([disabled]), [href], input:not([disabled]), [tabindex]:not([tabindex="-1"])';

const DeleteMcpServerModal: React.FC<DeleteMcpServerModalProps> = ({
  server,
  isDeleting,
  error,
  onCancel,
  onConfirm,
  affectedAgentCount,
}) => {
  const modalRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!server) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (!isDeleting) onCancel();
        return;
      }
      if (e.key !== 'Tab') return;
      trapFocus(e);
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [server, isDeleting, onCancel]);

  const trapFocus = (e: KeyboardEvent) => {
    const focusable = Array.from(
      modalRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS) ?? [],
    );
    if (focusable.length === 0) return;

    const first = focusable[0];
    const last = focusable[focusable.length - 1];

    if (e.shiftKey) {
      if (document.activeElement === first) { e.preventDefault(); last.focus(); }
    } else {
      if (document.activeElement === last) { e.preventDefault(); first.focus(); }
    }
  };

  if (!server) return null;

  return (
    <div
      className="fixed inset-0 bg-black/60 backdrop-blur-[2px] flex items-center justify-center z-[200]"
    >
      <div
        ref={modalRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="delete-modal-title"
        aria-describedby="delete-modal-body delete-modal-impact"
        aria-busy={isDeleting}
        className="bg-surface border border-border rounded-[14px] p-6 w-[440px] max-w-[calc(100vw-2rem)] shadow-[0_24px_64px_rgba(0,0,0,0.7),0_0_0_1px_rgba(255,255,255,0.06)]"
      >
        <div className="w-11 h-11 rounded-full bg-red-500/[0.12] border border-red-500/20 flex items-center justify-center mb-4">
          <AlertTriangle className="w-5 h-5 text-red-400" aria-hidden="true" />
        </div>

        <h2
          id="delete-modal-title"
          className="text-base font-bold text-text mb-2"
        >
          Delete MCP Server?
        </h2>

        <p
          id="delete-modal-body"
          className="text-[13.5px] text-textMuted leading-relaxed mb-3"
        >
          Are you sure you want to delete{' '}
          <strong className="text-text font-semibold">{server.name}</strong>?
        </p>

        {affectedAgentCount === null ? (
          <div
            role="status"
            aria-label="Loading agent impact count"
            className="flex items-center gap-2 text-sm text-muted mt-3"
          >
            <svg
              className="animate-spin h-4 w-4 text-muted"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              aria-hidden="true"
            >
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v4l3-3-3-3V0a12 12 0 100 24v-4l-3 3 3 3v4a12 12 0 000-24z" />
            </svg>
            <span>Checking agent impact…</span>
          </div>
        ) : (
          <p
            id="delete-modal-impact"
            className="mt-3 text-sm text-warning"
          >
            {affectedAgentCount} agent(s) will lose access to tools from this server.{' '}
            This action cannot be undone.
          </p>
        )}

        {error !== null && (
          <div
            role="alert"
            className="text-[13px] text-red-400 bg-red-500/[0.08] border border-red-500/20 rounded-[7px] px-3 py-2 mb-4"
          >
            {error}
          </div>
        )}

        <div className="flex items-center justify-end gap-2.5">
          <button
            type="button"
            ref={(el) => { if (el) el.setAttribute('autofocus', ''); }}
            autoFocus
            disabled={isDeleting}
            onClick={onCancel}
            className="px-4 py-2 rounded-[7px] text-[13.5px] font-medium text-textMuted border border-border hover:bg-surface-2 hover:text-text disabled:opacity-40 disabled:cursor-not-allowed transition-colors duration-[120ms]"
          >
            Cancel
          </button>

          <button
            type="button"
            disabled={isDeleting}
            onClick={onConfirm}
            className="px-4 py-2 rounded-[7px] text-[13.5px] font-medium bg-red-500 text-white hover:bg-red-600 disabled:opacity-50 disabled:cursor-not-allowed transition-colors duration-[120ms] flex items-center gap-2"
          >
            {isDeleting ? (
              <>
                <Loader2 className="w-3.5 h-3.5 animate-spin" aria-hidden="true" />
                Deleting…
              </>
            ) : error !== null ? (
              'Retry Delete'
            ) : (
              'Delete'
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

export default DeleteMcpServerModal;
