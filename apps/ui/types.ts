
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
  externalTicketId?: string;
  externalUrl?: string;
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
  id: string;
  name: string;
  description: string;
  requiredScopes?: string[];
  dangerLevel?: 'Safe' | 'Moderate' | 'Destructive';
  isEnabled?: boolean;
  isMcpTool?: boolean;
  mcpToolSchema?: string;
  integrationId?: string;
}

export interface Tool {
  id: string;
  name: string;
  description: string;
  category: 'TRACKER' | 'CODE' | 'KNOWLEDGE' | 'UTILITY' | 'COMMUNICATION' | 'INTERNAL';
  icon: string; // Lucide icon name or provider name
  actions?: ToolAction[];  // Optional: Array of specific actions this tool can perform
  source?: 'native' | 'mcp';
  integrationId?: string;
}

export interface IntegrationStatus {
  integrationMissing: boolean;
  integrationTypeLabel: string;
  warningMessage: string;
}

export interface Skill {
  id: string;
  workspaceId: string;
  name: string;
  description: string;
  instructions: string;
  createdAt: string;
  updatedAt: string;
}

export interface ModelMetadataDto {
  id: string;
  supportedReasoningEfforts?: string[] | null;
  defaultReasoningEffort?: string | null;
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
  mcpServerNames?: string[]; // MCP server names assigned to this agent
  subAgentIds: string[]; // IDs of agents assigned as sub-agents
  skillIds?: string[]; // IDs of skills assigned to this agent
  skills?: Skill[]; // Populated skill objects (read-only, from server)
  avatarUrl: string;
  customInstructions?: string;
  projectPrinciples?: string; // Non-null only for agents with a review tool assigned
  model?: string | null; // LLM model override; null means system default
  reasoningEffort?: string | null; // Reasoning effort: "low", "medium", "high", or null
  templateId?: string | null;
  templateVersion?: number | null;
  isBuiltIn?: boolean;
  guide?: string | null;
  integrationStatus?: IntegrationStatus | null;
  aiCliIntegrationId?: string | null;
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
  isCliAgent: boolean;
  editableFields: string[];
}

export interface CreateAgentFromTemplateRequest {
  workspaceId: string;
  templateId: string;
  projectPrinciples: string;
  model?: string;
  reasoningEffort?: string | null;
  aiCliIntegrationId?: string;
}

export type JobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'WaitingForInput';
export type JobTriggerType = 'Ticket' | 'ManualApi' | 'Cli';
export type JobStepType =
  | 'AgentStarted'
  | 'ThinkingMessage'
  | 'ToolCallStarted'
  | 'ToolCallCompleted'
  | 'AgentCompleted'
  | 'AgentFailed'
  | 'SubAgentCallStarted'
  | 'SubAgentCallCompleted';

export interface JobSummary {
  id: string;
  workspaceId: string;
  agentId: string;
  agentName: string;
  ticketTitle?: string;
  ticketId?: string;
  status: JobStatus;
  triggerType: JobTriggerType;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
}

export interface JobStep {
  id: string;
  stepType: JobStepType;
  sequence: number;
  timestamp: string;
  content?: string;
  toolName?: string;
  isJson: boolean;
  durationMs?: number;
  isError: boolean;
  parentStepId?: string;
  agentId?: string;
  agentName?: string;
}

export interface JobDetail extends JobSummary {
  initialPrompt: string;
  finalResponse?: string;
  errorMessage?: string;
  steps: JobStep[];
}

export interface PagedJobsResult {
  items: JobSummary[];
  total: number;
  page: number;
  pageSize: number;
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
  icon: string;
  connected: boolean;
  lastSync: string;
  url?: string;
  provider?: string;
  filterQuery?: string;
  username?: string;
  vectorize?: boolean;
  jiraType?: string;
  confluenceType?: string;
  isMcpBacked?: boolean;
  mcpEndpointUrl?: string;
  toolCount?: number;
  mcpTransportType?: 'HTTP' | 'STDIO';
  mcpCommand?: string;
}

export enum AiCliProviderType {
  GITHUB_COPILOT = 'GITHUB_COPILOT',
  CLAUDE = 'CLAUDE',
  GEMINI = 'GEMINI',
}

