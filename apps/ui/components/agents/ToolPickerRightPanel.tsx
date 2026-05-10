import React from 'react';
import { MousePointerClick } from 'lucide-react';
import { McpFetchedTool, McpToolFetchState, ToolCatalogueEntry } from '../../types';

export interface ToolPickerRightPanelProps {
  toolCatalogue: ToolCatalogueEntry[];
  activeSourceId: string | null;
  activeSourceType: 'native' | 'mcp' | null;
  searchTerm: string;
  workingSelection: string[];
  onToggleTool: (actionId: string) => void;
  mcpFetchState?: McpToolFetchState;
  onRetryFetch?: () => void;
  activeMcpServerName?: string;
  onDestructiveToolAttempt?: (actionId: string) => void;
}

// ---------------------------------------------------------------------------
// Pure helpers
// ---------------------------------------------------------------------------

function matchesSearch(text: string, term: string): boolean {
  if (!term) return true;
  return text.toLowerCase().includes(term.toLowerCase());
}

function filterTools(
  catalogue: ToolCatalogueEntry[],
  sourceId: string,
  searchTerm: string
): ToolCatalogueEntry[] {
  return catalogue
    .filter(e => e.sourceId === sourceId)
    .filter(e => matchesSearch(e.actionName, searchTerm) || matchesSearch(e.actionDescription, searchTerm));
}

