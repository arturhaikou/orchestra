
import { Tool, ToolCategory } from '../types';
import { getToken } from './authService';

// Map providerType to icon name
const getIconForProviderType = (providerType: string): string => {
  const iconMap: Record<string, string> = {
    'COMMUNICATION': 'message',
    'CODE': 'code',
    'GITLAB': 'code',
    'INTERNAL': 'settings',
    'KNOWLEDGE': 'book',
    'TRACKER': 'ticket'
  };
  return iconMap[providerType] || 'tool';
};

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/tools`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getTools = async (workspaceId: string): Promise<Tool[]> => {
  try {
    const categories = await getToolCategories(workspaceId);
    return categories.map(c => ({
      id: c.id,
      name: c.name,
      description: c.description,
      category: c.providerType as Tool['category'],
      icon: getIconForProviderType(c.providerType),
      actions: c.actions,
      source: c.source,
      integrationId: c.integrationId,
    }));
  } catch (error) {
    console.error('Failed to fetch tools:', error);
    return [];
  }
};

export const getToolCategories = async (workspaceId: string): Promise<ToolCategory[]> => {
  const url = new URL(API_BASE_URL);
  url.searchParams.append('workspaceId', workspaceId);

  const response = await fetch(url.toString(), { headers: getAuthHeaders() });
  if (!response.ok) throw new Error('Backend error');

  const raw = await response.json();
  if (!Array.isArray(raw)) return [];

  return raw.map((category: any) => ({
    id: category.id,
    name: category.name,
    description: category.description,
    providerType: category.providerType,
    source: category.source ?? 'native',
    integrationId: category.integrationId ?? undefined,
    actions: (category.actions ?? []).map((action: any) => ({
      id: action.id,
      name: action.name,
      description: action.description,
      dangerLevel: action.dangerLevel,
      isEnabled: action.isEnabled ?? true,
      isMcpTool: action.isMcpTool ?? false,
      mcpToolSchema: action.mcpToolSchema ?? undefined,
      integrationId: action.integrationId ?? undefined,
    })),
  }));
};
