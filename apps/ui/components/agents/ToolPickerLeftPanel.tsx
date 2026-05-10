import React from 'react';
import { MousePointerClick } from 'lucide-react';
import CategoryCard from './CategoryCard';
import { getCategoryIconName, getCategoryDescription } from '../../utils/categoryIconMap';
import { McpServer, ToolCatalogueEntry, McpToolFetchState } from '../../types';

export interface ToolPickerLeftPanelProps {
  toolCatalogue: ToolCatalogueEntry[];
  mcpServers: McpServer[];
  activeSourceId: string | null;
  searchTerm: string;
  onSelectSource: (sourceId: string) => void;
  isMcpLoading?: boolean;
  mcpFetchStates?: Record<string, McpToolFetchState>;
  mcpSelectedCounts?: Record<string, number>;
  selectedActionIds?: string[];
}

interface NativeSource { 
  id: string
  name: string
  count: number
  selectedCount: number
}

function deriveNativeSources(catalogue: ToolCatalogueEntry[], selectedActionIds: string[]): NativeSource[] {
  const map = new Map<string, NativeSource>();
  const selectedSet = new Set(selectedActionIds);
  
  for (const entry of catalogue) {
    if (entry.sourceType !== 'native') continue;
    
    const existing = map.get(entry.sourceId);
    if (existing) {
      existing.count += 1;
      if (selectedSet.has(entry.actionId)) {
        existing.selectedCount += 1;
      }
    } else {
      const selectedCount = selectedSet.has(entry.actionId) ? 1 : 0;
      map.set(entry.sourceId, { id: entry.sourceId, name: entry.sourceName, count: 1, selectedCount });
    }
  }
  
  return Array.from(map.values());
}

function matchesSearch(name: string, term: string): boolean {
  return name.toLowerCase().includes(term.toLowerCase());
}

const SectionHeader: React.FC<{ label: string }> = ({ label }) => (
  <div className="sticky top-0 z-10 bg-surface px-4 py-2 text-xs font-semibold text-textMuted uppercase tracking-wide border-b border-border">
    {label}
  </div>
);

const McpIdleHint: React.FC = () => (
  <span data-testid="mcp-idle-hint" className="flex items-center gap-1 text-textMuted">
    <MousePointerClick size={12} aria-hidden="true" />
    <span className="text-xs">Click to discover tools</span>
  </span>
);

const McpEmptyState: React.FC = () => (
  <div
    data-testid="mcp-empty-state"
    className="px-4 py-3 text-sm text-textMuted"
  >
    No MCP servers connected.{' '}
    <span className="text-textMuted">Go to Settings → MCP Servers to add one.</span>
  </div>
);

const NoResultsState: React.FC<{ term: string }> = ({ term }) => (
  <div data-testid="no-results-state" className="px-4 py-3 text-sm text-textMuted">
    No results for &apos;{term}&apos;.
  </div>
);

const ToolPickerLeftPanel: React.FC<ToolPickerLeftPanelProps> = ({
  toolCatalogue, mcpServers, activeSourceId, searchTerm, onSelectSource,
  isMcpLoading = false, mcpFetchStates = {}, mcpSelectedCounts = {}, selectedActionIds = [],
}) => {
  const nativeSources = deriveNativeSources(toolCatalogue, selectedActionIds);
  const filteredNative = nativeSources.filter(s => matchesSearch(s.name, searchTerm));
  const filteredServers = mcpServers.filter(s => matchesSearch(s.name, searchTerm));
  const hasNoResults = searchTerm !== '' && filteredNative.length === 0 && filteredServers.length === 0;

  if (hasNoResults) return <NoResultsState term={searchTerm} />;

  return (
    <div className="flex flex-col h-full">
      {filteredNative.length > 0 && (
        <div>
          <SectionHeader label="Built-in Categories" />
          {filteredNative.map(source => (
            <CategoryCard
              key={source.id}
              sourceId={source.id}
              name={source.name}
              description={getCategoryDescription(source.id, source.name)}
              iconName={getCategoryIconName(source.id)}
              selectedCount={source.selectedCount}
              totalCount={source.count}
              isActive={activeSourceId === source.id}
              onClick={() => onSelectSource(source.id)}
            />
          ))}
        </div>
      )}
      <div>
        <SectionHeader label="MCP Servers" />
        {isMcpLoading ? (
          <div
            role="status"
            data-testid="mcp-section-loading"
            className="flex items-center gap-2 px-4 py-3 text-sm text-textMuted"
            aria-label="Loading MCP server status"
          >
            <span className="animate-spin w-3.5 h-3.5 rounded-full border-2 border-textMuted border-t-transparent" aria-hidden="true" />
            Checking connections…
          </div>
        ) : filteredServers.length === 0 && searchTerm === '' ? (
          <McpEmptyState />
        ) : (
          filteredServers.map(server => {
            const fetchState = mcpFetchStates[server.id] ?? { status: 'idle' };
            const selectedCount = mcpSelectedCounts[server.id] ?? 0;

            const isSuccess = fetchState.status === 'success';
            const selectedCountForBadge = isSuccess ? selectedCount : undefined;
            const totalCountForBadge = isSuccess ? (fetchState as any).tools?.length : undefined;
            const hintNode = !isSuccess ? <McpIdleHint /> : undefined;

            return (
              <CategoryCard
                key={server.id}
                sourceId={server.id}
                name={server.name}
                description={server.name + ' MCP server'}
                iconName="Plug"
                selectedCount={selectedCountForBadge}
                totalCount={totalCountForBadge}
                hint={hintNode}
                isActive={activeSourceId === server.id}
                onClick={() => onSelectSource(server.id)}
              />
            );
          })
        )}
      </div>
    </div>
  );
};

export default ToolPickerLeftPanel;
