import React from 'react';
import { McpServer, ToolCatalogueEntry, McpToolSelection } from '../../types';
import AgentToolSummaryCard from './AgentToolSummaryCard';
import McpToolSourceCard from './McpToolSourceCard';

export interface AgentToolSummarySectionProps {
  toolActionIds: string[];
  toolCatalogue: ToolCatalogueEntry[];
  mcpServers: McpServer[];
  mcpSelections: McpToolSelection[];
  onOpenModal: (sourceId?: string | null) => void;
  onRemoveSource: (sourceId: string) => void;
  onRemoveMcpServer: (serverId: string) => void;
}

interface SourceSummary {
  sourceId: string;
  sourceName: string;
  sourceType: 'native' | 'mcp';
  selectedCount: number;
  totalCount: number;
  connectionStatus?: McpServer['connectionStatus'];
}

interface McpSummary {
  serverId: string;
  serverName: string;
  selectedToolNames: string[];
  totalToolCount: number;
  connectionStatus: McpServer['connectionStatus'];
}

function deriveSourceSummaries(
  toolActionIds: string[],
  toolCatalogue: ToolCatalogueEntry[],
  mcpServers: McpServer[]
): SourceSummary[] {
  const map = new Map<string, SourceSummary>();
  for (const entry of toolCatalogue) {
    if (!map.has(entry.sourceId)) {
      const server = entry.sourceType === 'mcp'
        ? mcpServers.find(s => s.id === entry.sourceId)
        : undefined;
      map.set(entry.sourceId, {
        sourceId: entry.sourceId,
        sourceName: entry.sourceName,
        sourceType: entry.sourceType,
        selectedCount: 0,
        totalCount: 0,
        connectionStatus: server?.connectionStatus,
      });
    }
    map.get(entry.sourceId)!.totalCount += 1;
    if (toolActionIds.includes(entry.actionId)) {
      map.get(entry.sourceId)!.selectedCount += 1;
    }
  }
  return Array.from(map.values()).filter(s => s.selectedCount > 0);
}

function deriveMcpSummaries(
  mcpSelections: McpToolSelection[],
  mcpServers: McpServer[]
): McpSummary[] {
  return mcpSelections
    .filter(selection => selection.toolNames.length > 0)
    .map(selection => {
      const server = mcpServers.find(s => s.id === selection.mcpServerId);
      return {
        serverId: selection.mcpServerId,
        serverName: server?.name ?? selection.mcpServerId,
        selectedToolNames: selection.toolNames,
        totalToolCount: selection.toolNames.length,
        connectionStatus: server?.connectionStatus ?? 'Unverified',
      };
    })
    .filter((summary): summary is McpSummary => !!mcpServers.find(s => s.id === summary.serverId));
}

const EmptyState: React.FC = () => (
  <p className="text-sm text-textMuted py-2">
    No tools selected. Click &apos;Add Tools&apos; to grant this agent tool access.
  </p>
);

const AgentToolSummarySection: React.FC<AgentToolSummarySectionProps> = ({
  toolActionIds,
  toolCatalogue,
  mcpServers,
  mcpSelections,
  onOpenModal,
  onRemoveSource,
  onRemoveMcpServer,
}) => {
  const nativeSummaries = deriveSourceSummaries(toolActionIds, toolCatalogue, mcpServers);
  const mcpSummaries = deriveMcpSummaries(mcpSelections, mcpServers);
  const isEmpty = nativeSummaries.length === 0 && mcpSummaries.length === 0;

  return (
    <section className="space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-text">Tools</h2>
        <button
          type="button"
          data-testid="add-tools-button"
          onClick={() => onOpenModal(null)}
          className="px-3 py-1.5 text-sm font-medium bg-primary text-white rounded-md hover:bg-primaryHover"
        >
          Add Tools
        </button>
      </div>
      {isEmpty ? (
        <EmptyState />
      ) : (
        <div className="flex flex-wrap gap-2">
          {nativeSummaries.map(s => (
            <AgentToolSummaryCard
              key={s.sourceId}
              sourceId={s.sourceId}
              sourceName={s.sourceName}
              selectedCount={s.selectedCount}
              totalCount={s.totalCount}
              connectionStatus={s.connectionStatus}
              onOpen={() => onOpenModal(s.sourceId)}
              onRemove={() => onRemoveSource(s.sourceId)}
            />
          ))}
          {mcpSummaries.map(summary => (
            <McpToolSourceCard
              key={summary.serverId}
              serverId={summary.serverId}
              serverName={summary.serverName}
              selectedToolNames={summary.selectedToolNames}
              totalToolCount={summary.totalToolCount}
              connectionStatus={summary.connectionStatus}
              onEdit={() => onOpenModal(summary.serverId)}
              onRemove={() => onRemoveMcpServer(summary.serverId)}
            />
          ))}
        </div>
      )}
    </section>
  );
};

export default AgentToolSummarySection;
