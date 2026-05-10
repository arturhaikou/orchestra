import React from 'react';

interface McpServerGridProps {
  children: React.ReactNode;
}

const McpServerGrid: React.FC<McpServerGridProps> = ({ children }) => (
  <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
    {children}
  </div>
);

export default McpServerGrid;
