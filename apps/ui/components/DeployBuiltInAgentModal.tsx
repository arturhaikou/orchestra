import React, { useState, useEffect, useRef } from 'react';
import { X, Lock, Info, Loader2, AlertTriangle, FileText, Eye, Pencil } from 'lucide-react';
import { marked } from 'marked';
import { Agent, AgentTemplateDto } from '../types';
import { createAgentFromTemplate } from '../services/agentService';
import ModelSelector from './ModelSelector';

interface DeployBuiltInAgentModalProps {
  isOpen: boolean;
  onClose: () => void;
  onDeployed: (agent: Agent) => void;
  template: AgentTemplateDto;
  workspaceId: string;
  availableModels: string[];
  defaultModel: string | undefined;
}

const MarkdownPreview: React.FC<{ content: string; className?: string }> = ({ content, className = '' }) => {
  if (!content) {
    return (
      <div className={`flex items-center justify-center h-[200px] text-textMuted italic ${className}`}>
        <div className="text-center">
          <FileText className="w-8 h-8 mx-auto mb-2 opacity-50" />
          <p>Preview will appear here</p>
        </div>
      </div>
    );
  }

  try {
    const html = marked.parse(content, { breaks: true });
    return (
      <div
        className={`prose prose-sm dark:prose-invert max-w-none ${className}`}
        dangerouslySetInnerHTML={{ __html: typeof html === 'string' ? html : '' }}
      />
    );
  } catch {
    return (
      <div className={`text-red-400 text-sm p-3 bg-red-500/10 rounded border border-red-500/20 ${className}`}>
        Error parsing markdown
      </div>
    );
  }
};

const ReadOnlyField: React.FC<{ label: string; value: string; ariaLabel: string }> = ({ label, value, ariaLabel }) => (
  <div>
    <label className="text-xs font-bold text-textMuted uppercase tracking-widest">{label}</label>
    <div
      className="bg-surfaceHighlight border border-border rounded-md px-3 py-2 text-sm text-textMuted cursor-not-allowed flex items-center gap-2 mt-1"
      aria-label={ariaLabel}
      role="textbox"
      aria-readonly="true"
    >
      <Lock className="w-3.5 h-3.5 text-textMuted flex-shrink-0" />
      <span>{value}</span>
    </div>
  </div>
);