function countSelected(tools: ToolCatalogueEntry[], working: string[]): number {
  return tools.filter(t => working.includes(t.actionId)).length;
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

const WelcomeScreen: React.FC = () => (
  <div
    className="flex flex-col items-center justify-center h-full text-center px-8 py-16 text-textMuted"
    data-testid="right-panel-welcome"
  >
    <p className="text-lg font-semibold text-text mb-2">Select a source</p>
    <p className="text-sm">
      Select a category or MCP server from the left panel to see its tools.
    </p>
  </div>
);

const PanelHeader: React.FC<{ sourceName: string; selectionCount: number }> = ({
  sourceName,
  selectionCount,
}) => (
  <div
    className="sticky top-0 z-10 bg-surface px-4 py-3 border-b border-border flex items-center gap-2"
    data-testid="right-panel-header"
  >
    <span className="font-semibold text-text">{sourceName}</span>
    <span className="text-textMuted text-sm">·</span>
    <span className="text-textMuted text-sm">{selectionCount} selected</span>
  </div>
);

const ZeroToolsState: React.FC<{ sourceName: string }> = ({ sourceName }) => (
  <div
    className="px-6 py-10 text-center text-textMuted text-sm"
    data-testid="zero-tools-state"
  >
    No tools are currently available from {sourceName}.
  </div>
);

const NoSearchResultsState: React.FC<{ term: string }> = ({ term }) => (
  <div
    className="px-6 py-10 text-center text-textMuted text-sm"
    data-testid="no-search-results-state"
  >
    No tools match &lsquo;{term}&rsquo;.
  </div>
);

const DangerBadge: React.FC<{ level: ToolCatalogueEntry['dangerLevel'] }> = ({ level }) => {
  const colours: Record<typeof level, string> = {
    Safe: 'bg-green-100 text-green-800',
    Moderate: 'bg-amber-100 text-amber-800',
    Destructive: 'bg-red-100 text-red-800',
  };
  return (
    <span
      className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${colours[level]}`}
      data-testid="danger-badge"
    >
      {level}
    </span>
  );
};

// ---------------------------------------------------------------------------
// MCP state sub-components
// ---------------------------------------------------------------------------

const McpLoadingState: React.FC = () => (
  <div
    className="flex flex-col items-center justify-center h-full py-16"
    data-testid="mcp-loading-state"
    aria-live="polite"
    aria-busy="true"
  >
    <div className="w-8 h-8 border-2 border-primary border-t-transparent rounded-full animate-spin" />
    <p className="mt-3 text-sm text-textMuted">Loading tools…</p>
  </div>
);

const McpIdleState: React.FC = () => (
  <div
    className="flex flex-col items-center justify-center h-full px-8 py-16 text-center"
    data-testid="mcp-idle-state"
    aria-live="polite"
  >
    <MousePointerClick className="w-8 h-8 text-textMuted mb-3" aria-hidden="true" />
    <p className="font-semibold text-text mb-1">Click to discover tools</p>
    <p className="text-sm text-textMuted">Select this server in the left panel to load its available tools.</p>
  </div>
);

const McpEmptyState: React.FC = () => (
  <div
    className="flex flex-col items-center justify-center h-full px-8 py-16 text-center"
    data-testid="mcp-empty-state"
  >
    <span className="text-4xl mb-3">🔌</span>
    <p className="font-semibold text-text mb-1">This MCP server has no tools available</p>
    <p className="text-sm text-textMuted">Check the server connection in MCP Server settings.</p>
  </div>
);

const McpErrorState: React.FC<{ message: string; onRetry: () => void }> = ({ message, onRetry }) => (
  <div
    className="flex flex-col items-center justify-center h-full px-8 py-16 text-center"
    data-testid="mcp-error-state"
  >
    <span className="text-4xl mb-3">⚠️</span>
    <p className="font-semibold text-text mb-1">Unable to reach this server</p>
    <p className="text-sm text-textMuted mb-4">{message}</p>
    <button
      onClick={onRetry}
      className="px-4 py-2 text-sm rounded bg-primary text-white hover:bg-primary/90 transition-colors"
      data-testid="retry-fetch-button"
    >
      Try Again
    </button>
  </div>
);

const McpAuthFailedState: React.FC = () => (
  <div
    className="flex flex-col items-center justify-center h-full px-8 py-16 text-center"
    data-testid="mcp-auth-failed-state"
  >
    <span className="text-4xl mb-3">🔒</span>
    <p className="font-semibold text-text mb-1">Authentication failed</p>
    <p className="text-sm text-textMuted">Verify your API key in MCP Server settings.</p>
  </div>
);

const McpToolRow: React.FC<{
  tool: McpFetchedTool;
  isChecked: boolean;
  onToggle: () => void;
  onDestructiveAttempt?: () => void;
}> = ({ tool, isChecked, onToggle, onDestructiveAttempt }) => {
  const isDestructive = tool.dangerLevel === 'Destructive';
  const isDisabled = isDestructive && !onDestructiveAttempt;
  const handleChange = isDestructive
    ? onDestructiveAttempt
    : onToggle;

  return (
    <li
      className="flex items-start gap-3 px-4 py-3 border-b border-border last:border-0"
      data-testid="mcp-tool-row"
    >
      <input
        type="checkbox"
        className="mt-1 flex-shrink-0 accent-primary disabled:opacity-40"
        checked={isChecked}
        disabled={isDisabled}
        onChange={handleChange}
        aria-label={tool.name}
      />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-medium text-text text-sm">{tool.name}</span>
          <DangerBadge level={tool.dangerLevel} />
        </div>
        {tool.description && (
          <p className="text-xs text-textMuted mt-0.5 leading-relaxed">{tool.description}</p>
        )}
      </div>
    </li>
  );
};

// ---------------------------------------------------------------------------
// Native tool sub-components
// ---------------------------------------------------------------------------

const ToolRow: React.FC<{
  entry: ToolCatalogueEntry;
  isChecked: boolean;
  onToggle: () => void;
  onDestructiveToolAttempt?: (actionId: string) => void;
}> = ({ entry, isChecked, onToggle, onDestructiveToolAttempt }) => {
  const isDestructive = entry.dangerLevel === 'Destructive';
  const isDisabled = isDestructive && !onDestructiveToolAttempt;
  const handleChange = isDestructive
    ? onDestructiveToolAttempt ? () => onDestructiveToolAttempt(entry.actionId) : undefined
    : onToggle;

  return (
    <li
      className={`flex items-start gap-3 px-4 py-3 border-b border-border last:border-0 ${
        isDestructive ? 'bg-red-50/40' : ''
      }`}
      data-testid="tool-row"
    >
      <input
        type="checkbox"
        className="mt-1 flex-shrink-0 accent-primary disabled:opacity-40"
        checked={isChecked}
        disabled={isDisabled}
        onChange={handleChange}
        aria-label={entry.actionName}
      />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-medium text-text text-sm">{entry.actionName}</span>
          <DangerBadge level={entry.dangerLevel} />
        </div>
        <p className="text-xs text-textMuted mt-0.5 leading-relaxed">
          {entry.actionDescription}
        </p>
      </div>
    </li>
  );
};

// ---------------------------------------------------------------------------
// Panel branch components (OCP — extend without modifying)
// ---------------------------------------------------------------------------

const McpServerPanel: React.FC<{
  activeSourceId: string;
  activeMcpServerName?: string;
  mcpFetchState?: McpToolFetchState;
  onRetryFetch?: () => void;
  searchTerm: string;
  workingSelection: string[];
  onToggleTool: (actionId: string) => void;
  onDestructiveToolAttempt?: (actionId: string) => void;
}> = ({ activeSourceId, activeMcpServerName, mcpFetchState, onRetryFetch, searchTerm, workingSelection, onToggleTool, onDestructiveToolAttempt }) => {
  const state = mcpFetchState ?? { status: 'idle' as const };
  const serverName = activeMcpServerName ?? activeSourceId;

  if (state.status === 'idle') return <McpIdleState />;
  if (state.status === 'loading') return <McpLoadingState />;
  if (state.status === 'empty') return <McpEmptyState />;
  if (state.status === 'auth_failed') return <McpAuthFailedState />;
  if (state.status === 'error') {
    return <McpErrorState message={state.message} onRetry={onRetryFetch ?? (() => {})} />;
  }

  const filtered = state.tools.filter(
    t =>
      !searchTerm ||
      t.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      (t.description ?? '').toLowerCase().includes(searchTerm.toLowerCase())
  );
  const selectionCount = state.tools.filter(t =>
    workingSelection.includes(`${activeSourceId}:${t.name}`)
  ).length;

  return (
    <div className="flex flex-col h-full">
      <PanelHeader sourceName={serverName} selectionCount={selectionCount} />
      {filtered.length === 0 && searchTerm ? (
        <NoSearchResultsState term={searchTerm} />
      ) : (
        <ul className="flex-1 overflow-y-auto">
          {filtered.map(tool => (
            <McpToolRow
              key={tool.name}
              tool={tool}
              isChecked={workingSelection.includes(`${activeSourceId}:${tool.name}`)}
              onToggle={() => onToggleTool(`${activeSourceId}:${tool.name}`)}
              onDestructiveAttempt={
                onDestructiveToolAttempt && tool.dangerLevel === 'Destructive'
                  ? () => onDestructiveToolAttempt(`${activeSourceId}:${tool.name}`)
                  : undefined
              }
            />
          ))}
        </ul>
      )}
    </div>
  );
};

const NativeCategoryPanel: React.FC<{
  toolCatalogue: ToolCatalogueEntry[];
  activeSourceId: string;
  searchTerm: string;
  workingSelection: string[];
  onToggleTool: (actionId: string) => void;
  onDestructiveToolAttempt?: (actionId: string) => void;
}> = ({ toolCatalogue, activeSourceId, searchTerm, workingSelection, onToggleTool, onDestructiveToolAttempt }) => {
  const sourceTools = toolCatalogue.filter(e => e.sourceId === activeSourceId);
  const sourceName = sourceTools[0]?.sourceName ?? activeSourceId;

  if (sourceTools.length === 0) return <ZeroToolsState sourceName={sourceName} />;

  const visibleTools = filterTools(toolCatalogue, activeSourceId, searchTerm);
  const selectionCount = countSelected(sourceTools, workingSelection);

  return (
    <div className="flex flex-col h-full">
      <PanelHeader sourceName={sourceName} selectionCount={selectionCount} />
      {visibleTools.length === 0 && searchTerm ? (
        <NoSearchResultsState term={searchTerm} />
      ) : (
        <ul className="flex-1 overflow-y-auto">
          {visibleTools.map(entry => (
            <ToolRow
              key={entry.actionId}
              entry={entry}
              isChecked={workingSelection.includes(entry.actionId)}
              onToggle={() => onToggleTool(entry.actionId)}
              onDestructiveToolAttempt={onDestructiveToolAttempt}
            />
          ))}
        </ul>
      )}
    </div>
  );
};

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

const ToolPickerRightPanel: React.FC<ToolPickerRightPanelProps> = ({
  toolCatalogue,
  activeSourceId,
  activeSourceType,
  searchTerm,
  workingSelection,
  onToggleTool,
  mcpFetchState,
  onRetryFetch,
  activeMcpServerName,
  onDestructiveToolAttempt,
}) => {
  if (activeSourceId === null) return <WelcomeScreen />;

  if (activeSourceType === 'mcp') {
    return (
      <McpServerPanel
        activeSourceId={activeSourceId}
        activeMcpServerName={activeMcpServerName}
        mcpFetchState={mcpFetchState}
        onRetryFetch={onRetryFetch}
        searchTerm={searchTerm}
        workingSelection={workingSelection}
        onToggleTool={onToggleTool}
        onDestructiveToolAttempt={onDestructiveToolAttempt}
      />
    );
  }

  return (
    <NativeCategoryPanel
      toolCatalogue={toolCatalogue}
      activeSourceId={activeSourceId}
      searchTerm={searchTerm}
      workingSelection={workingSelection}
      onToggleTool={onToggleTool}
      onDestructiveToolAttempt={onDestructiveToolAttempt}
    />
  );
};

export default ToolPickerRightPanel;
