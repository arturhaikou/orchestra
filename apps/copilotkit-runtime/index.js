const express = require('express');
const cors = require('cors');
const { CopilotRuntime, copilotRuntimeNodeHttpEndpoint } = require('@copilotkit/runtime');
const { HttpAgent } = require('@ag-ui/client');

const PORT = process.env.PORT || 3001;

// Aspire service discovery for the .NET API
const API_BASE_URL =
  process.env.services__api__http__0 ||
  process.env.api_http ||
  process.env.services__api__https__0 ||
  process.env.api_https ||
  'http://localhost:5075';

// The UI origin is injected by Aspire; fall back to wildcard for local dev
const CORS_ORIGIN = process.env.CORS_ORIGIN || '*';

const app = express();

// Request logging middleware
app.use((req, res, next) => {
  console.log(`[${new Date().toISOString()}] ${req.method} ${req.path}`);
  console.log('  Headers:', JSON.stringify(req.headers, null, 2));
  next();
});

app.use(
  cors({
    origin: CORS_ORIGIN,
    methods: ['GET', 'POST', 'OPTIONS', 'PUT', 'DELETE'],
    allowedHeaders: ['*'],
    credentials: CORS_ORIGIN !== '*',
  })
);
app.options('*', cors());

// Health check used by Aspire
app.get('/health', (_req, res) => {
  console.log('[HEALTH CHECK] OK');
  res.json({ status: 'ok' });
});

// Single runtime instance — the agents function is called per-request with the
// Hono context (ctx), giving us access to the incoming headers each time.
const runtime = new CopilotRuntime({
  agents: async (ctx) => {
    console.log('\n========== AGENTS FUNCTION CALLED ==========');
    console.log('ctx type:', typeof ctx);
    
    // ctx is undefined during the runtime info/discovery GET request.
    // Guard early — the info endpoint only needs a non-throwing response.
    if (!ctx?.request) {
      console.log('⚠️  ctx.request is undefined - returning empty agents (likely info request)');
      return {};
    }

    const headers = ctx.request.headers;
    const authHeader = headers.get('authorization');
    const workspaceId = headers.get('x-workspace-id');
    const agentId = headers.get('x-agent-id');

    console.log('Authorization header:', authHeader ? '***PRESENT***' : 'MISSING');
    console.log('X-Workspace-Id:', workspaceId || 'MISSING');
    console.log('X-Agent-Id:', agentId || 'MISSING');
    console.log("Auth Header:", authHeader);
    if (!workspaceId || !agentId) {
      console.log('⚠️  Missing workspace or agent ID - returning empty agents');
      return {};
    }

    const agentUrl = `${API_BASE_URL}/workspaces/${workspaceId}/agents/${agentId}`;
    console.log('✓ Creating HttpAgent for URL:', agentUrl);

    const agentHeaders = authHeader ? { Authorization: authHeader } : {};
    console.log('Headers passed to HttpAgent:', {
      hasAuthorization: !!authHeader,
      authHeaderPreview: authHeader ? `${authHeader.substring(0, 20)}...` : 'none',
      allHeaders: Object.keys(agentHeaders),
    });

    return {
      [agentId]: new HttpAgent({
        url: agentUrl,
        // headers: {
        //   Authorization: `${authHeader}`,
        // }
      }),
    };
  },
});

const copilotHandler = copilotRuntimeNodeHttpEndpoint({
  endpoint: '/',
  runtime,
});

app.use('/copilotkit', copilotHandler);

app.listen(PORT, () => {
  console.log(`\n✓ CopilotKit runtime listening on port ${PORT}`);
  console.log(`✓ Proxying agent requests to: ${API_BASE_URL}`);
  console.log(`✓ CORS origin: ${CORS_ORIGIN}\n`);
});

