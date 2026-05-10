import { useState, useEffect, FC, KeyboardEvent } from 'react';
import { LayoutGrid, LucideIcon } from 'lucide-react';
import * as LucideIcons from 'lucide-react';

export interface CategoryCardProps {
  sourceId: string;
  name: string;
  description: string;
  iconName: string | undefined;
  selectedCount?: number;
  totalCount?: number;
  isActive: boolean;
  onClick: () => void;
  hint?: React.ReactNode;
}

function resolveIcon(iconName: string | undefined): LucideIcon {
  if (!iconName) return LayoutGrid;
  const icon = (LucideIcons as Record<string, unknown>)[iconName];
  return typeof icon === 'function' ? (icon as LucideIcon) : LayoutGrid;
}

function cardClasses(isActive: boolean): string {
  const baseClasses =
    'flex items-center gap-2 px-3 py-2.5 rounded-lg border cursor-pointer select-none transition-colors motion-reduce:transition-none focus:outline-none focus-visible:ring-2 focus-visible:ring-primary';
  if (isActive) {
    return `${baseClasses} border-primary bg-primary/10 text-text`;
  }
  return `${baseClasses} border-border bg-surface text-textMuted hover:border-primary/50 hover:bg-surfaceHighlight hover:text-text`;
}

function CountBadge({ selected, total }: { selected: number; total: number }) {
  return (
    <span data-testid="count-badge" className="text-xs font-semibold text-primary bg-primary/10 px-2 py-1 rounded">
      {selected} / {total}
    </span>
  );
}

const CategoryCard: FC<CategoryCardProps> = (props) => {
  const IconComponent = resolveIcon(props.iconName);
  const [badgeVisible, setBadgeVisible] = useState(false);

  useEffect(() => {
    setBadgeVisible(true);
  }, []);

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      props.onClick();
    }
  }

  return (
    <div
      data-testid="category-card"
      role="button"
      tabIndex={0}
      aria-current={props.isActive ? 'true' : undefined}
      title={props.description}
      onClick={props.onClick}
      onKeyDown={handleKeyDown}
      className={cardClasses(props.isActive)}
    >
      <IconComponent aria-hidden="true" size={18} />

      <span className="truncate flex-1 min-w-0" title={props.name}>
        {props.name}
      </span>

      {props.selectedCount !== undefined && props.totalCount !== undefined ? (
        <span className={`transition-opacity duration-200 motion-reduce:transition-none ${badgeVisible ? 'opacity-100' : 'opacity-0'}`}>
          <CountBadge selected={props.selectedCount} total={props.totalCount} />
        </span>
      ) : props.hint ? (
        props.hint
      ) : null}
    </div>
  );
};

export default CategoryCard;
