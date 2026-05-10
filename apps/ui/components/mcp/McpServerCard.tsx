import React from 'react';
import { Pencil, Trash2 } from 'lucide-react';
import { McpServer } from '../../types';
import McpTransportBadge from './McpTransportBadge';
import McpServerStatusBadge from './McpServerStatusBadge';

interface McpServerCardProps {
  server: McpServer;
  onEdit: () => void;
  onDelete: () => void;
  deleteButtonRef?: React.RefObject<HTMLButtonElement>;
}

const McpServerCard: React.FC<McpServerCardProps> = ({ server, onEdit, onDelete, deleteButtonRef }) => (
  <div className="bg-surface border border-border rounded-[10px] shadow-[0_1px_3px_rgba(0,0,0,0.4),0_0_0_1px_rgba(255,255,255,0.04)] flex flex-col h-[136px] transition-[border-color,box-shadow] duration-150 hover:border-indigo-500/30 hover:shadow-[0_2px_8px_rgba(0,0,0,0.5),0_0_0_1px_rgba(99,102,241,0.15)]">

    <div className="flex items-start justify-between gap-2.5 px-3.5 pt-3.5 pb-2.5">
      <span
        className="block flex-1 min-w-0 text-sm font-semibold text-text whitespace-nowrap overflow-hidden text-ellipsis"
        title={server.name}
      >
        {server.name}
      </span>
      <McpTransportBadge transportType={server.transportType} />
    </div>

    <div className="flex-1 px-3.5 pb-2.5 flex items-start">
      <McpServerStatusBadge status={server.connectionStatus} />
    </div>

    <div className="flex items-center justify-end gap-1 px-2.5 py-2 border-t border-border bg-black/15 rounded-b-[10px]">
      <button
        type="button"
        aria-label="Edit server"
        title="Edit server"
        onClick={onEdit}
        className="w-8 h-8 rounded-[7px] flex items-center justify-center text-zinc-500 hover:text-text hover:bg-zinc-700/50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 transition-[background,color] duration-[120ms]"
      >
        <Pencil className="w-4 h-4" />
      </button>
      <button
        ref={deleteButtonRef}
        type="button"
        aria-label="Delete server"
        title="Delete server"
        onClick={onDelete}
        className="w-8 h-8 rounded-[7px] flex items-center justify-center text-zinc-500 hover:text-red-400 hover:bg-red-500/[0.12] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 transition-[background,color] duration-[120ms]"
      >
        <Trash2 className="w-4 h-4" />
      </button>
    </div>

  </div>
);

export default McpServerCard;
