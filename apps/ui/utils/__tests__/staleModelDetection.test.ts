import {
  isModelStale,
  detectStaleModels,
  getStaleModelWarningMessage,
} from '../staleModelDetection';

describe('staleModelDetection utility', () => {
  describe('isModelStale', () => {
    it('should return false when savedModelId is undefined', () => {
      const result = isModelStale(undefined, ['model-1', 'model-2']);
      expect(result).toBe(false);
    });

    it('should return false when savedModelId is null', () => {
      const result = isModelStale(null as unknown as undefined, ['model-1', 'model-2']);
      expect(result).toBe(false);
    });

    it('should return false when savedModelId is present in availableModels', () => {
      const result = isModelStale('model-1', ['model-1', 'model-2', 'model-3']);
      expect(result).toBe(false);
    });

    it('should return true when savedModelId is absent from availableModels', () => {
      const result = isModelStale('old-model-x', ['model-2', 'model-3']);
      expect(result).toBe(true);
    });

    it('should be case-sensitive when matching model identifiers', () => {
      const result = isModelStale('Model-1', ['model-1']);
      expect(result).toBe(true);
    });

    it('should return true when availableModels list is empty and savedModelId is defined', () => {
      const result = isModelStale('old-model', []);
      expect(result).toBe(true);
    });
  });

  describe('detectStaleModels', () => {
    it('should detect stale AI Summarization model', () => {
      const result = detectStaleModels(
        'old-model-x',
        'valid-model-y',
        ['valid-model-y', 'another-model']
      );
      expect(result.aiSummarization).toBe(true);
      expect(result.customerSatisfactionAnalysis).toBe(false);
    });

    it('should detect stale Customer Satisfaction Analysis model', () => {
      const result = detectStaleModels(
        'valid-model-x',
        'old-model-y',
        ['valid-model-x', 'another-model']
      );
      expect(result.aiSummarization).toBe(false);
      expect(result.customerSatisfactionAnalysis).toBe(true);
    });

    it('should detect both models as stale', () => {
      const result = detectStaleModels(
        'old-model-x',
        'old-model-y',
        ['valid-model-1', 'valid-model-2']
      );
      expect(result.aiSummarization).toBe(true);
      expect(result.customerSatisfactionAnalysis).toBe(true);
    });

    it('should detect no stale models when both are available', () => {
      const result = detectStaleModels(
        'model-1',
        'model-2',
        ['model-1', 'model-2', 'model-3']
      );
      expect(result.aiSummarization).toBe(false);
      expect(result.customerSatisfactionAnalysis).toBe(false);
    });

    it('should handle undefined model IDs correctly', () => {
      const result = detectStaleModels(
        undefined,
        undefined,
        ['available-model-1', 'available-model-2']
      );
      expect(result.aiSummarization).toBe(false);
      expect(result.customerSatisfactionAnalysis).toBe(false);
    });

    it('should handle partial definition of model IDs', () => {
      const result = detectStaleModels(
        'old-model-x',
        undefined,
        ['valid-model-y']
      );
      expect(result.aiSummarization).toBe(true);
      expect(result.customerSatisfactionAnalysis).toBe(false);
    });
  });

  describe('getStaleModelWarningMessage', () => {
    it('should return a warning message for AI Summarization', () => {
      const message = getStaleModelWarningMessage('AI Summarization');
      expect(message).toContain('AI Summarization');
      expect(message).toContain('no longer available');
      expect(message).toContain('Please select a valid model');
    });

    it('should return a warning message for Customer Satisfaction Analysis', () => {
      const message = getStaleModelWarningMessage('Customer Satisfaction Analysis');
      expect(message).toContain('Customer Satisfaction Analysis');
      expect(message).toContain('no longer available');
    });

    it('should return a consistent message format', () => {
      const message1 = getStaleModelWarningMessage('Feature A');
      const message2 = getStaleModelWarningMessage('Feature B');
      expect(message1).toMatch(/The previously selected model for.+is no longer available/);
      expect(message2).toMatch(/The previously selected model for.+is no longer available/);
    });
  });
});
