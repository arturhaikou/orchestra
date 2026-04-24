
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
  isAiSummarizationEnabled: boolean;
  isCustomerSatisfactionAnalysisEnabled: boolean;
  aiSummarizationModelId?: string;
  customerSatisfactionAnalysisModelId?: string;
  /** The workspace's configured default AI model identifier. Set when a provider is configured. */
  defaultModelId?: string;
  /** The user ID of the workspace owner. Used to gate interactive AI feature toggles (owner-only). */
  ownerId: string;
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

export interface TicketSummarizationResponse {
  ticket?: Ticket; // Populated when summarization is enabled and successful
  featureDisabled: boolean; // True when AI summarization is disabled for the workspace
  message?: string; // Message when feature is disabled
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

export interface IntegrationStatus {
  integrationMissing: boolean;
  integrationTypeLabel: string;
  warningMessage: string;
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
  projectPrinciples?: string; // Non-null only for agents with a review tool assigned
  model?: string | null; // LLM model override; null means system default
  templateId?: string | null;
  templateVersion?: number | null;
  isBuiltIn?: boolean;
  usageGuide?: string | null;
  integrationStatus?: IntegrationStatus | null;
}

export interface TemplatePrerequisiteDto {
  integrationType: string;
  providerName: string;
  satisfied: boolean;
}

export interface TemplateAvailabilityDto {
  status: 'AVAILABLE' | 'UNAVAILABLE' | 'ALREADY_DEPLOYED' | 'ERROR';
  reason?: string | null;
  existingAgentId?: string | null;
}

export interface AgentTemplateDto {
  templateId: string;
  name: string;
  role: string;
  description: string;
  prerequisites: TemplatePrerequisiteDto[];
  availability: TemplateAvailabilityDto;
  templateVersion: number;
  capabilities: string[];
  toolLabel: string;
  usageGuide: string;
}

export interface CreateAgentFromTemplateRequest {
  workspaceId: string;
  templateId: string;
  projectPrinciples: string;
  model?: string;
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
  types: IntegrationType[];
  icon: string; // Used for UI display logic (jira, github, etc)
  connected: boolean;
  lastSync: string;
  url?: string;
  provider?: string; 
  filterQuery?: string;
  username?: string;
  vectorize?: boolean;
  jiraType?: string;       // Computed from URL: "Cloud" or "OnPremise"
  confluenceType?: string; // Computed from URL: "Cloud" or "OnPremise"
}

export interface User {
  id: string;
  email: string;
  name: string;
}

export interface ModelPullProgressPayload {
  model: string;
  status: string;
  percent: number;
}

export interface ModelPullCompletedPayload {
  model: string;
}

export interface ModelPullFailedPayload {
  model: string;
  error: string;
}

export interface ModelPullProgressEvent {
  workspaceId: string;
  model: string;
  status: string;
  percent: number;
}

export interface ModelPullCompletedEvent {
  workspaceId: string;
  model: string;
}

export interface ModelPullFailedEvent {
  workspaceId: string;
  model: string;
  error: string;
}

export interface WorkspaceModel {
  /** Ollama model name identifier (e.g., "llama3.2" or "llama3:8b"). */
  modelName: string;
  /** Current lifecycle status of the model on the Ollama server. */
  status: 'Pulling' | 'Available' | 'Failed' | 'Removing';
  /** Pull progress percentage 0–100. Null when not in a Pulling state. */
  pullProgress: number | null;
  /** Error message populated when status is 'Failed'. Null otherwise. */
  errorMessage: string | null;
  /** ISO 8601 timestamp string recording when the model was added to the workspace. */
  addedAt: string;
}

/** Status payload returned by GET /v1/provider/ollama/models/pull/{pullId}. */
export interface OllamaPullStatus {
  pullId: string;
  status: 'Pulling' | 'Available' | 'Failed';
  progress: number;
  errorMessage: string | null;
}

export interface AgentExecutionCompletedEvent {
  workspaceId: string;
  agentId: string;
  agentName: string;
  ticketId: string;
  ticketTitle: string;
  status: 'success' | 'failed';
  reviewUrl: string | null;
}

export type ConnectionStatus = 'connected' | 'reconnecting' | 'disconnected';

export interface ExecutionToastData {
  id: string;
  agentId: string;
  agentName: string;
  ticketId: string;
  ticketTitle: string;
  status: 'success' | 'failed';
  reviewUrl: string | null;
  createdAt: number;
}

/**
 * Provider configuration snapshot returned by `getWorkspaceProviderConfig()`.
 * Mirrors the `ProviderValidationResult` backend DTO from
 * `POST /v1/workspaces/{id}/provider/validate`.
 *
 * Security note: Azure credentials (`endpoint`, `apiKey`) are encrypted at rest
 * and are intentionally absent from this response per Phase 2 FR-06 constraints.
 * The Ollama server URL (`ollamaBaseUrl`) is stored as plaintext and is safe to
 * surface; it is populated for Ollama workspaces and `undefined` for Azure ones.
 */
export interface WorkspaceProviderConfig {
  /** The workspace's currently configured AI provider type. */
  providerType: 'AzureOpenAI' | 'Ollama';
  /** Live list of model deployment / tag names for the configured provider. */
  models: string[];
  /**
   * Azure OpenAI resource endpoint URL.
   * `undefined` — the backend intentionally omits encrypted Azure credentials.
   */
  endpoint?: string;
  /**
   * Stored Ollama server base URL (e.g., `http://localhost:11434`).
   * Populated for Ollama workspaces; `undefined` for Azure OpenAI workspaces.
   * Must not be logged.
   */
  ollamaBaseUrl?: string;
  /**
   * `true` when the currently stored provider credentials passed a live
   * connectivity probe on load. `false` when the probe failed (e.g., expired
   * API key, Ollama server offline).
   */
  isValid: boolean;
}

/**
 * Payload sent to `updateWorkspaceProvider()` via `PUT /v1/workspaces/{id}/provider`.
 * Mirrors the `ReconfigureProviderRequest` backend DTO from Phase 4 FR-03.
 *
 * Security note: `apiKey` is transmitted only over HTTPS and must never be
 * written to localStorage, sessionStorage, or any client-side log.
 * When omitting `apiKey` (i.e., keeping the stored credentials unchanged),
 * leave the field `undefined` — do NOT send an empty string.
 */
export interface WorkspaceProviderUpdateRequest {
  /** The provider type to configure. */
  providerType: 'AzureOpenAI' | 'Ollama';
  /** Azure OpenAI resource endpoint URL. Required when `providerType` is `'AzureOpenAI'` and sending new credentials. */
  endpoint?: string;
  /** Azure OpenAI API key. Omit entirely (undefined) to preserve the currently stored key. */
  apiKey?: string;
  /** The model identifier to use as the workspace default. Must appear in the validated model list. */
  defaultModelId: string;
}
