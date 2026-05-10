import {
  BookOpen,
  ClipboardList,
  Code2,
  FileText,
  GitBranch,
  GitPullRequest,
  LayoutGrid,
  Layers,
  MessageSquare,
  TicketCheck,
  type LucideIcon,
} from 'lucide-react';

const CATEGORY_ICON_MAP: Record<string, LucideIcon> = {
  'cat-github':      GitBranch,
  'cat-jira':        TicketCheck,
  'cat-confluence':  BookOpen,
  'cat-linear':      Layers,
  'cat-slack':       MessageSquare,
  'cat-notion':      FileText,
  'cat-cr':          GitPullRequest,
  'cat-code-review': Code2,
  'cat-tracker':     ClipboardList,
  'cat-tickets':     ClipboardList,
};

const CATEGORY_ICON_NAME_MAP: Record<string, string> = {
  'cat-github':      'GitBranch',
  'cat-jira':        'TicketCheck',
  'cat-confluence':  'BookOpen',
  'cat-linear':      'Layers',
  'cat-slack':       'MessageSquare',
  'cat-notion':      'FileText',
  'cat-cr':          'GitPullRequest',
  'cat-code-review': 'Code2',
  'cat-tracker':     'ClipboardList',
  'cat-tickets':     'ClipboardList',
};

const CATEGORY_DESCRIPTION_MAP: Record<string, string> = {
  'cat-github':      'Source control: create PRs, review code, manage branches',
  'cat-jira':        'Ticket tracking: read, create, and update issues',
  'cat-confluence':  'Knowledge base: read and search documentation',
  'cat-linear':      'Project management: manage issues, cycles, and projects',
  'cat-slack':       'Messaging: send and read channel messages',
  'cat-notion':      'Notes and docs: read, create, and update pages',
  'cat-cr':          'Code review: read PRs, post comments, approve changes',
  'cat-code-review': 'Code review: read PRs, post comments, approve changes',
  'cat-tracker':     'Ticket tracker: read, create, update, and delete tickets',
  'cat-tickets':     'Ticket tracker: read, create, update, and delete tickets',
};

export function getCategoryIcon(sourceId: string): LucideIcon {
  return CATEGORY_ICON_MAP[sourceId] ?? LayoutGrid;
}

export function getCategoryIconName(sourceId: string): string {
  return CATEGORY_ICON_NAME_MAP[sourceId] ?? 'LayoutGrid';
}

export function getCategoryDescription(sourceId: string, sourceName: string): string {
  return CATEGORY_DESCRIPTION_MAP[sourceId] ?? `${sourceName} tools`;
}
