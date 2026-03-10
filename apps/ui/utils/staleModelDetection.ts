/**
 * Utility functions for detecting stale AI model identifiers.
 * A stale model is one that was previously saved to a workspace
 * but is no longer present in the currently available models list.
 */

/**
 * Checks if a single model identifier is stale (absent from available models).
 * 
 * @param savedModelId - The model ID saved in the workspace (may be undefined/null)
 * @param availableModels - The list of currently available model identifiers
 * @returns true if the savedModelId is defined and NOT present in availableModels
 */
export function isModelStale(
  savedModelId: string | undefined,
  availableModels: string[]
): boolean {
  if (!savedModelId) return false;
  return !availableModels.includes(savedModelId);
}

/**
 * Detects stale models for both AI features in a workspace.
 * 
 * @param aiSummarizationModelId - The saved model ID for AI Summarization (may be undefined)
 * @param customerSatisfactionAnalysisModelId - The saved model ID for Customer Satisfaction Analysis (may be undefined)
 * @param availableModels - The list of currently available model identifiers
 * @returns An object with boolean flags indicating which features have stale models
 */
export function detectStaleModels(
  aiSummarizationModelId: string | undefined,
  customerSatisfactionAnalysisModelId: string | undefined,
  availableModels: string[]
): { aiSummarization: boolean; customerSatisfactionAnalysis: boolean } {
  return {
    aiSummarization: isModelStale(aiSummarizationModelId, availableModels),
    customerSatisfactionAnalysis: isModelStale(customerSatisfactionAnalysisModelId, availableModels),
  };
}

/**
 * Gets a human-readable warning message for a stale model.
 * 
 * @param featureName - The name of the AI feature (e.g., "AI Summarization")
 * @returns A warning message string
 */
export function getStaleModelWarningMessage(featureName: string): string {
  return `The previously selected model for ${featureName} is no longer available. Please select a valid model.`;
}
