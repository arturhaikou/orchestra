import React from 'react';
import { Loader2 } from 'lucide-react';

interface DiscoveryLoadingScreenProps {
  providerName: string;
  endpointUrl: string;
}

const DiscoveryLoadingScreen: React.FC<DiscoveryLoadingScreenProps> = ({ providerName, endpointUrl }) => (
  <div data-testid="discovery-loading" className="space-y-4 py-6">
    <div className="flex flex-col items-center gap-3">
      <Loader2 role="status" className="w-8 h-8 text-primary animate-spin" />
      <p className="text-sm text-textMuted">Connecting to {providerName}…</p>
    </div>
    <div>
      <label className="block text-xs font-bold text-textMuted uppercase tracking-widest mb-1">Endpoint URL</label>
      <input type="url" value={endpointUrl} disabled className="w-full bg-background/50 border border-border rounded-lg px-3 py-2 text-sm text-textMuted cursor-not-allowed" readOnly />
    </div>
  </div>
);

export default DiscoveryLoadingScreen;
