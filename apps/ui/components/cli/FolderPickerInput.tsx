import React, { useState } from 'react';
import { FolderOpen } from 'lucide-react';
import { FolderPickerDialog } from './FolderPickerDialog';

interface FolderPickerInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  className?: string;
}

export const FolderPickerInput: React.FC<FolderPickerInputProps> = ({
  value,
  onChange,
  placeholder,
  className,
}) => {
  const [showDialog, setShowDialog] = useState(false);

  const handleSelect = (fullPath: string) => {
    onChange(fullPath);
    setShowDialog(false);
  };

  return (
    <>
      <div className="flex gap-2">
        <input
          type="text"
          value={value}
          onChange={e => onChange(e.target.value)}
          placeholder={placeholder}
          className={className}
        />
        <button
          type="button"
          onClick={() => setShowDialog(true)}
          className="flex items-center gap-2 px-3 py-2.5 bg-background border border-border rounded-lg text-textMuted hover:text-text hover:bg-surfaceHighlight transition-colors"
          title="Browse for folder"
          aria-label="Browse for folder"
        >
          <FolderOpen size={16} />
        </button>
      </div>

      {showDialog && (
        <FolderPickerDialog
          initialPath={value}
          onSelect={handleSelect}
          onClose={() => setShowDialog(false)}
        />
      )}
    </>
  );
};
