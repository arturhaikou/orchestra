import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
  getMcpServers,
  deleteMcpServer,
  fetchDeleteImpact,
  checkMcpServerNameUnique,
  McpServerNotFoundError,
  McpServerForbiddenError,
} from '../mcpServerService';

// ─── Helpers ─────────────────────────────────────────────────────────────────

const AUTH_TOKEN = 'test-bearer-token';
const WORKSPACE_ID = 'ws-abc-123';
const SERVER_ID = 'srv-def-456';

vi.mock('../authService', () => ({
  getToken: () => AUTH_TOKEN,
}));

// Mock import.meta.env
vi.stubGlobal('import', {
  meta: { env: { VITE_API_URL: 'http://localhost:5001' } },
});

const mockJsonResponse = (body: unknown, status = 200): Response =>
  ({
    ok: status >= 200 && status < 300,
    status,
    json: vi.fn().mockResolvedValue(body),
  } as unknown as Response);

let fetchSpy: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  fetchSpy = vi.spyOn(globalThis, 'fetch');
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ─── Scenario 1: getMcpServers calls the NEW endpoint ─────────────────────────

describe('Scenario 1: getMcpServers URL migration', () => {
  it('GetMcpServers_CallsNewEndpoint_NotIntegrationsEndpoint', async () => {
    fetchSpy.mockResolvedValueOnce(
      mockJsonResponse([{ id: SERVER_ID, name: 'Analytics', connectionStatus: 'Connected', transportType: 'HTTP', workspaceId: WORKSPACE_ID, createdAt: '2026-01-01T00:00:00Z' }])
    );

    await getMcpServers(WORKSPACE_ID);

    expect(fetchSpy).toHaveBeenCalledOnce();
    const [url] = fetchSpy.mock.calls[0] as [string, ...unknown[]];
    expect(url).toContain('/v1/mcp-servers');
    expect(url).not.toContain('/v1/integrations');
    expect(url).toContain(`workspaceId=${WORKSPACE_ID}`);
  });

  it('GetMcpServers_WhenResponseOk_ReturnsServerList', async () => {
    const servers = [{ id: SERVER_ID, name: 'Analytics', connectionStatus: 'Connected', transportType: 'HTTP', workspaceId: WORKSPACE_ID, createdAt: '2026-01-01T00:00:00Z' }];
    fetchSpy.mockResolvedValueOnce(mockJsonResponse(servers));

    const result = await getMcpServers(WORKSPACE_ID);
    expect(result).toHaveLength(1);
    expect(result[0].id).toBe(SERVER_ID);
  });

  it('GetMcpServers_WhenResponseNotOk_Throws', async () => {
    fetchSpy.mockResolvedValueOnce(mockJsonResponse({}, 500));
    await expect(getMcpServers(WORKSPACE_ID)).rejects.toThrow('HTTP 500');
  });
});

// ─── Scenario 2: deleteMcpServer calls NEW endpoint ───────────────────────────

describe('Scenario 2: deleteMcpServer URL migration', () => {
  it('DeleteMcpServer_CallsNewEndpoint_NotIntegrationsEndpoint', async () => {
    fetchSpy.mockResolvedValueOnce(
      mockJsonResponse({ affectedAgentCount: 2 }, 200)
    );

    await deleteMcpServer(SERVER_ID);

    expect(fetchSpy).toHaveBeenCalledOnce();
    const [url, init] = fetchSpy.mock.calls[0] as [string, RequestInit];
    expect(url).toContain(`/v1/mcp-servers/${SERVER_ID}`);
    expect(url).not.toContain('/v1/integrations');
    expect(init.method).toBe('DELETE');
  });

  it('DeleteMcpServer_WhenSuccessful_ReturnsAffectedAgentCount', async () => {
    fetchSpy.mockResolvedValueOnce(
      mockJsonResponse({ affectedAgentCount: 5 }, 200)
    );

    const result = await deleteMcpServer(SERVER_ID);
    expect(result.affectedAgentCount).toBe(5);
  });

  it('DeleteMcpServer_When404_ThrowsMcpServerNotFoundError', async () => {
    fetchSpy.mockResolvedValueOnce(mockJsonResponse({}, 404));
    await expect(deleteMcpServer(SERVER_ID)).rejects.toBeInstanceOf(McpServerNotFoundError);
  });

  it('DeleteMcpServer_When403_ThrowsMcpServerForbiddenError', async () => {
    fetchSpy.mockResolvedValueOnce(mockJsonResponse({}, 403));
    await expect(deleteMcpServer(SERVER_ID)).rejects.toBeInstanceOf(McpServerForbiddenError);
  });
});

// ─── Scenario 4: fetchDeleteImpact calls GET /v1/mcp-servers/{id}/impact ─────

describe('Scenario 4: fetchDeleteImpact URL and return value', () => {
  it('FetchDeleteImpact_CallsImpactEndpoint', async () => {
    fetchSpy.mockResolvedValueOnce(
      mockJsonResponse({ affectedAgentCount: 3 }, 200)
    );

    await fetchDeleteImpact(SERVER_ID);

    expect(fetchSpy).toHaveBeenCalledOnce();
    const [url] = fetchSpy.mock.calls[0] as [string, ...unknown[]];
    expect(url).toContain(`/v1/mcp-servers/${SERVER_ID}/impact`);
    expect(url).not.toContain('/v1/integrations');
  });

  it('FetchDeleteImpact_WhenSuccessful_ReturnsCount', async () => {
    fetchSpy.mockResolvedValueOnce(
      mockJsonResponse({ affectedAgentCount: 7 }, 200)
    );

    const count = await fetchDeleteImpact(SERVER_ID);
    expect(count).toBe(7);
  });

  it('FetchDeleteImpact_WhenNoneAffected_ReturnsZero', async () => {
    fetchSpy.mockResolvedValueOnce(
      mockJsonResponse({ affectedAgentCount: 0 }, 200)
    );

    const count = await fetchDeleteImpact(SERVER_ID);
    expect(count).toBe(0);
  });

  it('FetchDeleteImpact_WhenResponseNotOk_ThrowsMcpServerImpactFetchError', async () => {
    const { McpServerImpactFetchError } = await import('../mcpServerService');
    fetchSpy.mockResolvedValueOnce(mockJsonResponse({}, 500));
    await expect(fetchDeleteImpact(SERVER_ID)).rejects.toBeInstanceOf(McpServerImpactFetchError);
  });
});

// ─── checkMcpServerNameUnique — already correct endpoint (no change) ──────────

describe('checkMcpServerNameUnique — endpoint unchanged', () => {
  it('CheckName_CallsMcpServersCheckNameEndpoint', async () => {
    fetchSpy.mockResolvedValueOnce(mockJsonResponse({ isUnique: true }));

    await checkMcpServerNameUnique(WORKSPACE_ID, 'DataBot');

    const [url] = fetchSpy.mock.calls[0] as [string, ...unknown[]];
    expect(url).toContain('/v1/mcp-servers/check-name');
  });
});
