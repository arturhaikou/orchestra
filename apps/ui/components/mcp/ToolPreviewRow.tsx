import React from 'react';
import { ToolPreviewDto } from '../../types';

interface ToolPreviewRowProps {
  tool: ToolPreviewDto;
}

export const ToolPreviewRow: React.FC<ToolPreviewRowProps> = ({ tool }) => (
  <div
    className="flex flex-col gap-0.5 px-3 py-2 rounded-md border border-border bg-surface hover:bg-surface-highlight transition-colors"
    role="listitem"
  >
    <span className="text-sm font-medium text-text">{tool.name}</span>
    {tool.description && (
      <span className="text-xs text-textMuted">{tool.description}</span>
    )}
  </div>
);

export default ToolPreviewRow;
