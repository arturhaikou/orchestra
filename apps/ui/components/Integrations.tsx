
import React, { useState, useEffect } from 'react';
import { Plus, Layers, GitBranch, Gitlab, Database, Globe, Loader2, Trash2, AlertTriangle, RefreshCw } from 'lucide-react';
import { Integration, IntegrationType } from '../types';
import { getIntegrations, deleteIntegration } from '../services/integrationService';
import { useParams, useNavigate, Link } from 'react-router-dom';

const Integrations: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const [integrations, setIntegrations] = useState<Integration[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [deleteConfirmationId, setDeleteConfirmationId] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const fetchAll = async () => {
    setIsLoading(true);
    try {
        const data = await getIntegrations(workspaceId);
        setIntegrations(data);
    } catch (e) {
        console.error("Failed to load integrations", e);
    } finally {
        setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchAll();
  }, [workspaceId]);

  const categories = [
    { type: IntegrationType.TRACKER, label: 'Tracker Systems', icon: Layers, color: 'text-indigo-600' },
    { type: IntegrationType.KNOWLEDGE_BASE, label: 'Knowledge Bases', icon: Database, color: 'text-purple-600' },
    { type: IntegrationType.CODE_SOURCE, label: 'Code Sources', icon: GitBranch, color: 'text-blue-600' },
  ];

  const TYPE_LABELS: Record<IntegrationType, string> = {
    [IntegrationType.TRACKER]: 'Tracker',
    [IntegrationType.KNOWLEDGE_BASE]: 'Knowledge Base',
    [IntegrationType.CODE_SOURCE]: 'Code Source',
  };

  const executeDelete = async () => {
    if (!deleteConfirmationId) return;
    setIsDeleting(true);
    try {
        await deleteIntegration(deleteConfirmationId);
        setIntegrations(prev => prev.filter(i => i.id !== deleteConfirmationId));
        setDeleteConfirmationId(null);
    } catch (error) {
        console.error("Failed to delete integration", error);
    } finally {
        setIsDeleting(false);
    }
  };

  const renderIcon = (providerName: string) => {
    switch (providerName) {
      case 'jira': return <Layers className="w-5 h-5 text-indigo-500" />;
      case 'confluence': return <Database className="w-5 h-5 text-blue-400" />;
      case 'github': return <GitBranch className="w-5 h-5 text-orange-500" />;
      case 'gitlab': return <Gitlab className="w-5 h-5 text-orange-500" />;
      default: return <Globe className="w-5 h-5 text-zinc-600" />;
    }
  };

  return (
    <div className="space-y-12 pb-10">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-2xl font-bold text-text tracking-tight">Integrations</h2>
          <p className="text-textMuted text-sm mt-0.5">Manage your development ecosystem connections.</p>
        </div>
        <button 
            onClick={() => navigate('new')}
            className="bg-primary hover:bg-primaryHover text-white px-4 py-2 rounded-lg flex items-center gap-2 text-sm font-semibold transition-all shadow-lg shadow-primary/20 active:scale-95"
        >
            <Plus className="w-4 h-4" /> Add Connection
        </button>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
            <Loader2 className="w-8 h-8 animate-spin text-primary" />
        </div>
      ) : (
        categories.map((category) => {
          const categoryIntegrations = integrations.filter(i => Array.isArray(i.types) && i.types.includes(category.type));
          const Icon = category.icon;
          
          return (
            <div key={category.type} className="animate-fade-in">
              <div className="flex items-center justify-between mb-6 border-b border-border/50 pb-4">
                <div className="flex items-center gap-3">
                  <Icon className={`w-5 h-5 ${category.color}`} />
                  <div className="flex items-center gap-2">
                    <h3 className="text-lg font-bold text-text tracking-tight">{category.label}</h3>
                    <span className="bg-surfaceHighlight text-textMuted text-[10px] font-bold px-1.5 py-0.5 rounded-md border border-border/50 min-w-[22px] text-center">
                      {categoryIntegrations.length}
                    </span>
                  </div>
                </div>
              </div>

              {categoryIntegrations.length === 0 ? (
                <div className="border border-border border-dashed rounded-xl p-8 text-center bg-surface/30">
                  <p className="text-textMuted text-xs">No active {category.label.toLowerCase()} registered.</p>
                </div>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                  {categoryIntegrations.map((integration) => (
                    <div key={integration.id} className="bg-surface border border-border rounded-xl shadow-sm hover:shadow-md transition-all flex flex-col group overflow-hidden">
                      <div className="p-6">
                        <div className="flex justify-between items-start">
                          <div className="flex gap-4 min-w-0">
                            <div className="w-12 h-12 bg-surfaceHighlight border border-border/50 rounded-lg flex items-center justify-center shrink-0 shadow-inner">
                              {renderIcon(integration.provider || integration.icon)}
                            </div>
                            
                            <div className="min-w-0">
                              <h4 className="text-[16px] font-bold text-text mb-0.5 truncate">{integration.name}</h4>
                              <p className="text-[12px] text-textMuted truncate opacity-70 mb-1">
                                  {integration.url || 'No endpoint URL'}
                              </p>
                              <div className="flex flex-wrap gap-2">
                                  <span className="text-[10px] font-mono bg-surfaceHighlight px-1.5 py-0.5 rounded border border-border/50 text-textMuted">
                                      {integration.provider?.toUpperCase() || 'CUSTOM'}
                                  </span>
                                  {(integration.types ?? []).map(type => (
                                    <span key={type} className="text-[10px] font-mono bg-surfaceHighlight px-1.5 py-0.5 rounded border border-border/50 text-textMuted">
                                      {TYPE_LABELS[type] ?? type}
                                    </span>
                                  ))}
                                  {integration.vectorize && (
                                      <span className="text-[10px] font-mono bg-emerald-500/10 text-emerald-500 px-1.5 py-0.5 rounded border border-emerald-500/20 flex items-center gap-1">
                                          <Database className="w-2.5 h-2.5" /> VECTORIZED
                                      </span>
                                  )}
                              </div>
                            </div>
                          </div>

                          <div className={`w-2.5 h-2.5 rounded-full mt-1 ring-4 ring-surface ${integration.connected ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.4)]' : 'bg-red-500'}`} />
                        </div>
                      </div>

                      <div className="h-px bg-border/60 w-full" />

                      <div className="px-6 py-4 flex items-center justify-between bg-surfaceHighlight/30">
                        <div className="flex items-center gap-2 text-[11px] text-textMuted font-medium">
                          <RefreshCw className="w-3 h-3 text-textMuted" />
                          <span>Synced: {integration.lastSync}</span>
                        </div>

                        <div className="flex items-center gap-3">
                          <button 
                              onClick={(e) => { e.stopPropagation(); setDeleteConfirmationId(integration.id); }}
                              className="text-textMuted hover:text-red-500 transition-colors p-1.5 rounded-md hover:bg-red-500/10"
                              title="Delete"
                          >
                              <Trash2 className="w-4 h-4" />
                          </button>
                          <Link
                              to={`${integration.id}/edit`}
                              className="bg-surface border border-border hover:border-primary/50 text-text px-4 py-1.5 rounded-lg text-xs font-bold transition-all shadow-sm active:scale-95 inline-flex items-center"
                          >
                              Configure
                          </Link>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          );
        })
      )}

      {deleteConfirmationId && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/90 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-sm rounded-xl shadow-2xl p-6 space-y-4 animate-scale-in">
             <div className="flex items-center gap-3 text-red-500">
                <AlertTriangle className="w-6 h-6" />
                <h3 className="text-lg font-bold text-text">Sever Connection?</h3>
             </div>
             <p className="text-sm text-textMuted leading-relaxed">
                This will immediately stop all synchronization jobs and revoke access tokens. This action is irreversible.
             </p>
             <div className="flex gap-3 pt-2">
                <button onClick={() => setDeleteConfirmationId(null)} className="flex-1 px-4 py-2 border border-border rounded-lg text-xs font-bold uppercase tracking-widest text-text hover:bg-surfaceHighlight transition-colors">Cancel</button>
                <button onClick={executeDelete} disabled={isDeleting} className="flex-1 px-4 py-2 bg-red-600 text-white rounded-lg text-xs font-bold uppercase tracking-widest shadow-lg shadow-red-500/20 active:scale-95 transition-all">
                    {isDeleting ? <Loader2 className="w-4 h-4 animate-spin mx-auto" /> : 'Confirm'}
                </button>
             </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Integrations;
