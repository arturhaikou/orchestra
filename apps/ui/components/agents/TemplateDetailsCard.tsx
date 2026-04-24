import React from 'react';
import { CheckCircle2, XCircle } from 'lucide-react';
import { AgentTemplateDto } from '../../types';

interface TemplateDetailsCardProps {
  template: AgentTemplateDto;
}

const CapabilitiesList: React.FC<{ capabilities: string[] }> = ({ capabilities }) => (
  <div>
    <h3 className="text-sm font-medium mb-2">Capabilities</h3>
    <div className="flex flex-wrap gap-2">
      {capabilities.map(cap => (
        <span key={cap} className="px-2 py-1 bg-primary/10 text-primary text-xs rounded-full">{cap}</span>
      ))}
    </div>
  </div>
);

const PrerequisitesList: React.FC<{ prerequisites: AgentTemplateDto['prerequisites'] }> = ({ prerequisites }) => (
  <div>
    <h3 className="text-sm font-medium mb-2">Prerequisites</h3>
    <ul className="space-y-1">
      {prerequisites.map(prereq => (
        <li key={prereq.providerName} className="flex items-center gap-2 text-sm">
          {prereq.satisfied
            ? <CheckCircle2 className="w-4 h-4 text-emerald-500" />
            : <XCircle className="w-4 h-4 text-red-500" />}
          {prereq.providerName} ({prereq.integrationType})
        </li>
      ))}
    </ul>
  </div>
);

const TemplateDetailsCard: React.FC<TemplateDetailsCardProps> = ({ template }) => (
  <div className="bg-surface border border-border rounded-xl p-6 space-y-4">
    <h2 className="text-xl font-semibold">{template.name}</h2>
    <p className="text-sm text-textMuted">{template.role}</p>
    <p>{template.description}</p>

    <CapabilitiesList capabilities={template.capabilities} />
    <PrerequisitesList prerequisites={template.prerequisites} />

    <div>
      <h3 className="text-sm font-medium mb-1">Tool</h3>
      <p className="text-sm">{template.toolLabel}</p>
    </div>

    <div>
      <h3 className="text-sm font-medium mb-1">Usage Guide</h3>
      <p className="text-sm text-textMuted">{template.usageGuide}</p>
    </div>
  </div>
);

export default TemplateDetailsCard;
