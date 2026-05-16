/**
 * Determines whether a given model ID is a reasoning model.
 * Reasoning models support the "reasoning effort" parameter (low / medium / high).
 *
 * Patterns matched:
 *  - OpenAI o-series: o1, o3, o4, o1-mini, o3-mini, etc.
 *  - Models with "thinking" in the name (e.g. claude-3-7-sonnet-20250219-thinking)
 *  - Models with "reasoning" in the name
 */
export function isReasoningModel(modelId: string): boolean {
  if (!modelId) return false;
  const lower = modelId.toLowerCase();
  return (
    /^o[134](-|$)/.test(lower) ||
    lower.includes('thinking') ||
    lower.includes('reasoning')
  );
}

export const REASONING_EFFORT_OPTIONS = [
  { value: 'low', label: 'Low' },
  { value: 'medium', label: 'Medium' },
  { value: 'high', label: 'High' },
] as const;

export type ReasoningEffort = 'low' | 'medium' | 'high';
