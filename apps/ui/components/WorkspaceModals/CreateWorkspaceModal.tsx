import React, { useState, useEffect } from 'react';
import { X, Loader2 } from 'lucide-react';
import ToggleSwitch from '../ToggleSwitch';
import ModelSelector from '../ModelSelector';
import { fetchPlatformModels, fetchDefaultModel } from '../../services/workspaceService';

interface CreateWorkspaceModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (
    name: string,
    aiSummarization: boolean,
    customerSatisfactionAnalysis: boolean,
    aiSummarizationModelId?: string,
    customerSatisfactionAnalysisModelId?: string
  ) => void;
  isProcessing: boolean;
  hasExistingWorkspaces: boolean;
}

const CreateWorkspaceModal: React.FC<CreateWorkspaceModalProps> = ({
  isOpen,
  onClose,
  onSubmit,
  isProcessing,
  hasExistingWorkspaces,
}) => {
  const [workspaceName, setWorkspaceName] = useState('');
  const [isAiSummarizationEnabled, setIsAiSummarizationEnabled] = useState(false);
  const [isCustomerSatisfactionAnalysisEnabled, setIsCustomerSatisfactionAnalysisEnabled] = useState(false);

  // Model management state
  const [availableModels, setAvailableModels] = useState<string[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [modelsError, setModelsError] = useState<string | null>(null);
  const [defaultModel, setDefaultModel] = useState<string | null>(null);

  // Selected model state (only includes selections for enabled features)
  const [selectedAiSummarizationModel, setSelectedAiSummarizationModel] = useState<string | undefined>();
  const [selectedCustomerSatisfactionAnalysisModel, setSelectedCustomerSatisfactionAnalysisModel] = useState<string | undefined>();

  // Track whether models have been fetched (lazy-load only once)
  const [hasTriedFetchingModels, setHasTriedFetchingModels] = useState(false);

  // Fetch models and default model on mount or when feature is first toggled on
  const loadModelsIfNotAlready = async () => {
    if (hasTriedFetchingModels || modelsLoading) return;

    setModelsLoading(true);
    setModelsError(null);
    setHasTriedFetchingModels(true);

    try {
      // Fetch default model
      const defaultModelId = await fetchDefaultModel();
      setDefaultModel(defaultModelId);

      // Fetch available models list
      const models = await fetchPlatformModels();
      setAvailableModels(models);

      // Pre-select the default model for both features
      setSelectedAiSummarizationModel(defaultModelId);
      setSelectedCustomerSatisfactionAnalysisModel(defaultModelId);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Failed to load AI models';
      setModelsError(errorMessage);
    } finally {
      setModelsLoading(false);
    }
  };

  // Handle toggling AI Summarization on/off
  const handleAiSummarizationToggle = (newValue: boolean) => {
    setIsAiSummarizationEnabled(newValue);

    if (newValue) {
      // Feature turned on: load models if not already loaded
      if (!hasTriedFetchingModels && !modelsLoading) {
        loadModelsIfNotAlready();
      }
    } else {
      // Feature turned off: discard the in-modal selection for this feature
      setSelectedAiSummarizationModel(undefined);
    }
  };

  // Handle toggling Customer Satisfaction Analysis on/off
  const handleCustomerSatisfactionToggle = (newValue: boolean) => {
    setIsCustomerSatisfactionAnalysisEnabled(newValue);

    if (newValue) {
      // Feature turned on: load models if not already loaded
      if (!hasTriedFetchingModels && !modelsLoading) {
        loadModelsIfNotAlready();
      }
    } else {
      // Feature turned off: discard the in-modal selection for this feature
      setSelectedCustomerSatisfactionAnalysisModel(undefined);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(
      workspaceName,
      isAiSummarizationEnabled,
      isCustomerSatisfactionAnalysisEnabled,
      isAiSummarizationEnabled ? selectedAiSummarizationModel : undefined,
      isCustomerSatisfactionAnalysisEnabled ? selectedCustomerSatisfactionAnalysisModel : undefined
    );

    // Reset form state
    setWorkspaceName('');
    setIsAiSummarizationEnabled(false);
    setIsCustomerSatisfactionAnalysisEnabled(false);
    setSelectedAiSummarizationModel(undefined);
    setSelectedCustomerSatisfactionAnalysisModel(undefined);
    setHasTriedFetchingModels(false);
    setAvailableModels([]);
    setModelsError(null);
    setDefaultModel(null);
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
      <div className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl overflow-hidden">
        <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50">
          <h3 className="text-lg font-bold text-text">
            {hasExistingWorkspaces ? 'New Workspace' : 'Welcome'}
          </h3>
          {hasExistingWorkspaces && (
            <button onClick={onClose} className="text-textMuted hover:text-text transition-colors">
              <X className="w-5 h-5" />
            </button>
          )}
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          <div className="space-y-1.5">
            <label className="text-[10px] font-semibold text-textMuted uppercase">Workspace Name</label>
            <input 
              type="text" 
              value={workspaceName}
              onChange={(e) => setWorkspaceName(e.target.value)}
              placeholder="e.g., Engineering Alpha"
              className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
              autoFocus
            />
          </div>

          <div className="space-y-4 border-t border-border pt-4">
            <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest">AI Features</h4>
            
            {/* AI Summarization Feature Toggle */}
            <div>
              <ToggleSwitch
                id="aiSummarizationCreate"
                checked={isAiSummarizationEnabled}
                onChange={handleAiSummarizationToggle}
                label="Enable AI Summarization"
              />
              
              {/* AI Summarization Model Selector - shown only when toggle is on */}
              {isAiSummarizationEnabled && (
                <div className="mt-2 ml-4">
                  <ModelSelector
                    label="Model for AI Summarization"
                    selectedModel={selectedAiSummarizationModel}
                    availableModels={availableModels}
                    onModelChange={setSelectedAiSummarizationModel}
                    isLoading={modelsLoading}
                    error={modelsError}
                    disabled={isProcessing}
                  />
                </div>
              )}
            </div>

            {/* Customer Satisfaction Analysis Feature Toggle */}
            <div>
              <ToggleSwitch
                id="customerSatisfactionAnalysisCreate"
                checked={isCustomerSatisfactionAnalysisEnabled}
                onChange={handleCustomerSatisfactionToggle}
                label="Enable Customer Satisfaction Analysis"
              />
              
              {/* Customer Satisfaction Analysis Model Selector - shown only when toggle is on */}
              {isCustomerSatisfactionAnalysisEnabled && (
                <div className="mt-2 ml-4">
                  <ModelSelector
                    label="Model for Customer Satisfaction Analysis"
                    selectedModel={selectedCustomerSatisfactionAnalysisModel}
                    availableModels={availableModels}
                    onModelChange={setSelectedCustomerSatisfactionAnalysisModel}
                    isLoading={modelsLoading}
                    error={modelsError}
                    disabled={isProcessing}
                  />
                </div>
              )}
            </div>
          </div>

          {/* Save action disabled if models failed to load AND at least one feature is enabled */}
          {(isAiSummarizationEnabled || isCustomerSatisfactionAnalysisEnabled) && modelsError && (
            <div className="px-3 py-2 bg-red-500/10 border border-red-500/50 rounded-md text-sm text-red-400">
              Unable to load AI models. Please try again or disable AI features.
            </div>
          )}

          <div className="pt-2 flex gap-3">
            {hasExistingWorkspaces && (
              <button 
                type="button" 
                onClick={onClose}
                className="flex-1 px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
                disabled={isProcessing}
              >
                Cancel
              </button>
            )}
            <button 
              type="submit" 
              disabled={!workspaceName.trim() || isProcessing || ((isAiSummarizationEnabled || isCustomerSatisfactionAnalysisEnabled) && modelsError)}
              className="flex-1 px-4 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-primary/20"
            >
              {isProcessing ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Create Workspace'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default CreateWorkspaceModal;
