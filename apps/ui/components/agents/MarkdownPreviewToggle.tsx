import React, { useState } from 'react';
import { Pencil, Eye } from 'lucide-react';
import { renderMarkdown } from '../../utils/markdownRenderer';

export interface MarkdownPreviewToggleProps {
  value: string;
  onChange: (value: string) => void;
  onFocus?: () => void;
  id?: string;
  rows?: number;
  placeholder?: string;
  disabled?: boolean;
}

const MarkdownPreviewToggle: React.FC<MarkdownPreviewToggleProps> = ({
  value,
  onChange,
  onFocus,
  id,
  rows = 6,
  placeholder,
  disabled,
}) => {
  const [mode, setMode] = useState<'edit' | 'preview'>('edit');

  const isEmptyPreview = value.trim() === '';

  return (
    <div className="flex flex-col gap-2">
      <div className="flex gap-1">
        <button
          type="button"
          onClick={() => setMode('edit')}
          aria-pressed={mode === 'edit'}
          className={
            mode === 'edit'
              ? 'text-xs px-3 py-1 rounded transition-colors bg-primary/10 text-primary font-medium'
              : 'text-xs px-3 py-1 rounded transition-colors text-textMuted hover:text-text'
          }
        >
          <Pencil className="w-3 h-3 inline mr-1" />
          Write
        </button>

        <button
          type="button"
          onClick={() => setMode('preview')}
          aria-pressed={mode === 'preview'}
          className={
            mode === 'preview'
              ? 'text-xs px-3 py-1 rounded transition-colors bg-primary/10 text-primary font-medium'
              : 'text-xs px-3 py-1 rounded transition-colors text-textMuted hover:text-text'
          }
        >
          <Eye className="w-3 h-3 inline mr-1" />
          Preview
        </button>
      </div>

      {mode === 'edit' ? (
        <textarea
          id={id}
          value={value}
          onChange={event => onChange(event.target.value)}
          onFocus={onFocus}
          rows={rows}
          placeholder={placeholder}
          disabled={disabled}
          className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary resize-y"
        />
      ) : isEmptyPreview ? (
        <p className="text-textMuted italic text-sm px-3 py-2">
          Nothing to preview yet.
        </p>
      ) : (
        <div
          data-testid="preview-pane"
          className="w-full px-3 py-2 bg-surface border border-border rounded-md min-h-[9rem] text-text text-sm prose prose-sm dark:prose-invert max-w-none prose-headings:font-bold prose-a:text-primary"
          dangerouslySetInnerHTML={{ __html: renderMarkdown(value) }}
        />
      )}
    </div>
  );
};

export default MarkdownPreviewToggle;
