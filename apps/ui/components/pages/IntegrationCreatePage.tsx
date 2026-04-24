import React from 'react';
import { Link } from 'react-router-dom';
import { useParams } from 'react-router-dom';
import { Save, Loader2, AlertTriangle, ArrowLeft, Key, Globe, User, Filter, Wifi, Check } from 'lucide-react';
import { IntegrationType } from '../../types';
import { useIntegrationForm } from '../../hooks/useIntegrationForm';
import FilterWarningModal from '../FilterWarningModal';

const TYPE_LABELS: Record<IntegrationType, string> = {
  [IntegrationType.TRACKER]: 'Tracker',
  [IntegrationType.KNOWLEDGE_BASE]: 'Knowledge Base',
  [IntegrationType.CODE_SOURCE]: 'Code Source',
};

const providers = [
  { value: 'jira', label: 'Jira' },
  { value: 'confluence', label: 'Confluence' },
  { value: 'github', label: 'GitHub' },
  { value: 'gitlab', label: 'GitLab' },
];

const IntegrationCreatePage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const {
    formState,
    setFormState,
    isSaving,
    saveError,
    validationErrors,
    typesError,
    isFilterWarningOpen,
    setIsFilterWarningOpen,
    isTestingConnection,
    connectionTestError,
    connectionTestSuccess,
    integrationsListPath,
    filterConfig,
    availableTypes,
    showFilterField,
    showVectorizeToggle,
    handleProviderChange,
    handleTypeToggle,
    handleSave,
    performSave,
    handleCancel,
    handleTestConnection,
  } = useIntegrationForm(workspaceId);

  return (
    <div className="max-w-3xl mx-auto py-8 px-4">
      <Link to={integrationsListPath} className="inline-flex items-center gap-1 text-sm text-textMuted hover:text-text transition-colors mb-4">
        <ArrowLeft className="w-4 h-4" /> Back to Integrations
      </Link>

      <div className="bg-surface border border-border rounded-xl shadow-lg overflow-hidden">
        <div className="px-6 py-4 border-b border-border">
          <h1 className="text-2xl font-bold text-text">New Integration</h1>
          <p className="text-sm text-textMuted mt-0.5">Connect an external service to your workspace.</p>
        </div>

        <form onSubmit={handleSave} className="p-6 space-y-6">
          {saveError && (
            <div className="flex items-center gap-2 p-3 bg-red-500/10 border border-red-500/20 rounded-lg text-red-400 text-sm">
              <AlertTriangle className="w-4 h-4 shrink-0" />
              <span>{saveError}</span>
            </div>
          )}

          <section className="space-y-2">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Provider</label>
            <select
              value={formState.provider}
              onChange={(e) => handleProviderChange(e.target.value)}
              disabled={isSaving}
              className="w-full bg-background border border-border rounded-lg px-3 py-2.5 text-sm text-text focus:outline-none focus:border-primary transition-all"
            >
              {providers.map(p => (
                <option key={p.value} value={p.value}>{p.label}</option>
              ))}
            </select>
          </section>

          <section className="space-y-2">
            <label className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Integration Types</label>
            <div className="flex flex-wrap gap-2">
              {availableTypes.map(type => (
                <button
                  key={type}
                  type="button"
                  onClick={() => handleTypeToggle(type)}
                  disabled={isSaving || availableTypes.length === 1}
                  className={`px-3 py-1.5 rounded-lg text-xs font-semibold border transition-all ${
                    formState.types.includes(type)
                      ? 'bg-primary/10 border-primary/40 text-primary'
                      : 'bg-background border-border text-textMuted hover:border-primary/30'
                  }`}
                >
                  {TYPE_LABELS[type]}
                </button>
              ))}
            </div>
            {typesError && <p className="text-red-400 text-xs mt-1">{typesError}</p>}
          </section>

          <section className="space-y-1.5">
            <label htmlFor="integration-name" className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Display Name</label>
            <input
              id="integration-name"
              type="text"
              value={formState.name}
              onChange={(e) => { setFormState(prev => ({ ...prev, name: e.target.value })); }}
              disabled={isSaving}
              placeholder="e.g., Company Jira"
              className={`w-full bg-background border rounded-lg px-3 py-2.5 text-sm text-text focus:outline-none focus:border-primary transition-all ${validationErrors.name ? 'border-red-500' : 'border-border'}`}
            />
            {validationErrors.name && <p className="text-red-400 text-xs mt-1">{validationErrors.name}</p>}
          </section>

          <section className="space-y-1.5">
            <label htmlFor="integration-url" className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Base URL</label>
            <div className="relative">
              <Globe className="absolute left-3 top-2.5 w-4 h-4 text-textMuted" />
              <input
                id="integration-url"
                type="url"
                value={formState.url}
                onChange={(e) => { setFormState(prev => ({ ...prev, url: e.target.value })); }}
                disabled={isSaving}
                placeholder="https://your-domain.atlassian.net"
                className={`w-full bg-background border rounded-lg pl-10 pr-3 py-2.5 text-sm text-text focus:outline-none focus:border-primary transition-all ${validationErrors.url ? 'border-red-500' : 'border-border'}`}
              />
            </div>
            {validationErrors.url && <p className="text-red-400 text-xs mt-1">{validationErrors.url}</p>}
          </section>

          <section className="space-y-1.5">
            <label htmlFor="integration-username" className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Username / Email (Optional)</label>
            <div className="relative">
              <User className="absolute left-3 top-2.5 w-4 h-4 text-textMuted" />
              <input
                id="integration-username"
                type="text"
                value={formState.username}
                onChange={(e) => setFormState(prev => ({ ...prev, username: e.target.value }))}
                disabled={isSaving}
                placeholder="user@example.com"
                className="w-full bg-background border border-border rounded-lg pl-10 pr-3 py-2.5 text-sm text-text focus:outline-none focus:border-primary transition-all"
              />
            </div>
          </section>

          <section className="space-y-1.5">
            <label htmlFor="integration-apikey" className="text-[10px] font-bold text-textMuted uppercase tracking-widest">API Key / Token</label>
            <div className="relative">
              <Key className="absolute left-3 top-2.5 w-4 h-4 text-textMuted" />
              <input
                id="integration-apikey"
                type="password"
                value={formState.apiKey}
                onChange={(e) => { setFormState(prev => ({ ...prev, apiKey: e.target.value })); }}
                disabled={isSaving}
                placeholder="Enter your API key or token"
                className={`w-full bg-background border rounded-lg pl-10 pr-3 py-2.5 text-sm text-text focus:outline-none focus:border-primary transition-all ${validationErrors.apiKey ? 'border-red-500' : 'border-border'}`}
              />
            </div>
            {validationErrors.apiKey && <p className="text-red-400 text-xs mt-1">{validationErrors.apiKey}</p>}
          </section>

          {showFilterField && (
            <section className="space-y-1.5">
              <label htmlFor="integration-filter" className="text-[10px] font-bold text-textMuted uppercase tracking-widest">{filterConfig.label}</label>
              <div className="relative">
                <Filter className="absolute left-3 top-2.5 w-4 h-4 text-textMuted" />
                <input
                  id="integration-filter"
                  type="text"
                  value={formState.filterQuery}
                  onChange={(e) => { setFormState(prev => ({ ...prev, filterQuery: e.target.value })); }}
                  disabled={isSaving}
                  placeholder={filterConfig.placeholder}
                  className={`w-full bg-background border rounded-lg pl-10 pr-3 py-2.5 text-sm text-text focus:outline-none focus:border-primary transition-all ${validationErrors.filterQuery ? 'border-red-500' : 'border-border'}`}
                />
              </div>
              <p className="text-xs text-textMuted">{filterConfig.hint}</p>
              {validationErrors.filterQuery && <p className="text-red-400 text-xs mt-1">{validationErrors.filterQuery}</p>}
            </section>
          )}

          {showVectorizeToggle && (
            <section className="flex items-center justify-between py-2">
              <div>
                <label className="text-sm font-semibold text-text">Vectorize Content</label>
                <p className="text-xs text-textMuted">Enable AI-powered search over synced knowledge base content.</p>
              </div>
              <button
                type="button"
                onClick={() => setFormState(prev => ({ ...prev, vectorize: !prev.vectorize }))}
                disabled={isSaving}
                className={`relative w-11 h-6 rounded-full transition-colors ${formState.vectorize ? 'bg-primary' : 'bg-border'}`}
              >
                <span className={`absolute top-0.5 left-0.5 w-5 h-5 rounded-full bg-white transition-transform ${formState.vectorize ? 'translate-x-5' : ''}`} />
              </button>
            </section>
          )}

          <section className="border-t border-border pt-4">
            <button
              type="button"
              onClick={handleTestConnection}
              disabled={isTestingConnection || !formState.url || !formState.apiKey || isSaving}
              className="w-full px-4 py-2.5 border border-primary/40 hover:border-primary bg-primary/5 hover:bg-primary/10 text-primary rounded-lg text-sm font-semibold transition-all flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isTestingConnection ? (
                <><Loader2 className="w-4 h-4 animate-spin" /> Testing...</>
              ) : connectionTestSuccess ? (
                <><Check className="w-4 h-4 text-emerald-500" /><span className="text-emerald-500">Connection Verified</span></>
              ) : (
                <><Wifi className="w-4 h-4" /> Test Connection</>
              )}
            </button>
            {connectionTestError && (
              <p className="text-red-400 text-xs mt-2 text-center">{connectionTestError}</p>
            )}
          </section>

          <div className="flex justify-end gap-3 pt-4 border-t border-border">
            <button
              type="button"
              onClick={handleCancel}
              disabled={isSaving}
              className="px-4 py-2.5 border border-border rounded-lg text-sm font-bold text-text hover:bg-surfaceHighlight transition-all active:scale-[0.98]"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="px-4 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-lg text-sm font-bold transition-all shadow-lg shadow-primary/20 flex items-center justify-center gap-2 active:scale-[0.98]"
            >
              {isSaving ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
              {isSaving ? 'Saving...' : 'Save Connection'}
            </button>
          </div>
        </form>
      </div>

      <FilterWarningModal
        isOpen={isFilterWarningOpen}
        providerName={formState.provider}
        isProcessing={isSaving}
        onProceed={() => {
          setIsFilterWarningOpen(false);
          performSave();
        }}
        onCancel={() => setIsFilterWarningOpen(false)}
      />
    </div>
  );
};

export default IntegrationCreatePage;
