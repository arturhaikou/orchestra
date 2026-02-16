
export enum IntegrationType {
  TRACKER = 'TRACKER',
  KNOWLEDGE_BASE = 'KNOWLEDGE_BASE',
  CODE_SOURCE = 'CODE_SOURCE'
}

export interface TicketStatus {
  id: string;
  name: string;
  color: string;
}

export interface TicketPriority {
  id: string;
  name: string;
  color: string;
  value: number;
}

export interface Workspace {
  id: string;
  name: string;
}

export interface Comment {
  id: string;
  author: string;
  content: string;
  timestamp?: string; // Optional: populated for internal tickets, null for external tickets
}

export interface Ticket {
  id: string;
  workspaceId: string;
  source: string; // e.g., 'Jira-123' or 'INTERNAL'
  internal: boolean;
  title: string;
  description: string;
  status: TicketStatus | null;
  priority: TicketPriority | null;
  satisfaction: number; // 0-100 score
  assignedAgentId?: string;
  assignedWorkflowId?: string;
  integrationId?: string; // Link to the integration that sourced this ticket
  comments: Comment[];
  summary?: string; // AI Generated
}

export interface PaginatedResponse<T> {
  items: T[];
  nextPageToken?: string;
  isLast: boolean;
  totalCount?: number;
}

export interface ToolAction {
  id: string;              // Unique action ID (e.g., 'jira_read_tickets')
  name: string;            // Display name (e.g., 'Read Tickets')
  description: string;     // Action description
  requiredScopes?: string[];  // Optional: OAuth scopes required
  dangerLevel?: 'safe' | 'moderate' | 'destructive'; // Optional: risk indicator
}

export interface Tool {
  id: string;
  name: string;
  description: string;
  category: 'TRACKER' | 'CODE' | 'KNOWLEDGE' | 'UTILITY' | 'COMMUNICATION' | 'INTERNAL';
  icon: string; // Lucide icon name or provider name
  actions?: ToolAction[];  // Optional: Array of specific actions this tool can perform
}

export interface Agent {
  id: string;
  workspaceId: string;
  name: string;
  role: string;
  status: 'IDLE' | 'BUSY' | 'OFFLINE';
  capabilities: string[];
  toolActionIds: string[]; // List of authorized tool action IDs (for create/update)
  toolCategories: string[]; // Unique category names for display
  avatarUrl: string;
  customInstructions?: string;
}

export interface Job {
  id: string;
  workspaceId: string;
  ticketId?: string;
  integrationId?: string;
  agentId?: string;
  workflowId?: string;
  status: 'PENDING' | 'IN_PROGRESS' | 'COMPLETED' | 'FAILED';
  progress: number;
  logs: string[];
  startedAt: string;
  type?: 'SYNC' | 'AUTOMATION' | 'ANALYSIS';
}

export interface WorkflowStep {
  id: string;
  name: string;
  type: 'TRIGGER' | 'ACTION' | 'CONDITION';
  config: Record<string, any>;
}

export interface Workflow {
  id: string;
  workspaceId: string;
  name: string;
  nodes: any[]; // ReactFlow nodes
  edges: any[]; // ReactFlow edges
}

export interface Integration {
  id: string;
  workspaceId: string;
  name: string;
  type: IntegrationType;
  icon: string; // Used for UI display logic (jira, github, etc)
  connected: boolean;
  lastSync: string;
  url?: string;
  provider?: string; 
  filterQuery?: string;
  username?: string;
  vectorize?: boolean;
}

export interface User {
  id: string;
  email: string;
  name: string;
}
