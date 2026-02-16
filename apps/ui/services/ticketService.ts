
import { Comment, Ticket, TicketPriority, TicketStatus, PaginatedResponse } from '../types';
import { getToken } from './authService';

const API_BASE_URL = `${import.meta.env.VITE_API_URL}/v1/tickets`;

const getAuthHeaders = () => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${getToken() || ''}`
});

export const getTicketStatuses = async (): Promise<TicketStatus[]> => {
  try {
    const response = await fetch(`${API_BASE_URL}/statuses`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || "Failed to fetch statuses");
      } catch {
        throw new Error("API Error");
      }
    }
    return await response.json();
  } catch (error) {
    throw error;
  }
};

export const getTicketPriorities = async (): Promise<TicketPriority[]> => {
  try {
    const response = await fetch(`${API_BASE_URL}/priorities`, {
      headers: getAuthHeaders()
    });

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || "Failed to fetch priorities");
      } catch {
        throw new Error("API Error");
      }
    }
    return await response.json();
  } catch (error) {
    throw error;
  }
};

export const getTickets = async (workspaceId: string, pageToken?: string, pageSize: number = 2): Promise<PaginatedResponse<Ticket>> => {
  try {
    const url = new URL(API_BASE_URL);
    url.searchParams.append('workspaceId', workspaceId);
    if (pageToken) url.searchParams.append('pageToken', pageToken);
    url.searchParams.append('pageSize', pageSize.toString());

    const response = await fetch(url.toString(), {
      headers: getAuthHeaders()
    });

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || "Failed to fetch tickets");
      } catch {
        throw new Error("API Error");
      }
    }
    return await response.json();
  } catch (error) {
    throw error;
  }
};

export const addComment = async (ticketId: string, content: string, author: string): Promise<Comment> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${ticketId}/comments`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ content, author }),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }

    return await response.json();
  } catch (error) {
    throw error;
  }
};

const DEFAULT_PRIORITY = { name: 'Medium', color: 'bg-blue-500/10 text-blue-400 border border-blue-500/20', value: 1 };
const DEFAULT_STATUS = { name: 'Open', color: 'bg-blue-500/20 text-blue-400' };

export const createTicket = async (workspaceId: string, data: { title: string; description: string; statusId: string; priorityId: string; assignedAgentId?: string; assignedWorkflowId?: string }): Promise<Ticket> => {
  try {
    const body: any = {
      workspaceId,
      title: data.title,
      description: data.description,
      statusId: data.statusId,
      priorityId: data.priorityId,
      internal: true
    };

    // Only include optional fields if they have values (not empty strings)
    if (data.assignedAgentId) body.assignedAgentId = data.assignedAgentId;
    if (data.assignedWorkflowId) body.assignedWorkflowId = data.assignedWorkflowId;

    const response = await fetch(API_BASE_URL, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(body),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }

    return await response.json();
  } catch (error) {
    throw error;
  }
};

export const updateTicket = async (ticketId: string, updates: Partial<Ticket>): Promise<Ticket> => {
  try {
    // Filter out undefined values, but allow empty strings for unassignment
    const body: any = {};
    Object.entries(updates).forEach(([key, value]) => {
      // Skip undefined values
      if (value === undefined) {
        return;
      }
      
      // Convert empty string to null for assignedAgentId and assignedWorkflowId
      // This allows explicit unassignment of agents and workflows
      if ((key === 'assignedAgentId' || key === 'assignedWorkflowId') && value === '') {
        body[key] = null;
      } else if (value !== '') {
        // Skip other empty strings
        body[key] = value;
      }
    });

    const response = await fetch(`${API_BASE_URL}/${ticketId}`, {
      method: 'PATCH',
      headers: getAuthHeaders(),
      body: JSON.stringify(body),
    });

    const contentType = response.headers.get("content-type");
    if (contentType && contentType.includes("text/html")) throw new Error("Not JSON");

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }

    return await response.json();
  } catch (error) {
    // Propagate errors instead of returning mock success
    // This ensures the UI knows when the update failed
    throw error;
  }
};

export const deleteTicket = async (ticketId: string): Promise<void> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${ticketId}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }
  } catch (error) {
    // Mock fallback
    return new Promise((resolve) => {
      setTimeout(() => {
        resolve();
      }, 500);
    });
  }
};

export const convertToExternal = async (
  ticketId: string,
  integrationId: string,
  issueTypeName: string
): Promise<Ticket> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${ticketId}/convert`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify({ integrationId, issueTypeName }),
    });

    if (!response.ok) {
      try {
        const error = await response.json();
        throw new Error(error.detail || error.message || "Conversion failed");
      } catch (e: any) {
        if (e.message && !e.message.includes('Conversion failed')) {
          throw e;
        }
        throw new Error("Conversion failed");
      }
    }
    return await response.json();
  } catch (error) {
    console.error("Failed to convert ticket:", error);
    throw error;
  }
};

/**
 * Generates an AI-powered summary for a ticket by calling the backend API.
 * The backend combines the ticket's title, description, and comments to generate a summary.
 * @param ticketId - The ID of the ticket to summarize
 * @returns A Promise that resolves to the ticket with the summary field populated
 */
export const generateSummary = async (ticketId: string): Promise<Ticket> => {
  try {
    const response = await fetch(`${API_BASE_URL}/${ticketId}/summarize`, {
      method: 'POST',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      try {
        const errorData = await response.json();
        throw new Error(errorData.detail || errorData.message || response.statusText);
      } catch {
        throw new Error(`Backend error: ${response.statusText}`);
      }
    }

    return await response.json();
  } catch (error) {
    console.error("Failed to generate summary:", error);
    throw error;
  }
};
