import React from 'react';
import { Settings, Bot, Brain, Sparkles, Trash2, Wrench, MessageCircle, Users } from 'lucide-react';
import { Agent } from '../../types';
import { isBuiltInAgent } from '../../utils/builtInAgentUtils';
import BuiltInBadge from './BuiltInBadge';
import IntegrationWarningBadge from './IntegrationWarningBadge';
import GuideButton from './GuideButton';
import GuidePanel from './GuidePanel';
import SkillsSection from './SkillsSection';

interface AgentCardProps {
  agent: Agent;
  allAgents: Agent[];
  openGuideId: string | null;
  onToggleGuide: (agentId: string) => void;
  onDelete: (agentId: string) => void;
  onEdit: (agent: Agent) => void;
  onChat: (agent: Agent) => void;
}

const StatusIndicator: React.FC<{ status: string }> = ({ status }) => (
  <span className={`text-xs px-2 py-0.5 rounded-full font-medium flex items-center gap-1.5
      ${status === 'BUSY' ? 'bg-orange-500/20 text-orange-400' : ''}
      ${status === 'IDLE' ? 'bg-emerald-500/20 text-emerald-400' : ''}
      ${status === 'OFFLINE' ? 'bg-gray-500/20 text-gray-400' : ''}
  `}>
      <span className="relative flex h-2 w-2">
        {status === 'BUSY' && <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-orange-400 opacity-75"></span>}
        <span className={`relative inline-flex rounded-full h-2 w-2 ${status === 'BUSY' ? 'bg-orange-500' : status === 'IDLE' ? 'bg-emerald-500' : 'bg-gray-500'}`}></span>
      </span>
      {status}
  </span>
);

const CapabilitiesSection: React.FC<{ capabilities: string[] }> = ({ capabilities }) => (
  <div className="space-y-2">
    <div className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-1.5">
        <Brain className="w-3 h-3" /> Capabilities
    </div>
    <div className="flex flex-wrap gap-1.5">
        {capabilities.length > 0 ? capabilities.map(cap => (
          <span key={cap} className="text-[10px] bg-surfaceHighlight border border-border text-textMuted px-2 py-0.5 rounded">
            {cap}
          </span>
        )) : <span className="text-[10px] text-textMuted italic">None assigned</span>}
    </div>
  </div>
);

const ToolingSection: React.FC<{ toolCategories?: string[]; mcpServerNames?: string[] }> = ({ toolCategories, mcpServerNames }) => (
  <div className="space-y-2">
    <div className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-1.5">
        <Wrench className="w-3 h-3" /> Tooling
    </div>
    <div className="flex flex-wrap gap-1.5">
        {toolCategories && toolCategories.length > 0 ? toolCategories.map(category => (
            <span key={category} className="text-[10px] bg-primary/10 border border-primary/20 text-primary px-2 py-0.5 rounded flex items-center gap-1">
                <Sparkles className="w-2.5 h-2.5" /> {category}
            </span>
        )) : null}
        {mcpServerNames && mcpServerNames.length > 0 ? mcpServerNames.map(serverName => (
            <span key={serverName} className="text-[10px] bg-violet-500/10 border border-violet-500/20 text-violet-400 px-2 py-0.5 rounded flex items-center gap-1">
                <Sparkles className="w-2.5 h-2.5" /> {serverName}
            </span>
        )) : null}
        {(!toolCategories || toolCategories.length === 0) && (!mcpServerNames || mcpServerNames.length === 0) ? <span className="text-[10px] text-textMuted italic">No tools authorized</span> : null}
    </div>
  </div>
);

const ModelSection: React.FC<{ model?: string | null }> = ({ model }) => (
  <div className="space-y-2">
    <div className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-1.5">
        <Bot className="w-3 h-3" /> Model
    </div>
    <div className="flex flex-wrap gap-1.5">
        <span className="text-[10px] bg-surfaceHighlight border border-border text-textMuted px-2 py-0.5 rounded">
            {model ?? 'Default'}
        </span>
    </div>
  </div>
);

const SubAgentsSection: React.FC<{ subAgentIds: string[]; allAgents: Agent[] }> = ({ subAgentIds, allAgents }) => {
  const subAgents = subAgentIds
    .map(id => allAgents.find(a => a.id === id))
    .filter((a): a is Agent => a !== undefined);

  return (
    <div className="space-y-2">
      <div className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-1.5">
        <Users className="w-3 h-3" /> Sub-Agents
      </div>
      <div className="flex flex-wrap gap-1.5">
        {subAgents.length > 0 ? subAgents.map(agent => (
          <span key={agent.id} className="text-[10px] bg-violet-500/10 border border-violet-500/20 text-violet-400 px-2 py-0.5 rounded flex items-center gap-1">
            <Bot className="w-2.5 h-2.5" /> {agent.name}
          </span>
        )) : <span className="text-[10px] text-textMuted italic">None assigned</span>}
      </div>
    </div>
  );
};

const AgentCard: React.FC<AgentCardProps> = ({ agent, allAgents, openGuideId, onToggleGuide, onDelete, onEdit, onChat }) => {
  const builtIn = isBuiltInAgent(agent);

  return (
    <div className="bg-surface border border-border p-6 rounded-lg relative overflow-hidden group hover:border-primary/50 transition-all duration-300 hover:shadow-lg flex flex-col h-full">
        <div className="absolute top-0 left-0 w-1 h-full bg-primary opacity-0 group-hover:opacity-100 transition-opacity" />
        
        <div className="flex justify-between items-start mb-4">
            <div className="flex items-center gap-4">
                <img src={agent.avatarUrl} alt={agent.name} className="w-12 h-12 rounded-full border border-border object-cover bg-surfaceHighlight" />
                <div>
                    <div className="flex items-center gap-2">
                        <h3 className="font-semibold text-text leading-tight">{agent.name}</h3>
                        <BuiltInBadge isBuiltIn={builtIn} />
                    </div>
                    <p className="text-xs text-textMuted">{agent.role}</p>
                </div>
            </div>
        </div>

        <IntegrationWarningBadge integrationStatus={agent.integrationStatus} />

        {builtIn && (
            <GuidePanel isOpen={openGuideId === agent.id} content={agent.guide} />
        )}
        
        <div className="flex-1 space-y-4">
          <CapabilitiesSection capabilities={agent.capabilities} />
          <ToolingSection toolCategories={agent.toolCategories} mcpServerNames={agent.mcpServerNames} />
          <ModelSection model={agent.model} />
          <SkillsSection skills={agent.skills} />
          <SubAgentsSection subAgentIds={agent.subAgentIds ?? []} allAgents={allAgents} />
        </div>

        <div className="pt-4 mt-4 border-t border-border flex items-center justify-between">
          <StatusIndicator status={agent.status} />
          <div className="flex items-center gap-1">
            {builtIn && (
                <GuideButton onClick={() => onToggleGuide(agent.id)} />
            )}
            <button
                onClick={() => onChat(agent)}
                className="text-textMuted hover:text-primary transition-colors p-1.5 rounded hover:bg-surfaceHighlight"
                title="Chat with Agent"
            >
                <MessageCircle className="w-4 h-4" />
            </button>
            <button 
                onClick={() => onDelete(agent.id)}
                className="text-textMuted hover:text-red-500 transition-colors p-1.5 rounded hover:bg-surfaceHighlight"
                title="Delete Agent"
            >
                <Trash2 className="w-4 h-4" />
            </button>
            <button 
                onClick={() => onEdit(agent)}
                className="text-textMuted hover:text-primary transition-colors p-1.5 rounded hover:bg-surfaceHighlight"
                title="Edit Configuration"
            >
                <Settings className="w-4 h-4" />
            </button>
          </div>
        </div>
    </div>
  );
};

export default AgentCard;
