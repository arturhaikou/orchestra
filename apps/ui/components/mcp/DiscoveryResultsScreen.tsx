import React, { useState } from 'react';
import { DiscoveredTool, ToolEnablementOverride } from '../../types';

interface DiscoveryResultsScreenProps {
  tools: DiscoveredTool[];
  onConfirm: (overrides: ToolEnablementOverride[]) => void;
  onCancel: () => void;
}

const dangerBadgeClass: Record<string, string> = {
  Safe: 'bg-green-500/10 text-green-400 border-green-500/20',
  Moderate: 'bg-yellow-500/10 text-yellow-400 border-yellow-500/20',
  Destructive: 'bg-red-500/10 text-red-400 border-red-500/20',
};

const DiscoveryResultsScreen: React.FC<DiscoveryResultsScreenProps> = ({ tools, onConfirm, onCancel }) => {
  const [enabled, setEnabled] = useState<Record<string, boolean>>(
    Object.fromEntries(tools.map(t => [t.id ?? t.name, t.dangerLevel !== 'Destructive']))
  );

  const handleConfirm = () => {
    const overrides: ToolEnablementOverride[] = tools.map(t => ({
      toolId: t.id,
      enabled: enabled[t.id ?? t.name] ?? false,
    }));
    onConfirm(overrides);
  };

  const handleToggle = (toolId: string, checked: boolean) => {
    setEnabled(prev => ({ ...prev, [toolId]: checked }));
  };

  if (tools.length === 0) {
    return (
      <div data-testid="discovery-results" className="py-8 text-center text-sm text-textMuted">
        No tools discovered. The server responded but reported 0 capabilities.
      </div>
    );
  }

  const selectedCount = Object.values(enabled).filter(Boolean).length;

  return (
    <div data-testid="discovery-results" className="space-y-3">
      <p className="text-sm text-textMuted">{selectedCount} of {tools.length} tools selected</p>
      <ul className="space-y-2">
        {tools.map(tool => (
          <li
            key={tool.id ?? tool.name}
            className={`flex items-start justify-between gap-3 p-3 rounded-lg border ${tool.dangerLevel === 'Destructive' ? 'bg-red-500/5 border-red-500/20' : 'bg-surface border-border'}`}
          >
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="text-sm font-medium text-text">{tool.name}</span>
                <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded border ${dangerBadgeClass[tool.dangerLevel] ?? dangerBadgeClass.Safe}`}>{tool.dangerLevel}</span>
              </div>
              <p className="text-xs text-textMuted mt-0.5 truncate">{tool.description}</p>
            </div>
            <input
              type="checkbox"
              id={tool.id ?? tool.name}
              checked={enabled[tool.id ?? tool.name] ?? false}
              onChange={e => handleToggle(tool.id ?? tool.name, e.target.checked)}
              aria-label={tool.dangerLevel === 'Destructive' ? `Enable destructive tool ${tool.name}` : tool.name}
              className="mt-0.5 shrink-0 accent-primary"
            />
          </li>
        ))}
      </ul>
      <div className="flex gap-2 pt-2">
        <button onClick={handleConfirm} className="flex-1 bg-primary text-white rounded-lg py-2.5 text-sm font-medium hover:bg-primary/90 transition-colors">Confirm &amp; Create Integration</button>
        <button onClick={onCancel} className="px-4 py-2.5 text-sm text-textMuted hover:text-text border border-border rounded-lg transition-colors">Cancel</button>
      </div>
    </div>
  );
};

export default DiscoveryResultsScreen;
