import React from 'react';
import { X } from 'lucide-react';
import { CopilotKit } from '@copilotkit/react-core';
import { Agent } from '../../types';
import { getToken } from '../../services/authService';
import AgentChatInner from './AgentChatInner';

interface AgentChatModalProps {
  agent: Agent;
  workspaceId: string;
  onClose: () => void;
}

const AgentChatModal: React.FC<AgentChatModalProps> = ({ agent, workspaceId, onClose }) => {
  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm"
    >
      <div className="bg-surface border border-border rounded-xl shadow-2xl flex flex-col w-full max-w-2xl h-[80vh] mx-4 overflow-hidden">
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-border shrink-0">
          <img
            src={agent.avatarUrl}
            alt={agent.name}
            className="w-9 h-9 rounded-full border border-border object-cover bg-surfaceHighlight"
          />
          <div className="flex-1 min-w-0">
            <h2 className="font-semibold text-text leading-tight truncate">{agent.name}</h2>
            <p className="text-xs text-textMuted truncate">{agent.role}</p>
          </div>
          <button
            onClick={onClose}
            className="text-textMuted hover:text-text transition-colors p-1.5 rounded hover:bg-surfaceHighlight shrink-0"
            title="Close chat"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Chat Area */}
        <div className="flex-1 overflow-hidden">
          <CopilotKit
            runtimeUrl={`${import.meta.env.VITE_COPILOTKIT_RUNTIME_URL}/copilotkit`}
            headers={{
              Authorization: `Bearer ${getToken()}`,
              'X-Workspace-Id': workspaceId,
              'X-Agent-Id': agent.id,
            }}
            agent={agent.id}
          >
            <AgentChatInner agent={agent} />
          </CopilotKit>
        </div>
      </div>
    </div>
  );
};

export default AgentChatModal;
