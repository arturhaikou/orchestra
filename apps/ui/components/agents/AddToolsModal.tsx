import React, { useEffect, useCallback, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import { X } from 'lucide-react';
import { McpToolSelection, ToolCatalogueEntry, McpToolFetchState } from '../../types';
import { useToolPickerState } from '../../hooks/useToolPickerState';
import { useToolPickerMcpServers } from '../../hooks/useToolPickerMcpServers';
import { useLazyMcpServerTools } from '../../hooks/useLazyMcpServerTools';
import { useAgentMcpAssignments } from '../../hooks/useAgentMcpAssignments';
import ToolPickerLeftPanel from './ToolPickerLeftPanel';
import ToolPickerRightPanel from './ToolPickerRightPanel';
import DestructiveToolWarningDialog from './DestructiveToolWarningDialog';

export interface AddToolsModalProps {
  isOpen: boolean;
  agentId?: string;
  initialToolActionIds: string[];
  toolCatalogue: ToolCatalogueEntry[];
  workspaceId: string;
  onCommit: (toolActionIds: string[], mcpSelections: McpToolSelection[]) => void;
  onDiscard: () => void;
  openAtSource?: string | null;
  initialMcpSelections?: McpToolSelection[];
}

const toMcpRecord = (selections: McpToolSelection[]): Record<string, string[]> =>
  Object.fromEntries(selections.map(s => [s.mcpServerId, s.toolNames]));

const AddToolsModal: React.FC<AddToolsModalProps> = ({
  isOpen,
  agentId,
  initialToolActionIds,
  toolCatalogue,
  workspaceId,
  onCommit,
  onDiscard,
  openAtSource,
  initialMcpSelections,
}) => {
  const { servers: mcpServers, isLoading: isMcpLoading } = useToolPickerMcpServers(workspaceId, isOpen);
  const { fetchState: mcpFetchState, fetchForServer, retry: retryMcpFetch, getServerState } = useLazyMcpServerTools(workspaceId);
  const { state, dispatch } = useToolPickerState();
  const { assignments: mcpAssignments } = useAgentMcpAssignments(agentId, isOpen);
  const [pendingDestructiveTools, setPendingDestructiveTools] = useState<string[]>([]);

  useEffect(() => {
    if (Object.keys(mcpAssignments).length > 0) {
      dispatch({ type: 'SET_MCP_SNAPSHOT', payload: { mcpSelections: mcpAssignments } });
    }
  }, [mcpAssignments, dispatch]);

  useEffect(() => {
    if (!isOpen) return;
    dispatch({
      type: 'OPEN_MODAL',
      payload: {
        currentSelections: initialToolActionIds,
        initialActiveSourceId: openAtSource ?? null,
        initialMcpSelections: toMcpRecord(initialMcpSelections ?? []),
      },
    });
  }, [isOpen, initialToolActionIds, openAtSource, initialMcpSelections, dispatch]);

  useEffect(() => {
    if (!isOpen) return;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = ''; };
  }, [isOpen]);

  const handleDiscard = useCallback(() => {
    dispatch({ type: 'DISCARD' });
    onDiscard();
  }, [dispatch, onDiscard]);

  const handleEscape = useCallback(
    (e: KeyboardEvent) => { if (e.key === 'Escape') handleDiscard(); },
    [handleDiscard]
  );

  useEffect(() => {
    if (!isOpen) return;
    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [isOpen, handleEscape]);

  const activeSourceTools = state.activeSourceId
    ? toolCatalogue.filter(e => e.sourceId === state.activeSourceId)
    : [];

  const enabledActionIds = activeSourceTools
    .filter(e => e.dangerLevel !== 'Destructive')
    .map(e => e.actionId);

  const destructiveActionIds = activeSourceTools
    .filter(e => e.dangerLevel === 'Destructive')
    .map(e => e.actionId);

  const checkedEnabledCount = enabledActionIds.filter(id =>
    state.working.includes(id)
  ).length;

  const bulkState: 'all' | 'none' | 'indeterminate' =
    checkedEnabledCount === 0
      ? 'none'
      : checkedEnabledCount === enabledActionIds.length
      ? 'all'
      : 'indeterminate';

  const handleCommit = () => {
    dispatch({ type: 'COMMIT' });
    const mcpSelections: McpToolSelection[] = Object.entries(state.mcpWorking).map(
      ([mcpServerId, toolNames]) => ({ mcpServerId, toolNames })
    );
    onCommit(state.working, mcpSelections);
  };

  const activeSourceType: 'native' | 'mcp' | null = state.activeSourceId === null
    ? null
    : mcpServers.some(s => s.id === state.activeSourceId)
    ? 'mcp'
    : 'native';

  const handleToggleTool = useCallback((actionId: string) => {
    if (activeSourceType === 'mcp') {
      const colonIndex = actionId.indexOf(':');
      dispatch({
        type: 'TOGGLE_MCP_TOOL',
        payload: { serverId: actionId.slice(0, colonIndex), toolName: actionId.slice(colonIndex + 1) },
      });
    } else {
      dispatch({ type: 'TOGGLE_TOOL', payload: { actionId } });
    }
  }, [activeSourceType, dispatch]);

  const handleDestructiveToolAttempt = useCallback((actionId: string) => {
    setPendingDestructiveTools([actionId]);
  }, []);

  const handleDestructiveConfirm = useCallback(() => {
    pendingDestructiveTools.forEach(actionId => {
      handleToggleTool(actionId);
    });
    setPendingDestructiveTools([]);
  }, [pendingDestructiveTools, handleToggleTool]);

  const handleDestructiveCancel = useCallback(() => {
    setPendingDestructiveTools([]);
  }, []);

  const activeMcpServer = mcpServers.find(s => s.id === state.activeSourceId);

  const mcpWorkingAsSelection = useMemo(
    () => Object.entries(state.mcpWorking).flatMap(
      ([serverId, tools]) => tools.map(toolName => `${serverId}:${toolName}`)
    ),
    [state.mcpWorking]
  );

  const mcpFetchStates = useMemo((): Record<string, McpToolFetchState> => {
    const result: Record<string, McpToolFetchState> = {};
    for (const server of mcpServers) {
      result[server.id] = getServerState(server.id);
    }
    return result;
  }, [mcpServers, getServerState, mcpFetchState]);

  const mcpSelectedCounts = useMemo((): Record<string, number> => {
    const result: Record<string, number> = {};
    for (const server of mcpServers) {
      result[server.id] = state.mcpWorking[server.id]?.length ?? 0;
    }
    return result;
  }, [mcpServers, state.mcpWorking]);

  if (!isOpen) return null;

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex flex-col bg-black/60 backdrop-blur-sm"
      data-testid="modal-backdrop"
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Add Tools"
        className="flex flex-col flex-1 bg-surface m-4 rounded-xl shadow-2xl overflow-hidden"
      >
        <div className="flex items-center gap-4 px-6 py-4 border-b border-border flex-shrink-0">
          <h2 className="text-lg font-bold text-text whitespace-nowrap">Add Tools</h2>
          <div className="flex-1">
            <input
              type="text"
              value={state.searchTerm}
              onChange={e => dispatch({ type: 'SET_SEARCH', payload: { searchTerm: e.target.value } })}
              placeholder="Search categories and tools…"
              className="w-full px-3 py-1.5 bg-background border border-border rounded-md
                         text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
            />
          </div>
          <button
            type="button"
            onClick={handleDiscard}
            aria-label="Close"
            className="text-textMuted hover:text-text p-1.5 rounded hover:bg-surfaceHighlight transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="flex flex-1 overflow-hidden">
          <div className="w-[280px] flex-shrink-0 border-r border-border overflow-y-auto">
            <ToolPickerLeftPanel
              toolCatalogue={toolCatalogue}
              mcpServers={mcpServers}
              isMcpLoading={isMcpLoading}
              activeSourceId={state.activeSourceId}
              searchTerm={state.searchTerm}
              mcpFetchStates={mcpFetchStates}
              mcpSelectedCounts={mcpSelectedCounts}
              selectedActionIds={state.working}
              onSelectSource={(sourceId) => {
                dispatch({ type: 'SET_ACTIVE_SOURCE', payload: { sourceId } });
                if (mcpServers.some(s => s.id === sourceId)) fetchForServer(sourceId);
              }}
            />
          </div>
          <div className="flex-1 overflow-y-auto">
            <ToolPickerRightPanel
              toolCatalogue={toolCatalogue}
              activeSourceId={state.activeSourceId}
              activeSourceType={activeSourceType}
              searchTerm={state.searchTerm}
              workingSelection={activeSourceType === 'mcp' ? mcpWorkingAsSelection : state.working}
              onToggleTool={handleToggleTool}
              onDestructiveToolAttempt={handleDestructiveToolAttempt}
              activeMcpServerName={activeMcpServer?.name}
              mcpFetchState={mcpFetchState}
              onRetryFetch={retryMcpFetch}
            />
          </div>
        </div>

        <div className="flex items-center justify-between px-6 py-4 border-t border-border flex-shrink-0">
          <div className="flex items-center gap-2" data-bulk-state={bulkState}>
            <button
              type="button"
              disabled={state.activeSourceId === null || (enabledActionIds.length === 0 && destructiveActionIds.length === 0)}
              onClick={() => {
                if (enabledActionIds.length > 0) {
                  dispatch({ type: 'SELECT_ALL', payload: { actionIds: enabledActionIds } });
                }
                if (destructiveActionIds.length > 0) {
                  setPendingDestructiveTools(destructiveActionIds);
                }
              }}
              className="text-sm text-primary underline-offset-2 hover:underline disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Select All
            </button>
            <span className="text-textMuted text-sm">·</span>
            <button
              type="button"
              disabled={state.activeSourceId === null || checkedEnabledCount === 0}
              onClick={() =>
                dispatch({ type: 'DESELECT_ALL', payload: { actionIds: enabledActionIds } })
              }
              className="text-sm text-primary underline-offset-2 hover:underline disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Deselect All
            </button>
          </div>
          <div className="flex items-center gap-4">
            {pendingDestructiveTools.length === 0 && (
              <button
                type="button"
                onClick={handleDiscard}
                className="text-sm text-textMuted hover:text-text"
              >
                Cancel
              </button>
            )}
            <button
              type="button"
              onClick={handleCommit}
              className="px-4 py-2 bg-primary text-white rounded-md text-sm font-medium hover:bg-primaryHover"
            >
              Done
            </button>
          </div>
        </div>
      </div>
      {pendingDestructiveTools.length > 0 && (
        <DestructiveToolWarningDialog
          toolNames={pendingDestructiveTools.map(actionId => {
            const tool = toolCatalogue.find(e => e.actionId === actionId);
            return tool?.actionName ?? actionId;
          })}
          onConfirm={handleDestructiveConfirm}
          onCancel={handleDestructiveCancel}
        />
      )}
    </div>,
    document.body
  );
};

export default AddToolsModal;
