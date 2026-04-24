import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import { Loader2, AlertTriangle, ArrowLeft } from 'lucide-react';
import { AgentTemplateDto } from '../../types';
import { getAgentTemplates, createAgentFromTemplate } from '../../services/agentService';
import Toast from '../Toast';
import TemplateDetailsCard from '../agents/TemplateDetailsCard';

type PageState = 'LOADING' | 'READY' | 'ALREADY_DEPLOYED' | 'UNMET_PREREQUISITES' | 'NOT_FOUND' | 'ERROR';

const DeployBuiltInAgentPage: React.FC = () => {
  const { workspaceId, templateId } = useParams<{ workspaceId: string; templateId: string }>();
  const navigate = useNavigate();

  const [pageState, setPageState] = useState<PageState>('LOADING');
  const [template, setTemplate] = useState<AgentTemplateDto | null>(null);
  const [isDeploying, setIsDeploying] = useState(false);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const agentsPath = `/workspaces/${workspaceId}/agents`;

  useEffect(() => {
    if (!workspaceId || !templateId) return;
    loadTemplate();
  }, [workspaceId, templateId]);

  const loadTemplate = async () => {
    setPageState('LOADING');
    try {
      const templates = await getAgentTemplates(workspaceId!);
      const found = templates.find(t => t.templateId === templateId);
      if (!found) {
        setPageState('NOT_FOUND');
        return;
      }
      setTemplate(found);
      setPageState(resolvePageState(found));
    } catch (error) {
      setFetchError(error instanceof Error ? error.message : 'Failed to load template');
      setPageState('ERROR');
    }
  };

  const resolvePageState = (t: AgentTemplateDto): PageState => {
    const stateMap: Record<string, PageState> = {
      'AVAILABLE': 'READY',
      'ALREADY_DEPLOYED': 'ALREADY_DEPLOYED',
      'UNAVAILABLE': 'UNMET_PREREQUISITES',
      'ERROR': 'ERROR',
    };
    return stateMap[t.availability.status] ?? 'ERROR';
  };

  const handleDeploy = async () => {
    setIsDeploying(true);
    try {
      await createAgentFromTemplate({ workspaceId: workspaceId!, templateId: templateId!, projectPrinciples: '' });
      setToast({ message: 'Agent deployed successfully', type: 'success' });
      navigate(agentsPath);
    } catch (error) {
      setToast({ message: error instanceof Error ? error.message : 'Failed to deploy agent', type: 'error' });
      setIsDeploying(false);
    }
  };

  const handleCancel = () => navigate(agentsPath);

  if (pageState === 'LOADING') {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex items-center justify-center py-20" data-testid="deploy-page-loading">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
        </div>
      </div>
    );
  }

  if (pageState === 'NOT_FOUND') {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex flex-col items-center justify-center py-20 gap-4">
          <AlertTriangle className="w-12 h-12 text-yellow-500" />
          <h2 className="text-xl font-semibold">Template not found</h2>
          <Link to={agentsPath} className="text-primary hover:underline">Back to Agents</Link>
        </div>
      </div>
    );
  }

  if (pageState === 'ERROR' && !template) {
    return (
      <div className="max-w-4xl mx-auto py-8 px-4">
        <div className="flex flex-col items-center justify-center py-20 gap-4">
          <AlertTriangle className="w-12 h-12 text-red-500" />
          <h2 className="text-xl font-semibold">Failed to load template</h2>
          <Link to={agentsPath} className="text-primary hover:underline">Back to Agents</Link>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-4xl mx-auto py-8 px-4">
      <Link to={agentsPath} className="inline-flex items-center gap-1 text-sm text-textMuted hover:text-text mb-6">
        <ArrowLeft className="w-4 h-4" /> Back to Agents
      </Link>

      <div className="flex items-center justify-between mb-6">
        <h1 className="text-2xl font-bold">Deploy Built-In Agent</h1>
        <StatusBadge pageState={pageState} />
      </div>

      {pageState === 'ALREADY_DEPLOYED' && (
        <div className="bg-yellow-50 border border-yellow-300 text-yellow-800 rounded-lg px-4 py-3 mb-6">
          This template is already active in this workspace.
        </div>
      )}

      {pageState === 'UNMET_PREREQUISITES' && (
        <div className="bg-red-50 border border-red-300 text-red-800 rounded-lg px-4 py-3 mb-6">
          Missing required prerequisites. Please configure the required integrations before deploying.
        </div>
      )}

      {template && <TemplateDetailsCard template={template} />}

      <div className="flex gap-3 mt-8">
        <button
          onClick={handleDeploy}
          disabled={pageState !== 'READY' || isDeploying}
          className="px-4 py-2 bg-primary text-white rounded-lg font-medium hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed inline-flex items-center gap-2"
        >
          {isDeploying && <Loader2 className="w-4 h-4 animate-spin" />}
          {isDeploying ? 'Deploying…' : 'Deploy'}
        </button>
        <button
          onClick={handleCancel}
          disabled={isDeploying}
          className="px-4 py-2 border border-border rounded-lg font-medium hover:bg-surface"
        >
          Cancel
        </button>
      </div>

      {toast && <Toast message={toast.message} type={toast.type} onClose={() => setToast(null)} />}
    </div>
  );
};

const StatusBadge: React.FC<{ pageState: PageState }> = ({ pageState }) => {
  const badges: Record<string, { label: string; className: string }> = {
    READY: { label: 'Available', className: 'bg-blue-100 text-blue-800' },
    ALREADY_DEPLOYED: { label: 'Deployed', className: 'bg-emerald-100 text-emerald-800' },
    UNMET_PREREQUISITES: { label: 'Blocked', className: 'bg-red-100 text-red-800' },
  };
  const badge = badges[pageState];
  if (!badge) return null;
  return <span className={`px-3 py-1 rounded-full text-xs font-semibold ${badge.className}`}>{badge.label}</span>;
};

export default DeployBuiltInAgentPage;
