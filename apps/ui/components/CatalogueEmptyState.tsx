import React from 'react';
import { LayoutGrid } from 'lucide-react';

interface CatalogueEmptyStateProps {
  onBack: () => void;
}

const CatalogueEmptyState: React.FC<CatalogueEmptyStateProps> = ({ onBack }) => (
  <div className="flex flex-col items-center justify-center p-12 text-center">
    <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center mb-4">
      <LayoutGrid className="w-6 h-6 text-primary" />
    </div>
    <h3 className="font-semibold text-text mb-2">No Templates Available</h3>
    <p className="text-sm text-textMuted mb-6">
      There are no built-in agent templates available at the moment.
    </p>
    <button
      onClick={onBack}
      className="text-sm text-primary hover:underline"
    >
      Back to Deploy Options
    </button>
  </div>
);

export default CatalogueEmptyState;
