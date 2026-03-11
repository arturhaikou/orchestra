import React, { useState, useEffect } from 'react';
import { X, Pencil, Loader2 } from 'lucide-react';
import { Workspace } from '../../types';
import ToggleSwitch from '../ToggleSwitch';
import { fetchWorkspaceModels } from '../../services/workspaceService';
import { detectStaleModels } from '../../utils/staleModelDetection';
import ModelSelector from '../ModelSelector';

interface EditWorkspaceModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (name: string, aiSummarization: boolean, customerSatisfactionAnalysis: boolean, aiSummarizationModelId?: string, customerSatisfactionAnalysisModelId?: string) => void;
  workspace: Workspace;
  isProcessing: boolean;
}

const EditWorkspaceModal: React.FC<EditWorkspaceModalProps> = ({
  isOpen,
  onClose,
  onSubmit,
  workspace,
  isProcessing,
}) => {
  const [workspaceName, setWorkspaceName] = useState('');
  const [isAiSummarizationEnabled, setIsAiSummarizationEnabled] = useState(false);
  const [isCustomerSatisfactionAnalysisEnabled, setIsCustomerSatisfactionAnalysisEnabled] = useState(false);
  
  // New state for model selection
  const [aiSummarizationModelId, setAiSummarizationModelId] = useState<string | undefined>(undefined);
  const [customerSatisfactionAnalysisModelId, setCustomerSatisfactionAnalysisModelId] = useState<string | undefined>(undefined);
  
  // New state for model management
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [modelsError, setModelsError] = useState<string | null>(null);
  
  // New state for stale model detection
  const [staleModels, setStaleModels] = useState<{
    aiSummarization: boolean;
    customerSatisfactionAnalysis: boolean;
  }>({
    aiSummarization: false,
    customerSatisfactionAnalysis: false,
  });

  // Fetch available models when modal opens
  useEffect(() => {
    if (isOpen && workspace) {
      setWorkspaceName(workspace.name);
      setIsAiSummarizationEnabled(workspace.isAiSummarizationEnabled);
      setIsCustomerSatisfactionAnalysisEnabled(workspace.isCustomerSatisfactionAnalysisEnabled);
      
      // Initialize model selections from workspace data
      setAiSummarizationModelId(workspace.aiSummarizationModelId);
      setCustomerSatisfactionAnalysisModelId(workspace.customerSatisfactionAnalysisModelId);
      
      // Fetch available models
      fetchModels();
    }
  }, [isOpen, workspace]);

  const fetchModels = async () => {
    setModelsLoading(true);
    setModelsError(null);
    
    try {
      const models = await fetchWorkspaceModels(workspace.id);
      setAvailableModels(models);
      
      // Detect which saved models are no longer available
      const staleStatus = detectStaleModels(
        workspace.aiSummarizationModelId,
        workspace.customerSatisfactionAnalysisModelId,
        models
      );
      setStaleModels(staleStatus);
    } catch (error) {
      setModelsError(
        error instanceof Error ? error.message : 'Failed to load AI models'
      );
    } finally {
      setModelsLoading(false);
    }
  };

  // When a feature toggle is turned off, discard the in-modal selection for that feature
  const handleAiSummarizationToggle = (newValue: boolean) => {
    setIsAiSummarizationEnabled(newValue);
    if (!newValue) {
      setAiSummarizationModelId(undefined);
    }
  };

  // Handle model selection change and clear stale flag when user selects a valid model
  const handleAiSummarizationModelChange = (modelId: string) => {
    setAiSummarizationModelId(modelId);
    if (modelId) {
      // Clear stale flag since user has selected a valid model
      setStaleModels(prev => ({
        ...prev,
        aiSummarization: false,
      }));
    }
  };

  const handleCustomerSatisfactionToggle = (newValue: boolean) => {
    setIsCustomerSatisfactionAnalysisEnabled(newValue);
    if (!newValue) {
      setCustomerSatisfactionAnalysisModelId(undefined);
    }
  };

  // Handle model selection change and clear stale flag when user selects a valid model
  const handleCustomerSatisfactionModelChange = (modelId: string) => {
    setCustomerSatisfactionAnalysisModelId(modelId);
    if (modelId) {
      // Clear stale flag since user has selected a valid model
      setStaleModels(prev => ({
        ...prev,
        customerSatisfactionAnalysis: false,
      }));
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(
      workspaceName, 
      isAiSummarizationEnabled, 
      isCustomerSatisfactionAnalysisEnabled,
      isAiSummarizationEnabled ? aiSummarizationModelId : undefined,
      isCustomerSatisfactionAnalysisEnabled ? customerSatisfactionAnalysisModelId : undefined
    );
  };

  const handleClose = () => {
    setWorkspaceName('');
    setIsAiSummarizationEnabled(false);
    setIsCustomerSatisfactionAnalysisEnabled(false);
    setAiSummarizationModelId(undefined);
    setCustomerSatisfactionAnalysisModelId(undefined);
    setAvailableModels([]);
    setModelsLoading(false);
    setModelsError(null);
    setStaleModels({
      aiSummarization: false,
      customerSatisfactionAnalysis: false,
    });
    onClose();
  };

  if (!isOpen || !workspace) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
      <div className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl overflow-hidden">
        <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50">
          <h3 className="text-lg font-bold text-text flex items-center gap-2">
            <Pencil className="w-4 h-4 text-primary" /> Edit
          </h3>
          <button
            onClick={handleClose}
            className="text-textMuted hover:text-text transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-6">
          <div className="space-y-1.5">
            <label className="text-[10px] font-semibold text-textMuted uppercase">New Name</label>
            <input 
              type="text" 
              value={workspaceName}
              onChange={(e) => setWorkspaceName(e.target.value)}
              placeholder="e.g., Engineering Gamma"
              className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
              autoFocus
            />
          </div>

          <div className="space-y-4 border-t border-border pt-4">
            <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest">AI Features</h4>
            <ToggleSwitch
              id="aiSummarizationEdit"
              checked={isAiSummarizationEnabled}
              onChange={handleAiSummarizationToggle}
              label="Enable AI Summarization"
            />
            {isAiSummarizationEnabled && (
              <div className="ml-4 border-l border-border pl-4">
                <ModelSelector
                  label="Model for AI Summarization"
                  selectedModel={aiSummarizationModelId}
                  availableModels={availableModels}
                  onModelChange={handleAiSummarizationModelChange}
                  isLoading={modelsLoading}
                  error={modelsError}
                  isStale={staleModels.aiSummarization}
                  featureName="AI Summarization"
                />
              </div>
            )}
            
            <ToggleSwitch
              id="customerSatisfactionAnalysisEdit"
              checked={isCustomerSatisfactionAnalysisEnabled}
              onChange={handleCustomerSatisfactionToggle}
              label="Enable Customer Satisfaction Analysis"
            />
            {isCustomerSatisfactionAnalysisEnabled && (
              <div className="ml-4 border-l border-border pl-4">
                <ModelSelector
                  label="Model for Customer Satisfaction Analysis"
                  selectedModel={customerSatisfactionAnalysisModelId}
                  availableModels={availableModels}
                  onModelChange={handleCustomerSatisfactionModelChange}
                  isLoading={modelsLoading}
                  error={modelsError}
                  isStale={staleModels.customerSatisfactionAnalysis}
                  featureName="Customer Satisfaction Analysis"
                />
              </div>
            )}
          </div>

          <div className="pt-2 flex gap-3">
            <button 
              type="button" 
              onClick={handleClose}
              className="flex-1 px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
              disabled={isProcessing}
            >
              Cancel
            </button>
            <button 
              type="submit" 
              disabled={!workspaceName.trim() || isProcessing || modelsLoading || !!modelsError}
              className="flex-1 px-4 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-primary/20"
            >
              {isProcessing ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Save Changes'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default EditWorkspaceModal;
