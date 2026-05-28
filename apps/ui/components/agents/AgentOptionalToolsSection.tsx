import React from 'react';
import { OptionalToolDto } from '../../types';

interface Props {
  availableOptionalTools: OptionalToolDto[];
  selectedMethodNames: string[];
  onChange: (methodNames: string[]) => void;
  readonly?: boolean;
}

const AgentOptionalToolsSection: React.FC<Props> = ({
  availableOptionalTools,
  selectedMethodNames,
  onChange,
  readonly = false,
}) => {
  if (availableOptionalTools.length === 0) return null;

  const toolsByProvider = availableOptionalTools.reduce<Map<string, OptionalToolDto[]>>(
    (map, tool) => {
      const group = map.get(tool.provider) ?? [];
      group.push(tool);
      map.set(tool.provider, group);
      return map;
    },
    new Map()
  );

  const toggle = (methodName: string) => {
    if (readonly) return;
    if (selectedMethodNames.includes(methodName)) {
      onChange(selectedMethodNames.filter(n => n !== methodName));
    } else {
      onChange([...selectedMethodNames, methodName]);
    }
  };

  const providerLabel = (provider: string): string =>
    provider.charAt(0) + provider.slice(1).toLowerCase();

  return (
    <div className="space-y-3">
      <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300">
        Optional Code Source Tools
      </h3>
      {Array.from(toolsByProvider.entries()).map(([provider, tools]) => (
        <div key={provider} className="space-y-2">
          <p className="text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">
            {providerLabel(provider)}
          </p>
          {tools.map(tool => (
            <label
              key={tool.methodName}
              className="flex items-center gap-2 cursor-pointer"
            >
              <input
                type="checkbox"
                checked={selectedMethodNames.includes(tool.methodName)}
                onChange={() => toggle(tool.methodName)}
                disabled={readonly}
                className="rounded border-gray-300 text-blue-600 focus:ring-blue-500 disabled:opacity-50"
              />
              <span className="text-sm text-gray-700 dark:text-gray-300">{tool.label}</span>
            </label>
          ))}
        </div>
      ))}
    </div>
  );
};

export default AgentOptionalToolsSection;
