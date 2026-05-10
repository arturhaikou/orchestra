import React, { useState } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { ToolAction } from '../../types';

interface McpToolRowProps {
  tool: ToolAction;
  onToggle: (id: string, enabled: boolean) => void;
}

const dangerBadgeClass: Record<string, string> = {
  Safe: 'bg-green text-green-400 border-green-500/20',
  Moderate: 'bg-yellow text-yellow-400 border-yellow-500/20',
  Destructive: 'bg-red text-red-400 border-red-500/20',
};

interface SchemaParam {
  name: string;
  type: string;
  required?: boolean;
  description?: string;
}

const McpToolRow: React.FC<McpToolRowProps> = ({ tool, onToggle }) => {
  const [expanded, setExpanded] = useState(false);

  const isDisabled = tool.dangerLevel === 'Destructive' && !tool.isEnabled;
  const schemaParams: SchemaParam[] = tool.mcpToolSchema
    ? (JSON.parse(tool.mcpToolSchema) as { parameters: SchemaParam[] }).parameters
    : [];

  const handleToggle = () => {
    if (!isDisabled) onToggle(tool.id, !tool.isEnabled);
  };

  return (
    <div className={`rounded-lg border ${tool.dangerLevel === 'Destructive' ? 'bg-red-500/5 border-red-500/20' : 'bg-surface border-border'}`}>
      <div className="flex items-center gap-3 px-3 py-2.5">
        <button
          onClick={() => setExpanded(e => !e)}
          aria-label="Expand schema"
          className="text-textMuted hover:text-text transition-colors shrink-0"
        >
          {expanded ? <ChevronDown className="w-3.5 h-3.5" /> : <ChevronRight className="w-3.5 h-3.5" />}
        </button>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-sm font-medium text-text">{tool.name}</span>
            <span className="text-[10px] font-bold px-1.5 py-0.5 rounded border bg-indigo-500/10 text-indigo-400 border-indigo-500/20">MCP</span>
            <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded border ${dangerBadgeClass[tool.dangerLevel ?? 'Safe']}`} data-testid="danger-badge">
              {tool.dangerLevel}
            </span>
          </div>
          <p className="text-xs text-textMuted mt-0.5 truncate">{tool.description}</p>
        </div>
        <input
          type="checkbox"
          checked={tool.isEnabled}
          onChange={handleToggle}
          aria-label={`Assign ${tool.name}`}
          aria-disabled={isDisabled ? 'true' : undefined}
          className={`shrink-0 accent-primary ${isDisabled ? 'opacity-50 cursor-not-allowed' : ''}`}
        />
      </div>
      {expanded && schemaParams.length > 0 && (
        <div className="px-9 pb-3 space-y-1">
          {schemaParams.map(param => (
            <div key={param.name} className="flex items-center gap-2 text-xs text-textMuted">
              <span className="font-mono text-text">{param.name}</span>
              <span className="text-textMuted/60">{param.type}</span>
              {param.required && <span className="text-primary">required</span>}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default McpToolRow;
