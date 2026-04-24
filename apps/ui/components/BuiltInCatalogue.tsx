import React, { useEffect, useState } from 'react';
import { getAgentTemplates } from '../services/agentService';
import { Agent, AgentTemplateDto } from '../types';
import { fetchWorkspaceModels } from '../services/workspaceService';
import TemplateCard from './TemplateCard';
import CatalogueSkeleton from './CatalogueSkeleton';
import CatalogueEmptyState from './CatalogueEmptyState';
import DeployBuiltInAgentModal from './DeployBuiltInAgentModal';

interface BuiltInCatalogueProps {
  workspaceId: string;
  onViewAgent: (agentId: string) => void;
  onBack: () => void;
  onAgentDeployed?: (agent: Agent) => void;
}

const BuiltInCatalogue: React.FC<BuiltInCatalogueProps> = ({
  workspaceId,
  onViewAgent,
  onBack,
  onAgentDeployed,
}) => {
  const [templates, setTemplates] = useState<AgentTemplateDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedTemplate, setSelectedTemplate] = useState<AgentTemplateDto | null>(null);
  const [isDeployModalOpen, setIsDeployModalOpen] = useState(false);
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [defaultModel, setDefaultModel] = useState<string | undefined>(undefined);

  const fetchTemplates = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAgentTemplates(workspaceId);
      setTemplates(data);
    } catch {
      setError('Failed to load templates. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTemplates();
  }, [workspaceId]);

  useEffect(() => {
    if (!isDeployModalOpen) return;
    fetchWorkspaceModels(workspaceId)
      .then(models => setAvailableModels(models))
      .catch(() => setAvailableModels([]));
  }, [isDeployModalOpen, workspaceId]);

  const handleDeployClick = (templateId: string) => {
    const template = templates.find(t => t.templateId === templateId);
    if (!template) return;
    setSelectedTemplate(template);
    setIsDeployModalOpen(true);
  };

  const handleDeployed = (agent: Agent) => {
    setIsDeployModalOpen(false);
    setSelectedTemplate(null);
    onAgentDeployed?.(agent);
  };

  const closeDeployModal = () => {
    setIsDeployModalOpen(false);
    setSelectedTemplate(null);
  };

  if (loading) return <CatalogueSkeleton />;

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center p-12 text-center">
        <p className="text-sm text-red-400 mb-4">{error}</p>
        <button
          onClick={fetchTemplates}
          className="text-sm text-primary hover:underline"
        >
          Retry
        </button>
      </div>
    );
  }

  if (templates.length === 0) {
    return <CatalogueEmptyState onBack={onBack} />;
  }

  return (
    <div>
      <div className="mb-6">
        <h2 className="text-2xl font-bold text-text">Deploy Built-In Agent</h2>
        <p className="text-sm text-textMuted mt-1">
          Choose a pre-configured agent template to deploy to your workspace.
        </p>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
        {templates.map((template) => (
          <TemplateCard
            key={template.templateId}
            template={template}
            onDeploy={handleDeployClick}
            onViewAgent={onViewAgent}
          />
        ))}
      </div>
      {selectedTemplate && (
        <DeployBuiltInAgentModal
          isOpen={isDeployModalOpen}
          onClose={closeDeployModal}
          onDeployed={handleDeployed}
          template={selectedTemplate}
          workspaceId={workspaceId}
          availableModels={availableModels}
          defaultModel={defaultModel}
        />
      )}
    </div>
  );
};

export default BuiltInCatalogue;
