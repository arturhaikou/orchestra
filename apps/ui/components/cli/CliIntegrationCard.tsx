import React from 'react';
import { Pencil, Terminal, Trash2 } from 'lucide-react';
import { AiCliIntegration, AiCliProviderType } from '../../types';

interface CliIntegrationCardProps {
  integration: AiCliIntegration;
  onEdit: () => void;
  onDelete: () => void;
  deleteButtonRef?: React.RefObject<HTMLButtonElement>;
}

const providerLabel: Record<AiCliProviderType, string> = {
  [AiCliProviderType.GITHUB_COPILOT]: 'GitHub Copilot',
  [AiCliProviderType.CLAUDE]: 'Claude',
  [AiCliProviderType.GEMINI]: 'Gemini',
};

const CliIntegrationCard: React.FC<CliIntegrationCardProps> = ({
  integration,
  onEdit,
  onDelete,
  deleteButtonRef,
}) => (
  <div className="bg-surface border border-border rounded-[10px] shadow-[0_1px_3px_rgba(0,0,0,0.4),0_0_0_1px_rgba(255,255,255,0.04)] flex flex-col h-[136px] transition-[border-color,box-shadow] duration-150 hover:border-indigo-500/30 hover:shadow-[0_2px_8px_rgba(0,0,0,0.5),0_0_0_1px_rgba(99,102,241,0.15)]">

    <div className="flex items-start gap-3 px-3.5 pt-3.5 pb-2.5">
      <div className="w-9 h-9 rounded-lg bg-surfaceHighlight border border-border/50 flex items-center justify-center shrink-0">
        <Terminal className="w-4 h-4 text-primary" />
      </div>
      <div className="flex-1 min-w-0 pt-0.5">
        <span
          className="block text-sm font-semibold text-text whitespace-nowrap overflow-hidden text-ellipsis"
          title={integration.name}
        >
          {integration.name}
        </span>
        <span className="text-xs text-textMuted">{providerLabel[integration.provider]}</span>
      </div>
    </div>

    <div className="flex-1 px-3.5 pb-2.5 flex items-center gap-3">
      <span className="inline-flex items-center gap-1.5 text-xs text-textMuted">
        <span
          className={`w-1.5 h-1.5 rounded-full ${integration.useLoggedInUser ? 'bg-emerald-500' : 'bg-sky-400'}`}
        />
        {integration.useLoggedInUser ? 'Logged-in user' : 'Token auth'}
      </span>
      {integration.modelId && (
        <span className="inline-flex items-center gap-1 text-xs text-textMuted bg-surfaceHighlight border border-border/50 rounded px-1.5 py-0.5 font-mono">
          {integration.modelId}
        </span>
      )}
    </div>

    <div className="flex items-center justify-end gap-1 px-2.5 py-2 border-t border-border bg-black/15 rounded-b-[10px]">
      <button
        type="button"
        aria-label="Edit CLI integration"
        title="Edit CLI integration"
        onClick={onEdit}
        className="w-8 h-8 rounded-[7px] flex items-center justify-center text-zinc-500 hover:text-text hover:bg-zinc-700/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 transition-[background,color] duration-[120ms]"
      >
        <Pencil className="w-4 h-4" />
      </button>
      <button
        ref={deleteButtonRef}
        type="button"
        aria-label="Delete CLI integration"
        title="Delete CLI integration"
        onClick={onDelete}
        className="w-8 h-8 rounded-[7px] flex items-center justify-center text-zinc-500 hover:text-red-400 hover:bg-red-500/[0.12] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 transition-[background,color] duration-[120ms]"
      >
        <Trash2 className="w-4 h-4" />
      </button>
    </div>

  </div>
);

export default CliIntegrationCard;
