import { Agent } from '../types';

export function isBuiltInAgent(agent: Agent): boolean {
  return agent.isBuiltIn === true || agent.templateId != null;
}

export function hasIntegrationWarning(agent: Agent): boolean {
  return isBuiltInAgent(agent) && agent.integrationStatus?.integrationMissing === true;
}

export function getWarningMessage(agent: Agent): string {
  if (!hasIntegrationWarning(agent)) return '';
  return agent.integrationStatus?.warningMessage || 'Required integration is missing.';
}

export function buildBuiltInUpdatePayload(
  projectPrinciples: string,
  model: string | null,
  modelChanged: boolean
): Record<string, unknown> {
  const payload: Record<string, unknown> = { projectPrinciples };
  if (modelChanged) {
    payload.model = model === 'Default' ? null : model;
  }
  return payload;
}