const DeployBuiltInAgentModal: React.FC<DeployBuiltInAgentModalProps> = ({
  isOpen,
  onClose,
  onDeployed,
  template,
  workspaceId,
  availableModels,
  defaultModel,
}) => {
  const [projectPrinciples, setProjectPrinciples] = useState('');
  const [selectedModel, setSelectedModel] = useState(defaultModel ?? 'Default');
  const [isDeploying, setIsDeploying] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [previewMode, setPreviewMode] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const triggerRef = useRef<HTMLElement | null>(null);

  const trimmedPrinciples = projectPrinciples.trim();
  const isValid = trimmedPrinciples.length > 0;

  useEffect(() => {
    if (isOpen) {
      triggerRef.current = document.activeElement as HTMLElement;
      setProjectPrinciples('');
      setSelectedModel(defaultModel ?? 'Default');
      setError(null);
      setPreviewMode(false);
      setIsDeploying(false);
      setTimeout(() => textareaRef.current?.focus(), 100);
    } else {
      triggerRef.current?.focus();
    }
  }, [isOpen, defaultModel]);

  useEffect(() => {
    if (!isOpen) return;
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isDeploying) onClose();
    };
    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [isOpen, isDeploying, onClose]);

  const handleDeploy = async () => {
    if (!isValid || isDeploying) return;
    setIsDeploying(true);
    setError(null);

    try {
      const modelOverride = selectedModel !== 'Default' && selectedModel !== defaultModel
        ? selectedModel
        : undefined;

      const agent = await createAgentFromTemplate({
        workspaceId,
        templateId: template.templateId,
        projectPrinciples: trimmedPrinciples,
        model: modelOverride,
      });

      onDeployed(agent);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to deploy agent. Please try again.');
    } finally {
      setIsDeploying(false);
    }
  };

  const handleTabTrap = (e: React.KeyboardEvent) => {
    if (e.key !== 'Tab') return;
    const focusableElements = e.currentTarget.querySelectorAll<HTMLElement>(
      'button:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
    );
    if (focusableElements.length === 0) return;
    const first = focusableElements[0];
    const last = focusableElements[focusableElements.length - 1];

    if (e.shiftKey && document.activeElement === first) {
      e.preventDefault();
      last.focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      first.focus();
    }
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      onClick={(e) => { if (e.target === e.currentTarget && !isDeploying) onClose(); }}
      role="dialog"
      aria-modal="true"
      aria-label={`Deploy ${template.name}`}
    >
      <div
        className="bg-surface border border-border w-full max-w-2xl rounded-xl shadow-2xl overflow-hidden"
        onKeyDown={handleTabTrap}
      >
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-border">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-full bg-primary/10 flex items-center justify-center">
              <span className="text-primary font-bold text-sm">
                {template.name.charAt(0)}
              </span>
            </div>
            <div>
              <h2 className="text-lg font-bold text-text">{template.name}</h2>
              <span className="text-[10px] bg-primary/10 border border-primary/20 text-primary px-2 py-0.5 rounded">
                Built-In Agent
              </span>
            </div>
          </div>
          <button
            onClick={onClose}
            disabled={isDeploying}
            className="text-textMuted hover:text-text p-1.5 rounded hover:bg-surfaceHighlight transition-colors"
            aria-label="Close modal"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-6 max-h-[70vh] overflow-y-auto">
          {error && (
            <div className="bg-red-500/10 border border-red-500/20 rounded-lg p-4 flex items-start gap-3">
              <AlertTriangle className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
              <p className="text-sm text-red-400">{error}</p>
            </div>
          )}

          <div className="grid grid-cols-2 gap-4">
            <ReadOnlyField label="Name" value={template.name} ariaLabel="Name is locked and cannot be edited" />
            <ReadOnlyField label="Role" value={template.role} ariaLabel="Role is locked and cannot be edited" />
          </div>

          <ReadOnlyField
            label="Capabilities"
            value={template.capabilities.join(', ')}
            ariaLabel="Capabilities are locked and cannot be edited"
          />

          <ReadOnlyField
            label="Tool"
            value={template.toolLabel}
            ariaLabel="Tool is locked and cannot be edited"
          />

          {template.usageGuide && (
            <div className="bg-blue-500/10 border border-blue-500/20 rounded-lg p-4">
              <div className="flex items-start gap-2 mb-2">
                <Info className="w-4 h-4 text-blue-400 flex-shrink-0 mt-0.5" />
                <span className="text-xs font-bold text-blue-400 uppercase tracking-widest">Usage Guide</span>
              </div>
              <MarkdownPreview content={template.usageGuide} className="text-sm text-blue-300" />
            </div>
          )}

          <div>
            <label className="text-xs font-bold text-textMuted uppercase tracking-widest">
              Project Principles <span className="text-red-400">*</span>
            </label>
            <div className="flex gap-1 mt-1 mb-2">
              <button
                onClick={() => setPreviewMode(false)}
                className={`text-xs px-3 py-1 rounded transition-colors ${
                  !previewMode
                    ? 'bg-primary/10 text-primary font-medium'
                    : 'text-textMuted hover:text-text'
                }`}
                aria-pressed={!previewMode}
              >
                <Pencil className="w-3 h-3 inline mr-1" />
                Write
              </button>
              <button
                onClick={() => setPreviewMode(true)}
                className={`text-xs px-3 py-1 rounded transition-colors ${
                  previewMode
                    ? 'bg-primary/10 text-primary font-medium'
                    : 'text-textMuted hover:text-text'
                }`}
                aria-pressed={previewMode}
              >
                <Eye className="w-3 h-3 inline mr-1" />
                Preview
              </button>
            </div>

            {previewMode ? (
              <div className="bg-background border border-border rounded-md p-3 min-h-[200px]">
                <MarkdownPreview content={projectPrinciples} />
              </div>
            ) : (
              <textarea
                ref={textareaRef}
                value={projectPrinciples}
                onChange={(e) => setProjectPrinciples(e.target.value)}
                placeholder="Describe your project's coding standards, review criteria, and principles in Markdown…"
                className="w-full bg-background border border-border rounded-md p-3 font-mono text-sm text-text min-h-[200px] focus:outline-none focus:border-primary resize-y"
                disabled={isDeploying}
                aria-label="Project Principles markdown editor"
                aria-required="true"
              />
            )}

            {!isValid && (
              <p className="text-xs text-red-400 mt-1">Project Principles are required.</p>
            )}
          </div>

          <ModelSelector
            label="AI Model"
            selectedModel={selectedModel}
            availableModels={['Default', ...availableModels]}
            onModelChange={setSelectedModel}
            isLoading={false}
            disabled={isDeploying}
          />
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 p-6 border-t border-border">
          <button
            onClick={onClose}
            disabled={isDeploying}
            className="px-4 py-2 text-sm text-textMuted hover:text-text transition-colors rounded-md hover:bg-surfaceHighlight"
          >
            Cancel
          </button>
          <button
            onClick={handleDeploy}
            disabled={!isValid || isDeploying}
            aria-disabled={!isValid || isDeploying}
            className={`px-6 py-2.5 rounded-md text-sm font-medium shadow-lg shadow-primary/20 transition-all ${
              isValid && !isDeploying
                ? 'bg-primary hover:bg-primaryHover text-white'
                : 'bg-primary text-white opacity-50 cursor-not-allowed'
            }`}
          >
            {isDeploying ? (
              <span className="flex items-center gap-2">
                <Loader2 className="w-4 h-4 animate-spin" />
                Deploying…
              </span>
            ) : (
              'Authorize & Deploy'
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

export default DeployBuiltInAgentModal;
