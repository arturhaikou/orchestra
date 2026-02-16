
import { Tool } from '../types';
import { getToken } from './authService';

// Map providerType to icon name
const getIconForProviderType = (providerType: string): string => {
  const iconMap: Record<string, string> = {
    'INTERNAL': 'settings',
    'TRACKER': 'ticket',
    'CODE': 'code',
    'KNOWLEDGE': 'book',
    'COMMUNICATION': 'message'
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
    const url = new URL(API_BASE_URL);
    url.searchParams.append('workspaceId', workspaceId);

    const response = await fetch(url.toString(), {
      headers: getAuthHeaders()
    });

    if (!response.ok) throw new Error("Backend error");
    
    // API returns ToolCategoryDto[] where each category is a tool
    const categories = await response.json();
    
    if (!Array.isArray(categories)) {
      return [];
    }
    
    // Map each category to a Tool object
    const tools: Tool[] = categories.map((category: any) => ({
      id: category.id,
      name: category.name,
      description: category.description,
      category: category.providerType,
      icon: getIconForProviderType(category.providerType),
      actions: category.actions || []
    }));
    
    return tools;
  } catch (error) {
    console.error('Failed to fetch tools:', error);
    return [];
  }
};
