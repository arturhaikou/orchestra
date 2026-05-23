import {
  isBuiltInAgent,
  hasIntegrationWarning,
  getWarningMessage,
  buildBuiltInUpdatePayload,
} from '../builtInAgentUtils';
import { Agent } from '../../types';

const baseAgent: Agent = {
  id: 'agent-1',
  workspaceId: 'ws-1',
  name: 'Test Agent',
  role: 'Tester',
  status: 'IDLE',
  capabilities: ['testing'],
  toolActionIds: [],
  toolCategories: [],
  avatarUrl: 'https://example.com/avatar.png',
  subAgentIds: [],
};

describe('isBuiltInAgent', () => {
  it('returns true when isBuiltIn flag is true', () => {
    const agent: Agent = { ...baseAgent, isBuiltIn: true, templateId: 'code-review' };
    expect(isBuiltInAgent(agent)).toBe(true);
  });

  it('returns true when templateId is non-null even if isBuiltIn is undefined', () => {
    const agent: Agent = { ...baseAgent, templateId: 'code-review' };
    expect(isBuiltInAgent(agent)).toBe(true);
  });

  it('returns false when templateId is null and isBuiltIn is false', () => {
    const agent: Agent = { ...baseAgent, templateId: null, isBuiltIn: false };
    expect(isBuiltInAgent(agent)).toBe(false);
  });

  it('returns false when templateId is undefined and isBuiltIn is undefined', () => {
    const agent: Agent = { ...baseAgent };
    expect(isBuiltInAgent(agent)).toBe(false);
  });

  it('returns false for a standard custom agent with no template fields', () => {
    expect(isBuiltInAgent(baseAgent)).toBe(false);
  });
});

describe('hasIntegrationWarning', () => {
  it('returns true when built-in agent has missing integration', () => {
    const agent: Agent = {
      ...baseAgent,
      isBuiltIn: true,
      templateId: 'code-review',
      integrationStatus: {
        integrationMissing: true,
        integrationTypeLabel: 'Code Source',
        warningMessage: 'Required Code Source integration is missing.',
      },
    };
    expect(hasIntegrationWarning(agent)).toBe(true);
  });

  it('returns false when built-in agent has active integration', () => {
    const agent: Agent = {
      ...baseAgent,
      isBuiltIn: true,
      templateId: 'code-review',
      integrationStatus: {
        integrationMissing: false,
        integrationTypeLabel: 'Code Source',
        warningMessage: '',
      },
    };
    expect(hasIntegrationWarning(agent)).toBe(false);
  });

  it('returns false when custom agent has no integrationStatus', () => {
    expect(hasIntegrationWarning(baseAgent)).toBe(false);
  });

  it('returns false when integrationStatus is null', () => {
    const agent: Agent = { ...baseAgent, isBuiltIn: true, templateId: 'code-review', integrationStatus: null };
    expect(hasIntegrationWarning(agent)).toBe(false);
  });
});

describe('getWarningMessage', () => {
  it('returns the warning message when integration is missing', () => {
    const agent: Agent = {
      ...baseAgent,
      isBuiltIn: true,
      templateId: 'code-review',
      integrationStatus: {
        integrationMissing: true,
        integrationTypeLabel: 'Code Source',
        warningMessage: 'Required Code Source integration is missing. Restore it in Settings → Integrations to re-enable execution.',
      },
    };
    expect(getWarningMessage(agent)).toBe(
      'Required Code Source integration is missing. Restore it in Settings → Integrations to re-enable execution.'
    );
  });

  it('returns fallback message when warningMessage is empty', () => {
    const agent: Agent = {
      ...baseAgent,
      isBuiltIn: true,
      templateId: 'code-review',
      integrationStatus: {
        integrationMissing: true,
        integrationTypeLabel: 'Code Source',
        warningMessage: '',
      },
    };
    expect(getWarningMessage(agent)).toBe('Required integration is missing.');
  });

  it('returns empty string when no warning exists', () => {
    expect(getWarningMessage(baseAgent)).toBe('');
  });
});

describe('buildBuiltInUpdatePayload', () => {
  it('includes only projectPrinciples when model is unchanged', () => {
    const payload = buildBuiltInUpdatePayload('SOLID principles apply', null, false);
    expect(payload).toEqual({ projectPrinciples: 'SOLID principles apply' });
    expect(payload).not.toHaveProperty('model');
  });

  it('includes model when modelChanged is true', () => {
    const payload = buildBuiltInUpdatePayload('SOLID principles apply', 'gpt-4', true);
    expect(payload).toEqual({ projectPrinciples: 'SOLID principles apply', model: 'gpt-4' });
  });

  it('sets model to null when model is Default and modelChanged is true', () => {
    const payload = buildBuiltInUpdatePayload('Clean code', 'Default', true);
    expect(payload).toEqual({ projectPrinciples: 'Clean code', model: null });
  });
});
