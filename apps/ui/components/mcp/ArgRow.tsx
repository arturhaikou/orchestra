import React from 'react';
import { ChevronUp, ChevronDown, X } from 'lucide-react';

interface ArgRowProps {
  index: number;
  value: string;
  error?: string;
  isTouched: boolean;
  isFirst: boolean;
  isLast: boolean;
  isDisabled?: boolean;
  onChange: (index: number, value: string) => void;
  onRemove: (index: number) => void;
  onMoveUp: (index: number) => void;
  onMoveDown: (index: number) => void;
  onBlur: (index: number) => void;
}

const ArgRow: React.FC<ArgRowProps> = ({
  index, value, error, isTouched, isFirst, isLast, isDisabled,
  onChange, onRemove, onMoveUp, onMoveDown, onBlur,
}) => {
  const hasError = isTouched && !!error;
  return (
    <div className="flex flex-col gap-0.5">
      <div className="flex items-center gap-2">
        <div className="flex flex-col gap-0.5">
          <button
            type="button"
            disabled={isFirst || isDisabled}
            aria-label={`Move argument ${index} up`}
            onClick={() => onMoveUp(index)}
            className="p-0.5 text-textMuted hover:text-text disabled:opacity-25 disabled:pointer-events-none transition-colors"
          >
            <ChevronUp size={12} />
          </button>
          <button
            type="button"
            disabled={isLast || isDisabled}
            aria-label={`Move argument ${index} down`}
            onClick={() => onMoveDown(index)}
            className="p-0.5 text-textMuted hover:text-text disabled:opacity-25 disabled:pointer-events-none transition-colors"
          >
            <ChevronDown size={12} />
          </button>
        </div>
        <input
          type="text"
          value={value}
          disabled={isDisabled}
          aria-label={`Argument ${index}`}
          onChange={e => onChange(index, e.target.value)}
          onBlur={() => onBlur(index)}
          className={`flex-1 bg-surfaceHighlight border text-text text-sm font-mono rounded-md px-3 py-1.5
                      focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary
                      transition-colors disabled:opacity-45 disabled:cursor-not-allowed
                      ${hasError ? 'border-red-500' : 'border-border'}`}
        />
        <button
          type="button"
          disabled={isDisabled}
          aria-label={`Remove argument ${index}`}
          onClick={() => onRemove(index)}
          className="p-1 text-textMuted hover:text-red-400 transition-colors disabled:opacity-45"
        >
          <X size={14} />
        </button>
      </div>
      {hasError && <p className="ml-8 text-xs text-red-400">{error}</p>}
    </div>
  );
};

export default ArgRow;
