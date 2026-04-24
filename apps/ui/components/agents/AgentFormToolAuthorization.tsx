import React from 'react';
import { Search } from 'lucide-react';
import { Tool, ToolAction } from '../../types';

interface AgentFormToolAuthorizationProps {
  toolSearch: string;
  onToolSearchChange: (value: string) => void;
  filteredTools: Tool[];
  isToolSelected: (tool: Tool) => boolean;
  onToggleTool: (toolId: string) => void;
  actionModal: ActionModalState | null;
  onToggleActionId: (actionId: string) => void;
  onConfirmActions: () => void;
  onCancelActions: () => void;
}

export interface ActionModalState {
  toolName: string;
  actions: ToolAction[];
  selectedActionIds: string[];
}

const AgentFormToolAuthorization: React.FC<AgentFormToolAuthorizationProps> = ({
  toolSearch,
  onToolSearchChange,
  filteredTools,
  isToolSelected,
  onToggleTool,
  actionModal,
  onToggleActionId,
  onConfirmActions,
  onCancelActions,
}) => (
  <>
    <section className="space-y-4">
      <h2 className="text-lg font-semibold text-text">Tool Authorization</h2>
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted" />
        <input
          type="text"
          value={toolSearch}
          onChange={e => onToolSearchChange(e.target.value)}
          className="w-full pl-9 pr-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          placeholder="Search tools..."
        />
      </div>
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {filteredTools.map(tool => (
          <button
            key={tool.id}
            type="button"
            onClick={() => onToggleTool(tool.id)}
            className={`p-3 border rounded-lg text-left text-sm transition-colors ${
              isToolSelected(tool)
                ? 'border-primary bg-primary/10 text-text'
                : 'border-border bg-background text-textMuted hover:border-primary/50'
            }`}
          >
            <div className="font-medium">{tool.name}</div>
            <div className="text-xs mt-1 opacity-70">{tool.description}</div>
          </button>
        ))}
      </div>
    </section>

    {actionModal && (
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
        <div className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl p-6 space-y-4">
          <h3 className="text-lg font-bold text-text">
            Configure {actionModal.toolName} Actions
          </h3>
          <div className="space-y-2">
            {actionModal.actions.map(action => (
              <label key={action.id} className="flex items-center gap-2 p-2 rounded hover:bg-surfaceHighlight cursor-pointer">
                <input
                  type="checkbox"
                  checked={actionModal.selectedActionIds.includes(action.id)}
                  onChange={() => onToggleActionId(action.id)}
                  className="rounded border-border"
                />
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <div className="text-sm text-text">{action.name}</div>
                    {action.dangerLevel && action.dangerLevel !== 'safe' && (
                      <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-medium ${
                        action.dangerLevel === 'destructive'
                          ? 'bg-red-500/20 text-red-400 border border-red-500/30'
                          : 'bg-amber-500/20 text-amber-400 border border-amber-500/30'
                      }`}>
                        {action.dangerLevel}
                      </span>
                    )}
                  </div>
                  <div className="text-xs text-textMuted">{action.description}</div>
                </div>
              </label>
            ))}
          </div>
          <div className="flex gap-3 pt-2">
            <button type="button" onClick={onCancelActions} className="flex-1 px-4 py-2 border border-border rounded-md text-sm text-text hover:bg-surfaceHighlight">
              Cancel
            </button>
            <button type="button" onClick={onConfirmActions} className="flex-1 px-4 py-2 bg-primary text-white rounded-md text-sm hover:bg-primaryHover">
              Confirm
            </button>
          </div>
        </div>
      </div>
    )}
  </>
);

export default AgentFormToolAuthorization;