export interface AiCliIntegration {
  id: string;
  workspaceId: string;
  name: string;
  provider: AiCliProviderType;
  useLoggedInUser: boolean;
  workingDirectory: string;
  cliPath?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateCliIntegrationRequest {
  workspaceId: string;
  name: string;
  provider: AiCliProviderType;
  credential?: string;
  useLoggedInUser: boolean;
  workingDirectory: string;
  cliPath?: string | null;
}

export interface UpdateCliIntegrationRequest {
  workspaceId: string;
  name: string;
  credential?: string;
  useLoggedInUser: boolean;
  workingDirectory: string;
  cliPath?: string | null;
}

export interface ToolCategory {
  id: string;
  name: string;
  description: string;
  providerType: string;
  actions: ToolAction[];
  source: 'native' | 'mcp';
  integrationId?: string;
}

export interface McpIntegration extends Integration {
  mcpEndpointUrl: string;
  mcpAuthType: 'ApiKey' | 'None';
  toolCount: number;
}

export interface DiscoveredTool {
  id?: string;
  name: string;
  description?: string;
  dangerLevel: 'Safe' | 'Moderate' | 'Destructive';
  mcpToolSchema?: string | Record<string, unknown> | null;
}

export interface ToolEnablementOverride {
  toolId: string;
  enabled: boolean;
}

export interface SyncToolsResult {
  added: number;
  removed: number;
  total: number;
}

export type McpDiscoveryErrorType =
  | 'ConnectionFailed'
  | 'AuthenticationFailed'
  | 'AuthFailed'
  | 'Timeout'
  | 'ZeroTools'
  | 'NetworkError';

export interface McpDiscoveryError {
  errorType: McpDiscoveryErrorType;
  message: string;
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

export interface TicketStatusChangedEvent {
  workspaceId: string;
  ticketId: string;
  newStatus: string;
  previousStatus: string | null;
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

export type QuestionType = 'Text' | 'Radio' | 'Checkbox';

export interface QuestionItem {
  question: string;
  hint?: string;
  type: QuestionType;
  options?: string[];
  allowCustom?: boolean;
}

export interface AgentQuestion {
  id: string;
  jobId: string;
  agentId: string;
  workspaceId: string;
  questions: QuestionItem[];
  status: 'Pending' | 'Answered';
  createdAt: string;
}

/** Three-state connection status reflected from the last Connect verification. */
export type McpServerConnectionStatus = 'Connected' | 'ConnectionFailed' | 'Unverified';

/**
 * Slim read model returned by GET /v1/mcp-servers.
 * Does not include tool counts, discovery details, or encrypted credentials.
 */
export interface McpServer {
  id: string;
  workspaceId: string;
  name: string;
  connectionStatus: McpServerConnectionStatus;
  transportType: 'HTTP' | 'STDIO';
  /** HTTP transport: endpoint URL. Undefined for STDIO. */
  endpointUrl?: string;
  /** STDIO transport: command string. Undefined for HTTP. */
  command?: string;
  /** ISO 8601 — used for sort order (DESC, enforced server-side) */
  createdAt: string;
}

// ─── MCP Server Form Types (FR-003) ──────────────────────────────────────────

/** Internal form-state transport discriminator. Lowercase; distinct from McpServer.transportType ('HTTP' | 'STDIO'). */
export type McpServerTransportType = 'http' | 'stdio';

/** Authentication strategy options available for HTTP transport. */
export type HttpAuthType = 'none' | 'api_key' | 'bearer_token';

/** A single environment variable key-value pair for Stdio transport. */
export interface EnvVar {
  key: string;
  value: string;
}

/** All field values for the HTTP transport connection details section. */
export interface McpServerHttpFields {
  url: string;
  authType: HttpAuthType;
  /** Raw API key or bearer token string. Sent to backend only when authType !== 'none'. */
  apiKey: string;
}

/** All field values for the Stdio transport connection details section. */
export interface McpServerStdioFields {
  command: string;
  /** Each element is a single argument token, e.g. ['-y', '@modelcontextprotocol/server-github']. */
  args: string[];
  envVars: EnvVar[];
}

// ─── MCP Server HTTP Validation Types (FR-004) ───────────────────────────────

export interface HttpFieldErrors {
  serverName?: string;
  url?: string;
  apiKey?: string;
}

export interface HttpFieldTouched {
  serverName: boolean;
  url: boolean;
  apiKey: boolean;
}

export type ApiKeyEditState = 'masked' | 'touched';

// ─── MCP Server Stdio Validation Types (FR-005) ──────────────────────────────

export interface StdioFieldErrors {
  serverName?: string;
  command?: string;
  argErrors?: Record<number, string>;
  envKeyErrors?: Record<number, string>;
  envTotalSize?: string;
}

export interface StdioFieldTouched {
  serverName: boolean;
  command: boolean;
  argTouched: Record<number, boolean>;
  envKeyTouched: Record<number, boolean>;
}

export type EnvVarValueEditState = 'masked' | 'touched';

export type EnvVarEditStateMap = Record<number, EnvVarValueEditState>;

// ─── MCP Server Connect Types (FR-006) ───────────────────────────────────────

export type ConnectStatus = 'idle' | 'loading' | 'success' | 'error';

export type ConnectErrorCode =
  | 'CONNECTION_TIMEOUT'
  | 'AUTH_FAILED'
  | 'UNREACHABLE'
  | 'INVALID_COMMAND'
  | 'UNKNOWN';

export interface ToolPreviewDto {
  name: string;
  description: string | null;
}

export interface ConnectMcpServerResponse {
  tools: ToolPreviewDto[];
}

export interface ConnectMcpServerErrorResponse {
  errorCode: ConnectErrorCode;
  message: string;
}

// ─── MCP Server Save Types (FR-007) ──────────────────────────────────────────

/**
 * State machine for the Save operation.
 *  - 'idle'    — no save attempt, or error cleared.
 *  - 'saving'  — POST in flight; all form fields and buttons locked.
 *  - 'success' — 201 received; navigation fires immediately after state settles.
 *  - 'error'   — save failed; error banner shown; form remains editable.
 */
export type SaveStatus = 'idle' | 'saving' | 'success' | 'error';

/**
 * Discriminated error codes for the save operation.
 *  - 'DUPLICATE_NAME' — 409: another server with the same name exists in the workspace.
 *  - 'VALIDATION'     — 400: server-side field validation failed.
 *  - 'NETWORK'        — no response received (DNS, TCP, or fetch threw TypeError).
 *  - 'UNKNOWN'        — any other 4xx/5xx or unexpected error.
 */
export type SaveErrorCode = 'DUPLICATE_NAME' | 'VALIDATION' | 'NETWORK' | 'UNKNOWN';

/**
 * Error payload exposed by useSaveMcpServer when saveStatus === 'error'.
 */
export interface SaveMcpServerError {
  code: SaveErrorCode;
  /** User-facing message string, ready for display in SaveErrorBanner. */
  message: string;
}

/**
 * Successful 201 response shape from POST /v1/mcp-servers.
 * Mirrors McpServerListItemDto from FR-001 backend — no new backend DTO required.
 */
export interface SaveMcpServerResponseDto {
  id: string;
  workspaceId: string;
  name: string;
  connectionStatus: 'Connected' | 'ConnectionFailed' | 'Unverified';
  transportType: 'HTTP' | 'STDIO';
  endpointUrl: string | null;
  command: string | null;
  createdAt: string; // ISO 8601 UTC
}

/**
 * Error response body from POST /v1/integrations/mcp-servers on 409 or 400.
 */
export interface SaveMcpServerErrorDto {
  errorCode: string;
  message: string;
}

export interface DeleteMcpServerResponse {
  affectedAgentCount: number;
}

/**
 * Location state injected via React Router navigate() on a successful save.
 * Consumed by McpServersPage (FR-001/FR-002) to display the success toast.
 */
export interface McpServerSavedLocationState {
  toast: {
    /** 'created' drives "added successfully"; 'updated' drives "updated successfully". */
    intent: 'created' | 'updated';
    serverName: string;
  };
}

// ─── MCP Server Load Types (FR-008) ──────────────────────────────────────────

/**
 * State machine for the page-load fetch.
 *  - 'loading' — GET in flight; skeleton/spinner shown.
 *  - 'loaded'  — 200 received; form is pre-populated.
 *  - 'error'   — 404 or network failure; LoadErrorView shown.
 */
export type LoadMcpServerStatus = 'loading' | 'loaded' | 'error';

/**
 * Error codes for the load operation.
 *  - 'NOT_FOUND' — 404 from GET endpoint.
 *  - 'FORBIDDEN' — 403 (server belongs to a different workspace).
 *  - 'NETWORK'   — no response received.
 *  - 'UNKNOWN'   — any other non-2xx.
 */
export type LoadMcpServerErrorCode = 'NOT_FOUND' | 'FORBIDDEN' | 'NETWORK' | 'UNKNOWN';

/**
 * Safe DTO returned by GET /v1/integrations/mcp-servers/:id.
 * Credentials are NEVER present; presence/absence is indicated by boolean sentinels.
 */
export interface GetMcpServerByIdResponseDto {
  id: string;
  workspaceId: string;
  name: string;
  /** 'HTTP' | 'STDIO' — uppercase to match backend convention. */
  transportType: 'HTTP' | 'STDIO';
  connectionStatus: 'Connected' | 'ConnectionFailed' | 'Unverified';
  // HTTP fields — populated when transportType === 'HTTP'
  endpointUrl: string | null;
  authType: 'NONE' | 'API_KEY' | 'BEARER_TOKEN' | null;
  /** True when the server has a stored API key / bearer token; never exposes the raw value. */
  hasApiKey: boolean;
  // Stdio fields — populated when transportType === 'STDIO'
  command: string | null;
  args: string[] | null;
  /** Only env var KEYS are returned; values are never sent to the client. */
  envVarKeys: string[] | null;
}

// ─── MCP Server Patch Types (FR-008) ─────────────────────────────────────────

/**
 * State machine for the PATCH operation.
 * Mirrors SaveStatus from FR-007.
 */
export type PatchStatus = 'idle' | 'patching' | 'success' | 'error';

/**
 * Discriminated error codes for the patch operation.
 */
export type PatchErrorCode =
  | 'DUPLICATE_NAME'
  | 'VALIDATION_ERROR'
  | 'NOT_FOUND'
  | 'FORBIDDEN'
  | 'NETWORK'
  | 'UNKNOWN';

export interface PatchMcpServerError {
  code: PatchErrorCode;
  message: string;
}

// ─── Tool Picker Types (FR-002) ───────────────────────────────────────────────

export interface ToolCatalogueEntry {
  actionId: string;
  actionName: string;
  actionDescription: string;
  dangerLevel: 'Safe' | 'Moderate' | 'Destructive';
  sourceId: string;
  sourceName: string;
  sourceType: 'native' | 'mcp';
}

export interface ToolPickerState {
  snapshot: string[];
  working: string[];
  activeSourceId: string | null;
  searchTerm: string;
  /** MCP tool selections at the time the modal was opened (used for Discard). */
  mcpSnapshot: Record<string, string[]>;
  /** MCP tool selections being actively edited in the picker. */
  mcpWorking: Record<string, string[]>;
}

export type ToolPickerAction =
  | { type: 'OPEN_MODAL'; payload: { currentSelections: string[]; initialActiveSourceId?: string | null; initialMcpSelections?: Record<string, string[]> } }
  | { type: 'SET_ACTIVE_SOURCE'; payload: { sourceId: string } }
  | { type: 'TOGGLE_TOOL'; payload: { actionId: string } }
  | { type: 'SELECT_ALL'; payload: { actionIds: string[] } }
  | { type: 'DESELECT_ALL'; payload: { actionIds: string[] } }
  | { type: 'SET_SEARCH'; payload: { searchTerm: string } }
  | { type: 'COMMIT' }
  | { type: 'DISCARD' }
  | { type: 'TOGGLE_MCP_TOOL'; payload: { serverId: string; toolName: string } }
  | { type: 'SET_MCP_SNAPSHOT'; payload: { mcpSelections: Record<string, string[]> } }
  | { type: 'CLEAR_MCP_SERVER'; payload: { serverId: string } };

// ─── FR-005: MCP Tool Selection Types ──────────────────────────────────────────

export interface McpToolSelection {
  mcpServerId: string;
  toolNames: string[];
}

// ─── FR-004: Lazy MCP Tool Fetch Types ────────────────────────────────────────

export type McpToolFetchErrorType = 'Unreachable' | 'AuthFailed' | 'Empty';

export interface McpFetchedTool {
  name: string;
  description: string | null;
  dangerLevel: 'Safe' | 'Moderate' | 'Destructive';
}

export interface McpServerToolsResponse {
  isSuccess: boolean;
  tools: McpFetchedTool[] | null;
  errorType: McpToolFetchErrorType | null;
  errorMessage: string | null;
}

export type McpToolFetchState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; tools: McpFetchedTool[] }
  | { status: 'empty' }
  | { status: 'error'; message: string }
  | { status: 'auth_failed' };
