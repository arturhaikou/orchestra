import React from 'react';

const McpServerCardSkeleton: React.FC = () => (
  <div className="bg-surface border border-border rounded-xl p-5 flex flex-col gap-3 animate-pulse">
    <div className="h-5 bg-border rounded w-3/5" />
    <div className="flex gap-2">
      <div className="h-4 bg-border rounded-full w-20" />
      <div className="h-4 bg-border rounded-full w-14" />
    </div>
    <div className="h-4 bg-border rounded w-4/5" />
    <div className="h-3 bg-border rounded w-1/2 mt-auto" />
  </div>
);

export default McpServerCardSkeleton;
