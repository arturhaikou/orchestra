import React from 'react';

const CatalogueSkeleton: React.FC = () => (
  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
    {Array.from({ length: 4 }).map((_, i) => (
      <div
        key={i}
        className="bg-surfaceHighlight animate-pulse rounded-lg h-48 border border-border"
        aria-hidden="true"
      />
    ))}
  </div>
);

export default CatalogueSkeleton;
