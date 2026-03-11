import React from 'react';
import { Loader2 } from 'lucide-react';
import StaleModelWarning from './StaleModelWarning';

interface ModelSelectorProps {
  label: string; // Display name, e.g., "Model for AI Summarization"
  selectedModel: string | undefined; // Currently selected model ID
  availableModels: string[]; // Array of model identifier strings
  onModelChange: (modelId: string) => void; // Callback when user selects a model
  isLoading: boolean; // True while models are being fetched
  error?: string | null; // Error message if model fetch failed
  disabled?: boolean; // True to disable the selector (though not visible in disabled state)
  isStale?: boolean; // True when the saved model is no longer available (optional)
  featureName?: string; // Display name of the AI feature for the warning message (required if isStale is true)
}

const ModelSelector: React.FC<ModelSelectorProps> = ({
  label,
  selectedModel,
  availableModels,
  onModelChange,
  isLoading,
  error,
  disabled = false,
  isStale,
  featureName,
}) => {
  return (
    <div className="space-y-2">
      <label className="text-[10px] font-semibold text-textMuted uppercase">
        {label}
      </label>
      
      {isStale && featureName && (
        <StaleModelWarning featureName={featureName} />
      )}
      
      {isLoading ? (
        // Loading state: show spinner and loading message
        <div className="flex items-center gap-2 px-3 py-2 bg-background border border-border rounded-md text-sm text-textMuted">
          <Loader2 className="w-4 h-4 animate-spin" />
          Loading models...
        </div>
      ) : error ? (
        // Error state: show error message with red border
        <div className="space-y-1">
          <div className="px-3 py-2 bg-background border border-red-500/50 rounded-md text-sm text-red-400">
            Error loading models
          </div>
          <p className="text-[10px] text-red-400">{error}</p>
        </div>
      ) : (
        // Normal state: render dropdown
        <select
          value={selectedModel || ''}
          onChange={(e) => onModelChange(e.target.value)}
          disabled={disabled || availableModels.length === 0}
          className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
        >
          {availableModels.length === 0 ? (
            <option value="">No models available</option>
          ) : (
            <>
              {!selectedModel && <option value="">Select a model...</option>}
              {availableModels.map((model) => (
                <option key={model} value={model}>
                  {model}
                </option>
              ))}
            </>
          )}
        </select>
      )}
    </div>
  );
};

export default ModelSelector;
