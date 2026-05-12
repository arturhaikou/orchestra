import React from 'react';
import { useDefaultRenderTool } from '@copilotkit/react-core/v2';
import { CopilotChat } from '@copilotkit/react-ui';
import { Agent } from '../../types';

interface AgentChatInnerProps {
  agent: Agent;
}

const AgentChatInner: React.FC<AgentChatInnerProps> = ({ agent }) => {
  useDefaultRenderTool();

  return (
    <CopilotChat
      className="h-full"
      labels={{
        title: agent.name,
        initial: `Hi! I'm **${agent.name}**. How can I help you today?`,
        placeholder: `Message ${agent.name}...`,
      }}
    />
  );
};

export default AgentChatInner;
