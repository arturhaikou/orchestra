import React from 'react';
import { Lock } from 'lucide-react';

interface LockedFieldProps {
  label: string;
  value: string;
}

const LockedField: React.FC<LockedFieldProps> = ({ label, value }) => {
  return (
    <div className="space-y-1.5">
      <label className="text-[10px] font-semibold text-textMuted uppercase">{label}</label>
      <div
        aria-readonly="true"
        aria-disabled="true"
        className="bg-surfaceHighlight border border-border rounded-md px-3 py-2 text-sm text-textMuted cursor-not-allowed flex items-center gap-2"
      >
        <Lock className="w-3.5 h-3.5 text-textMuted shrink-0" />
        <span className="truncate">{value}</span>
      </div>
    </div>
  );
};

export default LockedField;
