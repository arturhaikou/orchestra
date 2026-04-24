import React from 'react';
import { Info } from 'lucide-react';

interface GuidePanelProps {
  isOpen: boolean;
  content: string | null | undefined;
}

const GuidePanel: React.FC<GuidePanelProps> = ({ isOpen, content }) => {
  return (
    <div
      className="overflow-hidden transition-all duration-300 ease-in-out"
      style={{ maxHeight: isOpen ? '500px' : '0px' }}
      aria-hidden={!isOpen}
    >
      <div className="bg-blue-500/10 border border-blue-500/20 rounded-lg p-4 text-sm text-blue-400 mt-3 flex items-start gap-2">
        <Info className="w-4 h-4 shrink-0 mt-0.5" />
        <span className="leading-relaxed">
          {content || 'No guide available.'}
        </span>
      </div>
    </div>
  );
};

export default GuidePanel;
