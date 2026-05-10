import React from 'react';
import { AlertCircle, Wrench } from 'lucide-react';
import { ToolPreviewDto } from '../../types';
import ToolPreviewRow from './ToolPreviewRow';

interface ToolPreviewSectionProps {
  tools: ToolPreviewDto[];
}

export const ToolPreviewSection: React.FC<ToolPreviewSectionProps> = ({ tools }) => (
  <div className="flex flex-col gap-2 mt-4">
    <div className="flex items-center justify-between">
      <div className="flex items-center gap-1.5 text-xs font-semibold text-textMuted uppercase tracking-wide">
        <Wrench size={14} aria-hidden="true" />
        Available Tools
      </div>
      <div className="flex items-center gap-2">
        <span className="text-xs px-2 py-0.5 rounded-full bg-surface-highlight text-textMuted border border-border">
          {tools.length > 0 ? `${tools.length} tools found` : 'No tools'}
        </span>
        <span className="text-[10px] font-bold px-1.5 py-0.5 rounded border bg-indigo-500/10 text-indigo-400 border-indigo-500/20">
          Read-only
        </span>
      </div>
    </div>

    <div
      className="flex flex-col gap-1"
      role="list"
      aria-label="Discovered tools"
    >
      {tools.length === 0 ? (
        <div className="flex items-center gap-2 text-xs text-textMuted py-2">
          <AlertCircle size={14} aria-hidden="true" />
          No tools found on this server.
        </div>
      ) : (
        tools.map(tool => (
          <ToolPreviewRow key={tool.name} tool={tool} />
        ))
      )}
    </div>
  </div>
);

export default ToolPreviewSection;
