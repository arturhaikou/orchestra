
import React, { useState, useEffect } from 'react';
import { Plus, Layers, GitBranch, Database, Globe, X, Save, Check, Loader2, Trash2, AlertTriangle, RefreshCw, Key, Filter, Zap, User, Link as LinkIcon, Search, ChevronDown, Wifi } from 'lucide-react';
import { Integration, IntegrationType } from '../types';
import { getIntegrations, createIntegration, updateIntegration, deleteIntegration, testIntegrationConnection } from '../services/integrationService';
import FilterWarningModal from './FilterWarningModal';

interface IntegrationsProps {
  workspaceId: string;
}

const Integrations: React.FC<IntegrationsProps> = ({ workspaceId }) => {
  const [integrations, setIntegrations] = useState<Integration[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [deleteConfirmationId, setDeleteConfirmationId] = useState<string | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isFilterWarningOpen, setIsFilterWarningOpen] = useState(false);
  const [isTestingConnection, setIsTestingConnection] = useState(false);
  const [connectionTestError, setConnectionTestError] = useState<string | null>(null);
  const [connectionTestSuccess, setConnectionTestSuccess] = useState(false);
  const [testFailed, setTestFailed] = useState(false);

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

  const [formState, setFormState] = useState({
    name: '',
    type: IntegrationType.TRACKER,
    provider: 'jira',
    url: '',
    username: '',
    apiKey: '',
    filterQuery: '',
    vectorize: false,
    jiraType: 'Cloud',
    confluenceType: 'Cloud'
  });

  const categories = [
    { type: IntegrationType.TRACKER, label: 'Tracker Systems', icon: Layers, color: 'text-indigo-600' },
    { type: IntegrationType.KNOWLEDGE_BASE, label: 'Knowledge Bases', icon: Database, color: 'text-purple-600' },
    { type: IntegrationType.CODE_SOURCE, label: 'Code Sources', icon: GitBranch, color: 'text-blue-600' },
  ];

  const providers = [
    { value: 'jira', label: 'Jira' },
    { value: 'confluence', label: 'Confluence' },
    { value: 'github', label: 'GitHub' },
    // Unsupported providers - pending backend implementation
    // { value: 'azure-devops', label: 'Azure DevOps' },
    // { value: 'linear', label: 'Linear' },
    // { value: 'gitlab', label: 'GitLab' },
    // { value: 'notion', label: 'Notion' },
    // { value: 'custom', label: 'Custom' },
  ];

  const getFilterConfig = (provider: string) => {
    switch (provider) {
      case 'jira':
        return {
          label: 'JQL QUERY',
          placeholder: 'e.g. project = "WEB" AND status = "To Do"',
          hint: 'Limit which tickets are synced by providing a specific Jira Query Language string.'
        };
      case 'confluence':
        return {
          label: 'CQL QUERY',
          placeholder: 'e.g. type = "page" AND space = "ENG"',
          hint: 'Limit which pages are synced by providing a specific Confluence Query Language string.'
        };
      case 'github':
        return {
          label: 'SEARCH FILTER',
          placeholder: 'e.g. is:open label:bug state:open',
          hint: 'Limit which issues are synced by providing a GitHub search filter.'
        };
      // Unsupported providers - pending backend implementation
      // case 'azure-devops':
      //   return {
      //     label: 'WIQL QUERY',
      //     placeholder: 'SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = "Nexus"',
      //     hint: 'Limit work items using Work Item Query Language.'
      //   };
      // case 'gitlab':
      //   return {
      //     label: 'SEARCH FILTER',
      //     placeholder: 'e.g. state:opened labels:bug',
      //     hint: 'Limit items using standard GitLab search syntax.'
      //   };
      // case 'notion':
      //   return {
      //     label: 'FILTER JSON',
      //     placeholder: '{ "property": "Status", "select": { "equals": "Done" } }',
      //     hint: 'Provide a valid Notion API filter object in JSON format.'
      //   };
      default:
        return {
          label: 'QUERY FILTER',
          placeholder: 'e.g. status:active type:task',
          hint: 'Limit which items are synced by providing a specific query string.'
        };
    }
  };

  const handleOpenModal = (integration?: Integration) => {
    if (integration) {
      setEditingId(integration.id);
      setFormState({
        name: integration.name,
        type: integration.type,
        provider: (integration.provider || 'custom').toLowerCase(),
        url: integration.url || '',
        username: integration.username || '',
        apiKey: '••••••••••••', // Masked for existing
        filterQuery: integration.filterQuery || '',
        vectorize: integration.vectorize || false,
        jiraType: (integration as any).jiraType || 'Cloud',
        confluenceType: (integration as any).confluenceType || 'Cloud'
      });
    } else {
      setEditingId(null);
      setFormState({
        name: '',
        type: IntegrationType.TRACKER,
        provider: 'jira',
        url: '',
        username: '',
        apiKey: '',
        filterQuery: '',
        vectorize: false,
        jiraType: 'Cloud',
        confluenceType: 'Cloud'
      });
    }
    setIsModalOpen(true);
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

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    
    // Check if filter warning should be shown
    if (shouldShowFilterWarning()) {
      setIsFilterWarningOpen(true);
      return;
    }
    
    // Proceed with saving
    await performSave();
  };

  const shouldShowFilterWarning = (): boolean => {
    const needsFilter = formState.type === IntegrationType.TRACKER || formState.type === IntegrationType.KNOWLEDGE_BASE;
    return needsFilter && !formState.filterQuery.trim();
  };

  const performSave = async () => {
    setIsSaving(true);
    try {
      // Determine connected status: false if test failed, true by default
      const connected = testFailed ? false : true;
      
      if (editingId) {
        const updated = await updateIntegration(editingId, { ...formState, connected });
        setIntegrations(prev => prev.map(item => item.id === editingId ? { ...updated, workspaceId } : item));
      } else {
        const newIntegration = await createIntegration({ ...formState, workspaceId, connected });
        setIntegrations(prev => [...prev, { ...newIntegration, workspaceId }]);
      }
      setIsModalOpen(false);
      setConnectionTestError(null);
      setConnectionTestSuccess(false);
      setTestFailed(false);
    } catch (error) {
      console.error("Failed to save integration", error);
    } finally {
      setIsSaving(false);
    }
  };

  const handleTestConnection = async () => {
    setIsTestingConnection(true);
    setConnectionTestError(null);
    setConnectionTestSuccess(false);
    setTestFailed(false);
    
    try {
      const testRequest: any = {
        provider: formState.provider,
        url: formState.url,
        username: formState.username,
        apiKey: formState.apiKey
      };
      
      // Include jiraType for Jira integrations
      if (formState.provider === 'jira') {
        testRequest.jiraType = formState.jiraType;
      }
      
      // Include confluenceType for Confluence integrations
      if (formState.provider === 'confluence') {
        testRequest.confluenceType = formState.confluenceType;
      }
      
      await testIntegrationConnection(testRequest);
      setConnectionTestSuccess(true);
      setTestFailed(false);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to test connection';
      setConnectionTestError(errorMessage);
      setTestFailed(true);
    } finally {
      setIsTestingConnection(false);
    }
  };

  const renderIcon = (providerName: string) => {
    switch (providerName) {
      case 'jira': return <Layers className="w-5 h-5 text-indigo-500" />;
      case 'confluence': return <Database className="w-5 h-5 text-blue-400" />;
      case 'github': return <GitBranch className="w-5 h-5 text-orange-500" />;
      // Unsupported providers - pending backend implementation
      // case 'linear': return <Zap className="w-5 h-5 text-purple-500" />;
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
            onClick={() => handleOpenModal()}
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
          const categoryIntegrations = integrations.filter(i => i.type === category.type);
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
                          <button 
                              onClick={() => handleOpenModal(integration)}
                              className="bg-surface border border-border hover:border-primary/50 text-text px-4 py-1.5 rounded-lg text-xs font-bold transition-all shadow-sm active:scale-95"
                          >
                              Configure
                          </button>
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

      {/* Connection Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-[480px] rounded-xl shadow-2xl overflow-hidden animate-scale-in flex flex-col max-h-[95vh]">
            <div className="px-6 py-5 flex justify-between items-center bg-surface shrink-0">
              <h3 className="text-xl font-bold text-text">
                {editingId ? 'Edit Integration' : 'New Integration'}
              </h3>
              <button onClick={() => setIsModalOpen(false)} className="text-textMuted hover:text-text transition-colors">
                <X className="w-6 h-6" />
              </button>
            </div>
            
            <form onSubmit={handleSave} className="px-6 pb-6 space-y-4 overflow-y-auto custom-scrollbar">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                   <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Integration Type</label>
                   <div className="relative">
                       <select 
                            value={formState.type} 
                            onChange={(e) => setFormState({...formState, type: e.target.value as IntegrationType})}
                            className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
                        >
                            <option value={IntegrationType.TRACKER}>Tracker System</option>
                            <option value={IntegrationType.KNOWLEDGE_BASE}>Knowledge Base</option>
                            <option value={IntegrationType.CODE_SOURCE}>Code Source</option>
                       </select>
                       <Layers className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
                   </div>
                </div>
                <div className="space-y-1.5">
                   <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Provider</label>
                   <div className="relative">
                       <select 
                            value={formState.provider} 
                            onChange={(e) => setFormState({...formState, provider: e.target.value})}
                            className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
                        >
                            {providers.map(p => <option key={p.value} value={p.value}>{p.label}</option>)}
                       </select>
                       <Globe className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
                   </div>
                </div>
              </div>

              <div className="space-y-1.5">
                  <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Display Name</label>
                  <input 
                  type="text" 
                  value={formState.name}
                  onChange={(e) => setFormState({...formState, name: e.target.value})}
                  className="w-full bg-background border border-border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all shadow-sm placeholder:text-textMuted/40"
                  placeholder="e.g., Company Jira"
                  required
                  />
              </div>

              <div className="space-y-1.5">
                  <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Base URL</label>
                  <input 
                  type="url" 
                  value={formState.url}
                  onChange={(e) => setFormState({...formState, url: e.target.value})}
                  className="w-full bg-background border border-border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all shadow-sm font-mono placeholder:text-textMuted/40"
                  placeholder="https://your-domain.atlassian.net"
                  required
                  />
              </div>

              {formState.provider === 'jira' && (
                <div className="space-y-1.5">
                  <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Jira Instance Type</label>
                  <div className="relative">
                    <select 
                      value={formState.jiraType} 
                      onChange={(e) => setFormState({...formState, jiraType: e.target.value})}
                      className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
                    >
                      <option value="Cloud">Jira Cloud</option>
                      <option value="OnPremise">Jira On-Premise</option>
                    </select>
                    <Globe className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
                  </div>
                  <p className="text-[10px] text-textMuted ml-1">
                    {formState.jiraType === 'Cloud' 
                      ? 'For cloud.atlassian.net instances' 
                      : 'For self-hosted or data center instances'}
                  </p>
                </div>
              )}

              {formState.provider === 'confluence' && (
                <div className="space-y-1.5">
                  <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Confluence Instance Type</label>
                  <div className="relative">
                    <select 
                      value={formState.confluenceType} 
                      onChange={(e) => setFormState({...formState, confluenceType: e.target.value})}
                      className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
                    >
                      <option value="Cloud">Confluence Cloud</option>
                      <option value="OnPremise">Confluence On-Premise</option>
                    </select>
                    <Globe className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
                  </div>
                  <p className="text-[10px] text-textMuted ml-1">
                    {formState.confluenceType === 'Cloud' 
                      ? 'Connects to Atlassian Cloud instance (https://[domain].atlassian.net)' 
                      : 'Connects to self-hosted or Data Center instance'}
                  </p>
                </div>
              )}

              <div className="space-y-1.5">
                  <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Username / Email</label>
                  <div className="relative">
                      <User className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted" />
                      <input 
                      type="text" 
                      value={formState.username}
                      onChange={(e) => setFormState({...formState, username: e.target.value})}
                      className="w-full bg-background border border-border rounded-lg pl-10 pr-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all shadow-sm placeholder:text-textMuted/40"
                      placeholder="user@example.com"
                      />
                  </div>
              </div>

              <div className="space-y-1.5">
                  <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">API Key / Token</label>
                  <input 
                  type="password" 
                  value={formState.apiKey}
                  onChange={(e) => setFormState({...formState, apiKey: e.target.value})}
                  className="w-full bg-background border border-border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all shadow-sm font-mono placeholder:text-textMuted/40"
                  placeholder="••••••••••••••••"
                  />
                  <p className="text-[10px] text-textMuted ml-1">Keys are encrypted at rest.</p>
              </div>

              {/* Test Connection Button */}
              <div className="pt-2 space-y-2">
                  <button
                    type="button"
                    onClick={handleTestConnection}
                    disabled={isTestingConnection || !formState.url || !formState.apiKey}
                    className="w-full px-4 py-2.5 border border-primary/40 hover:border-primary bg-primary/5 hover:bg-primary/10 text-primary rounded-lg text-sm font-semibold transition-all flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {isTestingConnection ? (
                      <>
                        <Loader2 className="w-4 h-4 animate-spin" />
                        Testing...
                      </>
                    ) : connectionTestSuccess ? (
                      <>
                        <Check className="w-4 h-4 text-emerald-500" />
                        <span className="text-emerald-500">Connection Verified</span>
                      </>
                    ) : (
                      <>
                        <Wifi className="w-4 h-4" />
                        Test Connection
                      </>
                    )}
                  </button>
                  {connectionTestError && (
                    <p className="text-[10px] text-red-500 ml-1">⚠ {connectionTestError}</p>
                  )}
              </div>

              {/* JQL Query / Filter Query Section - Improved with dynamic labeling */}
              {(formState.type === IntegrationType.TRACKER || formState.type === IntegrationType.KNOWLEDGE_BASE) && (
                <div className="space-y-1.5 pt-2 border-t border-border/40">
                    <div className="flex justify-between items-center mb-1">
                        <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">
                            {getFilterConfig(formState.provider).label}
                        </label>
                        <span className="text-[9px] bg-surfaceHighlight text-textMuted px-1.5 py-0.5 rounded uppercase font-bold tracking-tighter">optional</span>
                    </div>
                    <div className="relative">
                        <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted" />
                        <input 
                          type="text"
                          value={formState.filterQuery}
                          onChange={(e) => setFormState({...formState, filterQuery: e.target.value})}
                          className="w-full bg-background border border-border rounded-lg pl-10 pr-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all shadow-sm font-mono placeholder:text-textMuted/40"
                          placeholder={getFilterConfig(formState.provider).placeholder}
                        />
                    </div>
                    <p className="text-[10px] text-textMuted italic ml-1">{getFilterConfig(formState.provider).hint}</p>
                </div>
              )}

              {/* Vectorize toggle ONLY for Knowledge Bases */}
              {formState.type === IntegrationType.KNOWLEDGE_BASE && (
                <div className="pt-2 border-t border-border/40 flex items-center justify-between">
                    <div className="flex flex-col">
                        <span className="text-sm font-bold text-text tracking-tight uppercase">Vectorize Context</span>
                        <span className="text-[10px] text-textMuted">Enable AI indexing for deep knowledge retrieval.</span>
                    </div>
                    <label className="relative inline-flex items-center cursor-pointer">
                        <input 
                            type="checkbox" 
                            className="sr-only peer" 
                            checked={formState.vectorize}
                            onChange={(e) => setFormState({...formState, vectorize: e.target.checked})}
                        />
                        <div className="w-11 h-6 bg-surfaceHighlight peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-border after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-primary"></div>
                    </label>
                </div>
              )}

              <div className="pt-4 flex gap-4">
                 <button 
                   type="button" 
                   onClick={() => setIsModalOpen(false)}
                   className="flex-1 px-4 py-2.5 border border-border rounded-lg text-sm font-bold text-text hover:bg-surfaceHighlight transition-all active:scale-[0.98]"
                 >
                   Cancel
                 </button>
                 <button 
                   type="submit" 
                   disabled={isSaving}
                   className="flex-1 px-4 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-lg text-sm font-bold transition-all shadow-lg shadow-primary/20 flex items-center justify-center gap-2 active:scale-[0.98]"
                 >
                   {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                   {isSaving ? 'Saving...' : editingId ? 'Save Connection' : 'Save Connection'}
                 </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Filter Warning Modal */}
      <FilterWarningModal
        isOpen={isFilterWarningOpen}
        providerName={formState.provider}
        isProcessing={isSaving}
        onCancel={() => setIsFilterWarningOpen(false)}
        onProceed={() => {
          setIsFilterWarningOpen(false);
          performSave();
        }}
      />

      {/* Delete Confirmation */}
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
