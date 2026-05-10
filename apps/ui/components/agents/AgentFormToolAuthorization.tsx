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
  headerAction?: React.ReactNode;
}

export interface ActionModalState {
  toolName: string;
  actions: ToolAction[];
  selectedActionIds: string[];
}

const dangerBadgeClass: Record<string, string> = {
  Safe: 'bg-emerald-500/10 text-emerald-500 border-emerald-500/20',
  Moderate: 'bg-yellow-500/10 text-yellow-400 border-yellow-500/20',
  Destructive: 'bg-red-500/10 text-red-400 border-red-500/20',
};

const ToolGrid: React.FC<{
  tools: Tool[];
  isToolSelected: (tool: Tool) => boolean;
  onToggleTool: (id: string) => void;
}> = ({ tools, isToolSelected, onToggleTool }) => (
  <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
    {tools.map(tool => (
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
        <div className="flex items-center gap-1.5 flex-wrap mb-1">
          <span className="font-medium">{tool.name}</span>
          {tool.source === 'mcp' && (
            <span className="text-[9px] font-bold px-1 py-0.5 rounded border bg-indigo-500/10 text-indigo-400 border-indigo-500/20">MCP</span>
          )}
        </div>
        <div className="text-xs opacity-70">{tool.description}</div>
      </button>
    ))}
  </div>
);

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
  headerAction,
}) => {
  const nativeTools = filteredTools.filter(t => t.source !== 'mcp');
  const mcpTools = filteredTools.filter(t => t.source === 'mcp');

  return (
    <>
      <section className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-lg font-semibold text-text">Tool Authorization</h2>
          {headerAction}
        </div>
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

        {nativeTools.length > 0 && (
          <ToolGrid tools={nativeTools} isToolSelected={isToolSelected} onToggleTool={onToggleTool} />
        )}

        {mcpTools.length > 0 && (
          <div className="space-y-2">
            <div className="flex items-center gap-2">
              <span className="text-xs font-bold text-textMuted uppercase tracking-widest">MCP Tools</span>
              <div className="flex-1 h-px bg-border/60" />
            </div>
            <ToolGrid tools={mcpTools} isToolSelected={isToolSelected} onToggleTool={onToggleTool} />
          </div>
        )}

        {filteredTools.length === 0 && (
          <p className="text-sm text-textMuted text-center py-4">No tools match your search.</p>
        )}
      </section>

      {actionModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm">
          <div className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl p-6 space-y-4">
            <h3 className="text-lg font-bold text-text">
              Configure {actionModal.toolName} Actions
            </h3>
            <div className="space-y-2">
              {actionModal.actions.map(action => {
                const isNotOptedIn = action.isMcpTool && action.isEnabled === false;
                return (
                  <label
                    key={action.id}
                    className={`flex items-center gap-2 p-2 rounded cursor-pointer ${isNotOptedIn ? 'opacity-50 cursor-not-allowed' : 'hover:bg-surfaceHighlight'}`}
                  >
                    <input
                      type="checkbox"
                      checked={actionModal.selectedActionIds.includes(action.id)}
                      onChange={() => !isNotOptedIn && onToggleActionId(action.id)}
                      disabled={isNotOptedIn}
                      className="rounded border-border accent-primary"
                    />
                    <div className="flex-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <div className="text-sm text-text">{action.name}</div>
                        {action.isMcpTool && (
                          <span className="text-[9px] font-bold px-1 py-0.5 rounded border bg-indigo-500/10 text-indigo-400 border-indigo-500/20">MCP</span>
                        )}
                        {action.dangerLevel && action.dangerLevel !== 'Safe' && (
                          <span className={`text-[9px] font-bold px-1 py-0.5 rounded border ${dangerBadgeClass[action.dangerLevel] ?? ''}`}>
                            {action.dangerLevel}
                          </span>
                        )}
                        {isNotOptedIn && (
                          <span className="text-[9px] text-textMuted italic">not opted-in</span>
                        )}
                      </div>
                      <div className="text-xs text-textMuted">{action.description}</div>
                    </div>
                  </label>
                );
              })}
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
};

export default AgentFormToolAuthorization;
