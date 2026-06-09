import React from 'react';
import { SiGithub, SiGitlab } from '@icons-pack/react-simple-icons';
import { OptionalToolDto } from '../../types';

interface Props {
  availableOptionalTools: OptionalToolDto[];
  selectedMethodNames: string[];
  onChange: (methodNames: string[]) => void;
  readonly?: boolean;
}

const PROVIDER_ICONS: Record<string, (props: { size?: number; className?: string }) => React.ReactElement> = {
  GITHUB: ({ size = 18 }) => <SiGithub size={size} color="currentColor" />,
  GITLAB: ({ size = 18 }) => <SiGitlab size={size} color="currentColor" />,
};

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
    <section className="space-y-3">
      <h2 className="text-lg font-semibold text-text">Tools</h2>
      <div className="flex flex-wrap gap-2">
        {Array.from(toolsByProvider.entries()).map(([provider, tools]) => {
          const selectedCount = tools.filter(t => selectedMethodNames.includes(t.methodName)).length;
          const ProviderIcon = PROVIDER_ICONS[provider] ?? (({ size = 18 }) => <SiGithub size={size} color="currentColor" />);

          return (
            <div
              key={provider}
              data-testid="optional-tool-provider-card"
              className="flex flex-col gap-2 px-3 py-2 rounded-lg border border-border bg-surface min-w-[160px]"
            >
              <div className="flex items-center gap-2">
                <ProviderIcon size={18} className="shrink-0 text-textMuted" aria-hidden="true" />
                <span className="text-sm font-medium text-text">{providerLabel(provider)}</span>
                <span
                  data-testid="selection-count"
                  className="ml-auto text-xs font-semibold px-2 py-0.5 rounded-full bg-primary/10 text-primary border border-primary/20 whitespace-nowrap"
                >
                  {selectedCount} / {tools.length}
                </span>
              </div>
              <div className="flex flex-wrap gap-1.5">
                {tools.map(tool => {
                  const isSelected = selectedMethodNames.includes(tool.methodName);
                  return (
                    <button
                      key={tool.methodName}
                      type="button"
                      disabled={readonly}
                      onClick={() => toggle(tool.methodName)}
                      aria-pressed={isSelected}
                      className={`px-2.5 py-1 rounded-md text-xs font-medium border transition-colors disabled:opacity-50 disabled:cursor-not-allowed
                        ${isSelected
                          ? 'bg-primary text-white border-primary hover:bg-primaryHover'
                          : 'bg-background text-textMuted border-border hover:border-primary/50 hover:text-text'
                        }`}
                    >
                      {tool.label}
                    </button>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
};

export default AgentOptionalToolsSection;
