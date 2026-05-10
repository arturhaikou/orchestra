import React from 'react';
import { Plus } from 'lucide-react';
import ArgRow from './ArgRow';

const MAX_ARGS = 50;

interface ArgListEditorProps {
  args: string[];
  argErrors: Record<number, string>;
  argTouched: Record<number, boolean>;
  isDisabled?: boolean;
  onChange: (args: string[]) => void;
  onArgBlur: (index: number) => void;
}

const ArgListEditor: React.FC<ArgListEditorProps> = ({
  args, argErrors, argTouched, isDisabled, onChange, onArgBlur,
}) => (
  <div>
    <label className="block text-sm font-medium text-textMuted mb-1.5">Arguments</label>
    <div className="space-y-2">
      {args.length === 0 && (
        <p className="text-xs text-textMuted italic">
          No arguments yet.
        </p>
      )}
      {args.map((arg, i) => (
        <ArgRow
          key={i}
          index={i}
          value={arg}
          error={argErrors[i]}
          isTouched={!!argTouched[i]}
          isFirst={i === 0}
          isLast={i === args.length - 1}
          isDisabled={isDisabled}
          onChange={(idx, val) => onChange(updateArg(args, idx, val))}
          onRemove={idx => onChange(removeArg(args, idx))}
          onMoveUp={idx => onChange(swapArgs(args, idx, idx - 1))}
          onMoveDown={idx => onChange(swapArgs(args, idx, idx + 1))}
          onBlur={onArgBlur}
        />
      ))}
      <button
        type="button"
        disabled={isDisabled || args.length >= MAX_ARGS}
        onClick={() => onChange([...args, ''])}
        className="text-xs text-primary hover:text-primary/80 transition-colors
                   disabled:opacity-45 disabled:cursor-not-allowed flex items-center gap-1"
      >
        <Plus size={12} /> Add Argument
      </button>
    </div>
  </div>
);

function updateArg(args: string[], index: number, value: string): string[] {
  return args.map((a, i) => (i === index ? value : a));
}

function removeArg(args: string[], index: number): string[] {
  return args.filter((_, i) => i !== index);
}

function swapArgs(args: string[], indexA: number, indexB: number): string[] {
  const next = [...args];
  [next[indexA], next[indexB]] = [next[indexB], next[indexA]];
  return next;
}

export default ArgListEditor;
