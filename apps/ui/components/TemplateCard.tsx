import React from 'react';
import { Bot, CheckCircle, XCircle } from 'lucide-react';
import { AgentTemplateDto } from '../types';

interface TemplateCardProps {
  template: AgentTemplateDto;
  onDeploy: (templateId: string) => void;
  onViewAgent: (agentId: string) => void;
}

const StatusBadge: React.FC<{ status: string }> = ({ status }) => {
  const config = {
    AVAILABLE: { classes: 'bg-emerald-500/20 text-emerald-400', label: 'Available' },
    UNAVAILABLE: { classes: 'bg-red-500/20 text-red-400', label: 'Unavailable' },
    ALREADY_DEPLOYED: { classes: 'bg-surfaceHighlight text-textMuted', label: 'Already Deployed' },
  }[status] ?? { classes: 'bg-surfaceHighlight text-textMuted', label: status };

  return (
    <span
      className={`text-xs px-2 py-0.5 rounded-full font-medium ${config.classes}`}
      role="status"
      aria-label={`Template status: ${config.label}`}
    >
      {config.label}
    </span>
  );
};

const CardAction: React.FC<TemplateCardProps> = ({ template, onDeploy, onViewAgent }) => {
  const { availability } = template;

  if (availability.status === 'AVAILABLE') {
    return (
      <button
        onClick={() => onDeploy(template.templateId)}
        className="bg-primary hover:bg-primaryHover text-white px-4 py-2 rounded-md text-sm w-full transition-colors"
      >
        Deploy →
      </button>
    );
  }

  if (availability.status === 'ALREADY_DEPLOYED' && availability.existingAgentId) {
    return (
      <button
        onClick={() => onViewAgent(availability.existingAgentId!)}
        className="text-primary hover:underline text-sm w-full text-center"
      >
        View Agent →
      </button>
    );
  }

  return (
    <button
      disabled
      aria-disabled="true"
      className="bg-primary text-white px-4 py-2 rounded-md text-sm w-full opacity-50 cursor-not-allowed"
    >
      Deploy →
    </button>
  );
};

const PrerequisiteList: React.FC<{ prerequisites: AgentTemplateDto['prerequisites'] }> = ({
  prerequisites,
}) => {
  if (prerequisites.length === 0) return null;

  return (
    <div className="mb-4">
      <span className="text-[10px] font-bold text-textMuted uppercase tracking-widest">
        Prerequisites
      </span>
      <div className="flex flex-wrap gap-1.5 mt-1.5">
        {prerequisites.map((p) => (
          <span
            key={p.integrationType}
            className={`text-[10px] px-2 py-0.5 rounded-full border flex items-center gap-1 ${
              p.satisfied
                ? 'border-emerald-500/30 text-emerald-400'
                : 'border-red-500/30 text-red-400'
            }`}
          >
            {p.satisfied ? <CheckCircle className="w-3 h-3" /> : <XCircle className="w-3 h-3" />}
            {p.providerName}
          </span>
        ))}
      </div>
    </div>
  );
};

const UnavailableReason: React.FC<{ availability: AgentTemplateDto['availability'] }> = ({
  availability,
}) => {
  if (availability.status !== 'UNAVAILABLE' || !availability.reason) return null;

  return (
    <p className="text-xs text-red-400 mb-3">
      {availability.reason}{' '}
      <a href="/settings/integrations" className="text-primary hover:underline">
        Settings → Integrations
      </a>
    </p>
  );
};

const TemplateCard: React.FC<TemplateCardProps> = ({ template, onDeploy, onViewAgent }) => (
  <div
    className="bg-surface border border-border p-6 rounded-lg hover:border-primary/50 transition-all duration-300 hover:shadow-lg focus-visible:ring-2 focus-visible:ring-primary/50"
    tabIndex={0}
  >
    <div className="flex items-start justify-between mb-4">
      <div className="w-12 h-12 rounded-full bg-primary/10 flex items-center justify-center">
        <Bot className="w-6 h-6 text-primary" />
      </div>
      <StatusBadge status={template.availability.status} />
    </div>

    <h3 className="font-semibold text-text leading-tight mb-1">{template.name}</h3>
    <p className="text-xs text-textMuted mb-4">{template.description}</p>

    <PrerequisiteList prerequisites={template.prerequisites} />
    <UnavailableReason availability={template.availability} />

    <div className="mt-auto">
      <CardAction template={template} onDeploy={onDeploy} onViewAgent={onViewAgent} />
    </div>
  </div>
);

export default TemplateCard;
