import React from 'react';
import { X } from 'lucide-react';

interface AgentFormCapabilitiesProps {
  currentCapability: string;
  capabilities: string[];
  onCurrentCapabilityChange: (value: string) => void;
  onAddCapability: (e: React.KeyboardEvent) => void;
  onRemoveCapability: (cap: string) => void;
}

const AgentFormCapabilities: React.FC<AgentFormCapabilitiesProps> = ({
  currentCapability,
  capabilities,
  onCurrentCapabilityChange,
  onAddCapability,
  onRemoveCapability,
}) => (
  <section className="space-y-4">
    <h2 className="text-lg font-semibold text-text">Capabilities</h2>
    <div>
      <label htmlFor="agent-capability" className="block text-sm font-medium text-text mb-1">Add Capability</label>
      <input
        id="agent-capability"
        type="text"
        value={currentCapability}
        onChange={e => onCurrentCapabilityChange(e.target.value)}
        onKeyDown={onAddCapability}
        className="w-full px-3 py-2 bg-background border border-border rounded-md text-text text-sm focus:outline-none focus:ring-2 focus:ring-primary"
        placeholder="Type and press Enter"
      />
    </div>
    {capabilities.length > 0 && (
      <div className="flex flex-wrap gap-2">
        {capabilities.map(cap => (
          <span key={cap} className="inline-flex items-center gap-1 px-2 py-1 bg-primary/10 text-primary text-xs rounded-full">
            {cap}
            <button type="button" onClick={() => onRemoveCapability(cap)} className="hover:text-red-400">
              <X className="w-3 h-3" />
            </button>
          </span>
        ))}
      </div>
    )}
  </section>
);

export default AgentFormCapabilities;
