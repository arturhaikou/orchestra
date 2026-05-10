import React, { useState } from 'react';
import { Plus, Minus } from 'lucide-react';
import { DiscoveredTool } from '../../types';
import { createHttpMcpIntegration, createStdioMcpIntegration } from '../../services/integrationService';
import DiscoveryLoadingScreen from './DiscoveryLoadingScreen';
import DiscoveryResultsScreen from './DiscoveryResultsScreen';
import ConnectionErrorScreen from './ConnectionErrorScreen';

type TransportType = 'HTTP' | 'STDIO';
type FormStep = 'form' | 'loading' | 'results' | 'error';
type AuthType = 'API_KEY' | 'NONE';

interface EnvVar { key: string; value: string; }

interface McpTransportFormProps {
  workspaceId: string;
  onSuccess: () => void;
  onCancel: () => void;
}

const McpTransportForm: React.FC<McpTransportFormProps> = ({ workspaceId, onSuccess, onCancel }) => {
  const [step, setStep] = useState<FormStep>('form');
  const [transport, setTransport] = useState<TransportType>('HTTP');
  const [name, setName] = useState('');

  const [endpointUrl, setEndpointUrl] = useState('');
  const [authType, setAuthType] = useState<AuthType>('API_KEY');
  const [apiKey, setApiKey] = useState('');

  const [command, setCommand] = useState('');
  const [args, setArgs] = useState<string[]>(['']);
  const [envVars, setEnvVars] = useState<EnvVar[]>([{ key: '', value: '' }]);

  const [discoveredTools, setDiscoveredTools] = useState<DiscoveredTool[]>([]);
  const [errorCode, setErrorCode] = useState<string>('');
  const [urlError, setUrlError] = useState('');
  const [commandError, setCommandError] = useState('');

  const switchTransport = (next: TransportType) => {
    setTransport(next);
    setEndpointUrl('');
    setAuthType('API_KEY');
    setApiKey('');
    setCommand('');
    setArgs(['']);
    setEnvVars([{ key: '', value: '' }]);
    setUrlError('');
    setCommandError('');
  };

  const validateForm = () => {
    if (transport === 'HTTP') {
      if (!endpointUrl.startsWith('https://')) {
        setUrlError('Endpoint URL must start with https://');
        return false;
      }
    } else {
      if (!command.trim()) {
        setCommandError('Command is required for stdio transport.');
        return false;
      }
    }
    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!validateForm()) return;
    setStep('loading');

    try {
      if (transport === 'HTTP') {
        const result = await createHttpMcpIntegration({
          workspaceId,
          name,
          endpointUrl,
          authType,
          apiKey: authType === 'API_KEY' ? apiKey : undefined,
        });
        setDiscoveredTools(result.tools.map(t => ({
          id: t.toolId,
          name: t.toolName,
          description: undefined,
          dangerLevel: t.dangerLevel as 'Safe' | 'Moderate' | 'Destructive',
        })));
      } else {
        const result = await createStdioMcpIntegration({
          workspaceId,
          name,
          command,
          arguments: args.filter(a => a.trim() !== ''),
          environmentVariables: buildEnvVarMap(envVars),
        });
        setDiscoveredTools(result.tools.map(t => ({
          id: t.toolId,
          name: t.toolName,
          description: undefined,
          dangerLevel: t.dangerLevel as 'Safe' | 'Moderate' | 'Destructive',
        })));
      }
      setStep('results');
    } catch (err: any) {
      setErrorCode(err?.type ?? 'UNKNOWN_ERROR');
      setStep('error');
    }
  };

  if (step === 'loading') {
    return (
      <DiscoveryLoadingScreen
        providerName={transport === 'HTTP' ? 'MCP Server (HTTP)' : 'MCP Server (stdio)'}
        endpointUrl={transport === 'HTTP' ? endpointUrl : command}
      />
    );
  }

  if (step === 'results') {
    return (
      <DiscoveryResultsScreen
        tools={discoveredTools}
        onConfirm={onSuccess}
        onCancel={onCancel}
      />
    );
  }

  if (step === 'error') {
    return (
      <ConnectionErrorScreen
        error={{ errorType: 'ConnectionFailed', message: resolveErrorMessage(errorCode) }}
        onRetry={() => setStep('form')}
        onBack={onCancel}
      />
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div>
        <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">
          Display Name
        </label>
        <input
          id="mcp-name"
          type="text"
          value={name}
          onChange={e => setName(e.target.value)}
          required
          className="w-full bg-background border border-border rounded-lg px-3 py-2 text-sm text-text focus:outline-none focus:border-primary"
        />
      </div>

      <div>
        <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">
          Transport Type
        </label>
        <div role="group" aria-label="Transport type" className="flex rounded-lg border border-border overflow-hidden">
          {(['HTTP', 'STDIO'] as const).map(t => (
            <button
              key={t}
              type="button"
              aria-pressed={transport === t}
              onClick={() => switchTransport(t)}
              className={`flex-1 py-2 text-sm font-semibold transition-colors ${
                transport === t
                  ? 'bg-primary/10 border-primary/40 text-primary'
                  : 'text-textMuted hover:text-text bg-background'
              }`}
            >
              {t === 'HTTP' ? 'HTTP' : 'stdio'}
            </button>
          ))}
        </div>
      </div>

      {transport === 'HTTP' && (
        <>
          <div>
            <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">
              Endpoint URL
            </label>
            <input
              id="mcp-endpoint"
              type="url"
              value={endpointUrl}
              onChange={e => { setEndpointUrl(e.target.value); setUrlError(''); }}
              required
              aria-label="Endpoint URL"
              className={`w-full bg-background border rounded-lg px-3 py-2 text-sm text-text focus:outline-none focus:border-primary ${urlError ? 'border-red-500' : 'border-border'}`}
            />
            {urlError && <p className="text-xs text-red-400 mt-1">{urlError}</p>}
          </div>

          <div>
            <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">
              Auth Type
            </label>
            <select
              id="mcp-auth-type"
              aria-label="Auth type"
              value={authType}
              onChange={e => setAuthType(e.target.value as AuthType)}
              className="w-full bg-background border border-border rounded-lg px-3 py-2 text-sm text-text focus:outline-none focus:border-primary"
            >
              <option value="API_KEY">API Key</option>
              <option value="NONE">None</option>
            </select>
          </div>

          {authType === 'API_KEY' && (
            <div>
              <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">
                API Key
              </label>
              <input
                id="mcp-api-key"
                type="password"
                value={apiKey}
                onChange={e => setApiKey(e.target.value)}
                aria-label="API Key"
                className="w-full bg-background border border-border rounded-lg px-3 py-2 text-sm text-text focus:outline-none focus:border-primary"
              />
            </div>
          )}
        </>
      )}

      {transport === 'STDIO' && (
        <>
          <div>
            <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">
              Command
            </label>
            <input
              id="mcp-command"
              type="text"
              value={command}
              onChange={e => { setCommand(e.target.value); setCommandError(''); }}
              required
              aria-label="Command"
              className={`w-full bg-background border rounded-lg px-3 py-2 text-sm font-mono text-text focus:outline-none focus:border-primary ${commandError ? 'border-red-500' : 'border-border'}`}
            />
            {commandError && <p className="text-xs text-red-400 mt-1">{commandError}</p>}
          </div>

          <div>
            <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">
              Arguments
            </label>
            <div className="space-y-2">
              {args.map((arg, i) => (
                <div key={i} className="flex items-center gap-2">
                  <span className="text-xs text-textMuted w-6 shrink-0">[{i}]</span>
                  <input
                    type="text"
                    value={arg}
                    onChange={e => setArgs(prev => prev.map((a, idx) => idx === i ? e.target.value : a))}
                    aria-label={`Argument ${i}`}
                    className="flex-1 bg-background border border-border rounded-lg px-3 py-1.5 text-sm font-mono text-text focus:outline-none focus:border-primary"
                  />
                  <button
                    type="button"
                    onClick={() => setArgs(prev => prev.filter((_, idx) => idx !== i))}
                    aria-label={`Remove argument ${i}`}
                    className="p-1 text-textMuted hover:text-red-400 transition-colors"
                  >
                    <Minus className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))}
              <button
                type="button"
                onClick={() => setArgs(prev => [...prev, ''])}
                className="flex items-center gap-1 text-xs text-textMuted hover:text-text transition-colors"
              >
                <Plus className="w-3 h-3" /> Add argument
              </button>
            </div>
          </div>

          <div>
            <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">
              Environment Variables
            </label>
            <div className="space-y-2">
              {envVars.map((ev, i) => (
                <div key={i} className="flex items-center gap-2">
                  <input
                    type="text"
                    value={ev.key}
                    onChange={e => setEnvVars(prev => prev.map((v, idx) => idx === i ? { ...v, key: e.target.value } : v))}
                    placeholder="KEY"
                    aria-label={`Environment variable key ${i}`}
                    className="flex-1 bg-background border border-border rounded-lg px-3 py-1.5 text-sm font-mono text-text focus:outline-none focus:border-primary"
                  />
                  <input
                    type="password"
                    value={ev.value}
                    onChange={e => setEnvVars(prev => prev.map((v, idx) => idx === i ? { ...v, value: e.target.value } : v))}
                    placeholder="value"
                    aria-label={`Environment variable value ${i}`}
                    className="flex-1 bg-background border border-border rounded-lg px-3 py-1.5 text-sm text-text focus:outline-none focus:border-primary"
                  />
                  <button
                    type="button"
                    onClick={() => setEnvVars(prev => prev.filter((_, idx) => idx !== i))}
                    aria-label="Remove variable"
                    className="p-1 text-textMuted hover:text-red-400 transition-colors"
                  >
                    <Minus className="w-3.5 h-3.5" />
                  </button>
                </div>
              ))}
              <button
                type="button"
                onClick={() => setEnvVars(prev => [...prev, { key: '', value: '' }])}
                className="flex items-center gap-1 text-xs text-textMuted hover:text-text transition-colors"
              >
                <Plus className="w-3 h-3" /> Add variable
              </button>
            </div>
          </div>
        </>
      )}

      <div className="flex gap-2 pt-2">
        <button
          type="submit"
          className="flex-1 bg-primary text-white rounded-lg py-2.5 text-sm font-medium hover:bg-primary/90 transition-colors"
        >
          Connect &amp; Discover Tools
        </button>
        <button
          type="button"
          onClick={onCancel}
          className="px-4 py-2.5 text-sm text-textMuted hover:text-text border border-border rounded-lg transition-colors"
        >
          Cancel
        </button>
      </div>
    </form>
  );
};

function buildEnvVarMap(envVars: EnvVar[]): Record<string, string> | undefined {
  const filled = envVars.filter(e => e.key.trim() !== '');
  if (filled.length === 0) return undefined;
  return Object.fromEntries(filled.map(e => [e.key.trim(), e.value]));
}

function resolveErrorMessage(errorCode: string): string {
  if (errorCode === 'PROCESS_LAUNCH_FAILURE') return 'The process could not be launched. Check the command and try again.';
  if (errorCode === 'DISCOVERY_TIMEOUT') return 'The process did not respond within 30 seconds.';
  return 'Connection failed. Check the server details and try again.';
}

export default McpTransportForm;
