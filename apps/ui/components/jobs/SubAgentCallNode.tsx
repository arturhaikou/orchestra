import React, { useState, useMemo } from 'react';
import { Bot, ChevronRight, ChevronDown, MessageSquare } from 'lucide-react';
import { marked } from 'marked';
import { JobStep, JobStepType } from '../../types';
import { ExecutionTreeNode, resolveType } from '../../utils/executionTree';

interface SubAgentCallNodeProps {
  node: ExecutionTreeNode;
  renderStepRow: (step: JobStep) => React.ReactNode;
}

/** Unwrap JSON-encoded strings (e.g. `"text\nmore"` → `text\nmore`). */
function unwrapJsonString(raw: string): string {
  const t = raw.trim();
  if (t.startsWith('"') && t.endsWith('"')) {
    try { return JSON.parse(t) as string; } catch { /* not valid JSON */ }
  }
  return raw;
}

function useMarkdown(text: string | undefined): string {
  return useMemo(() => {
    if (!text) return '';
    return marked.parse(unwrapJsonString(text), { async: false }) as string;
  }, [text]);
}

const PROSE_CLASSES = 'prose prose-sm max-w-none prose-headings:text-indigo-400 prose-headings:font-semibold prose-p:text-text prose-li:text-text prose-strong:text-text prose-a:text-indigo-400 prose-code:text-emerald-400 prose-pre:bg-black';

const MarkdownBlock: React.FC<{ content: string; className?: string }> = ({ content, className }) => {
  const html = useMarkdown(content);
  return (
    <div
      className={`${PROSE_CLASSES} ${className ?? ''}`}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
};

interface CollapsibleSectionProps {
  label: string;
  icon: React.ReactNode;
  content: string;
  isJson?: boolean;
  defaultOpen?: boolean;
}

const CollapsibleSection: React.FC<CollapsibleSectionProps> = ({ label, icon, content, isJson, defaultOpen = false }) => {
  const [open, setOpen] = useState(defaultOpen);

  const formattedJson = useMemo(() => {
    if (!isJson) return content;
    try { return JSON.stringify(JSON.parse(content), null, 2); } catch { return content; }
  }, [content, isJson]);

  return (
    <div className="border-l-2 border-border pl-3 py-1">
      <div
        className="flex items-center gap-2 cursor-pointer"
        onClick={() => setOpen(o => !o)}
      >
        {icon}
        <span className="text-xs text-textMuted uppercase tracking-wider font-semibold">{label}</span>
        {open ? <ChevronDown className="w-3 h-3 text-textMuted ml-auto" /> : <ChevronRight className="w-3 h-3 text-textMuted ml-auto" />}
      </div>
      {open && (
        <div className="mt-2">
          {isJson ? (
            <pre className="bg-black text-emerald-400 rounded p-3 text-xs font-mono overflow-x-auto whitespace-pre-wrap">{formattedJson}</pre>
          ) : (
            <MarkdownBlock content={content} className="mt-1" />
          )}
        </div>
      )}
    </div>
  );
};

const SubAgentCallNode: React.FC<SubAgentCallNodeProps> = ({ node, renderStepRow }) => {
  const [isExpanded, setIsExpanded] = useState(false);
  const { step, children } = node;

  const completedStep = children.find(c => resolveType(c.step.stepType) === 'SubAgentCallCompleted')?.step;
  const isFailed = children.some(c => resolveType(c.step.stepType) === 'AgentFailed' && c.step.isError);
  const durationMs = completedStep?.durationMs ?? step.durationMs;

  const internalSteps = children.filter(c => resolveType(c.step.stepType) !== 'SubAgentCallCompleted');

  return (
    <div className={`border-l-2 pl-4 py-1 ${step.isError || isFailed ? 'border-red-500' : 'border-purple-500/60'}`}>
      {/* Header */}
      <div
        className="flex items-center gap-2 cursor-pointer"
        onClick={() => setIsExpanded(e => !e)}
      >
        <Bot className="w-4 h-4 text-purple-400 shrink-0" />
        <span className="text-text text-sm font-medium flex-1">
          {step.agentName ?? step.toolName ?? 'Sub-agent'}
        </span>
        {durationMs != null && <span className="text-xs text-textMuted">{durationMs}ms</span>}
        <span className="text-xs text-textMuted ml-2">{new Date(step.timestamp).toLocaleTimeString()}</span>
        {isExpanded
          ? <ChevronDown className="w-3 h-3 text-textMuted shrink-0" />
          : <ChevronRight className="w-3 h-3 text-textMuted shrink-0" />}
      </div>

      {/* Expanded body */}
      {isExpanded && (
        <div className="mt-2 ml-2 space-y-1">
          {/* Query — collapsible, hidden by default */}
          {step.content && (
            <CollapsibleSection
              label="Query"
              icon={<MessageSquare className="w-3 h-3 text-blue-400" />}
              content={step.content}
              defaultOpen={false}
            />
          )}

          {/* Internal execution steps */}
          {internalSteps.map(child =>
            resolveType(child.step.stepType) === 'SubAgentCallStarted' ? (
              <SubAgentCallNode key={child.step.id} node={child} renderStepRow={renderStepRow} />
            ) : (
              <React.Fragment key={child.step.id}>{renderStepRow(child.step)}</React.Fragment>
            )
          )}

          {/* Response from SubAgentCallCompleted */}
          {completedStep?.content && (
            <CollapsibleSection
              label="Response"
              icon={<Bot className="w-3 h-3 text-purple-400" />}
              content={completedStep.content}
              defaultOpen={false}
            />
          )}
        </div>
      )}
    </div>
  );
};

export default SubAgentCallNode;
