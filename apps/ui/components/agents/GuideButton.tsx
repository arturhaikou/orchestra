import React from 'react';
import { BookOpen } from 'lucide-react';

interface GuideButtonProps {
  onClick: () => void;
}

const GuideButton: React.FC<GuideButtonProps> = ({ onClick }) => {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label="View usage guide"
      className="text-textMuted hover:text-primary transition-colors p-1.5 rounded hover:bg-surfaceHighlight"
    >
      <BookOpen className="w-4 h-4" />
    </button>
  );
};

export default GuideButton;
