import React from 'react';
import { Sparkles } from 'lucide-react';

interface BuiltInBadgeProps {
  isBuiltIn: boolean;
}

const BuiltInBadge: React.FC<BuiltInBadgeProps> = ({ isBuiltIn }) => {
  if (!isBuiltIn) return null;

  return (
    <span
      role="status"
      aria-label="Built-In agent"
      className="text-[10px] bg-primary/10 border border-primary/20 text-primary px-2 py-0.5 rounded flex items-center gap-1"
    >
      <Sparkles className="w-3 h-3" />
      Built-In
    </span>
  );
};

export default BuiltInBadge;
