import React, { useEffect, useState } from 'react';
import { getIntegrations } from '../services/integrationService';
import { Integration } from '../types';

interface IntegrationSelectorProps {
  workspaceId: string;
  value: string | null;
  onChange: (integrationId: string, integrationName: string) => void;
  disabled?: boolean;
}

export const IntegrationSelector: React.FC<IntegrationSelectorProps> = ({
  workspaceId,
  value,
  onChange,
  disabled = false
}) => {
  const [integrations, setIntegrations] = useState<Integration[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchIntegrations = async () => {
      try {
        setLoading(true);
        setError(null);
        const allIntegrations = await getIntegrations(workspaceId);
        
        // Filter for TRACKER type integrations only
        const trackerIntegrations = allIntegrations.filter(
          integration => integration.type === 'TRACKER'
        );
        
        setIntegrations(trackerIntegrations);
      } catch (err) {
        setError('Failed to load integrations');
        console.error('Error fetching integrations:', err);
      } finally {
        setLoading(false);
      }
    };

    if (workspaceId) {
      fetchIntegrations();
    }
  }, [workspaceId]);

  const handleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const selectedId = e.target.value;
    const selectedIntegration = integrations.find(i => i.id === selectedId);
    if (selectedIntegration) {
      onChange(selectedId, selectedIntegration.name);
    }
  };

  if (loading) {
    return (
      <select disabled className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-md text-slate-400">
        <option>Loading integrations...</option>
      </select>
    );
  }

  if (error) {
    return (
      <select disabled className="w-full px-3 py-2 bg-slate-800 border border-red-500 rounded-md text-red-400">
        <option>{error}</option>
      </select>
    );
  }

  if (integrations.length === 0) {
    return (
      <select disabled className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-md text-slate-400">
        <option>No tracker integrations available</option>
      </select>
    );
  }

  return (
    <select
      value={value || ''}
      onChange={handleChange}
      disabled={disabled}
      className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-md text-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed"
    >
      <option value="">Select tracker integration...</option>
      {integrations.map(integration => (
        <option key={integration.id} value={integration.id}>
          {integration.name} ({integration.provider})
        </option>
      ))}
    </select>
  );
};
