import React, { useEffect, useState, useMemo, useCallback } from 'react';
import {
  X, Play, CheckCircle, XCircle, Wrench, MessageSquare, Zap,
  ChevronRight, ChevronDown, GitBranch, Clock,
} from 'lucide-react';
import { marked } from 'marked';
import { JobDetail, JobStep, JobStepType, WorkflowStep } from '../../../types';
import { getJobDetail } from '../../../services/jobService';
import { onJobStepAdded, onJobStatusChanged } from '../../../services/signalRService';
import { useSignalRReady } from '../../../hooks/useSignalRReady';
import { buildExecutionTree } from '../../../utils/executionTree';
import JobStatusBadge from '../../jobs/JobStatusBadge';
import SubAgentCallNode from '../../jobs/SubAgentCallNode';

// ── Step rendering (mirrored from JobDetailPage) ──────────────────────────────

const numericStepTypeMap: Record<number, JobStepType> = {
  0: 'AgentStarted', 1: 'ThinkingMessage', 2: 'ToolCallStarted', 3: 'ToolCallCompleted',
  4: 'AgentCompleted', 5: 'AgentFailed', 6: 'SubAgentCallStarted', 7: 'SubAgentCallCompleted',
};

const stepIcon: Record<JobStepType, React.ReactNode> = {
  AgentStarted:          <Play className="w-4 h-4 text-emerald-400" />,
  ThinkingMessage:       <MessageSquare className="w-4 h-4 text-blue-400" />,
  ToolCallStarted:       <Wrench className="w-4 h-4 text-yellow-400" />,
  ToolCallCompleted:     <CheckCircle className="w-4 h-4 text-emerald-400" />,
  AgentCompleted:        <Zap className="w-4 h-4 text-emerald-400" />,
  AgentFailed:           <XCircle className="w-4 h-4 text-red-400" />,
  SubAgentCallStarted:   null,
  SubAgentCallCompleted: null,
};

function getStepLabel(step: JobStep): string {
  const t: JobStepType = typeof step.stepType === 'number'
    ? numericStepTypeMap[step.stepType] ?? 'AgentStarted'
    : step.stepType;
  switch (t) {
    case 'AgentStarted':          return 'Agent started';
    case 'ThinkingMessage':       return 'Thinking…';
    case 'ToolCallStarted':       return step.toolName ? `Call: ${step.toolName}` : 'Tool call';
    case 'ToolCallCompleted':     return step.toolName ? `Result: ${step.toolName}` : 'Tool result';
    case 'AgentCompleted':        return 'Agent completed';
    case 'AgentFailed':           return 'Agent failed';
    case 'SubAgentCallStarted':   return step.agentName ?? step.toolName ?? 'Sub-agent';
    case 'SubAgentCallCompleted': return `${step.agentName ?? step.toolName ?? 'Sub-agent'} done`;
    default:                      return 'Step';
  }
}

function unwrapJson(raw: string): string {
  const t = raw.trim();
  if (t.startsWith('"') && t.endsWith('"')) {
    try { return JSON.parse(t) as string; } catch { /* not JSON string */ }
  }
  return raw;
}

const PROSE = 'prose prose-sm max-w-none prose-headings:text-indigo-400 prose-headings:font-semibold prose-p:text-text prose-li:text-text prose-strong:text-text prose-a:text-indigo-400 prose-code:text-emerald-400 prose-pre:bg-black';

const MarkdownContent: React.FC<{ content: string }> = ({ content }) => {
  const html = useMemo(() => marked.parse(unwrapJson(content), { async: false }) as string, [content]);
  return <div className={PROSE} dangerouslySetInnerHTML={{ __html: html }} />;
};

const StepRow: React.FC<{ step: JobStep }> = ({ step }) => {
  const [expanded, setExpanded] = useState(false);
  const normalizedType: JobStepType = typeof step.stepType === 'number'
    ? numericStepTypeMap[step.stepType] ?? 'AgentStarted'
    : step.stepType;
  const isExpandable = !!(step.content && normalizedType !== 'AgentStarted');
  const fmtJson = useMemo(() => {
    if (!step.isJson || !step.content) return step.content ?? '';
    try { return JSON.stringify(JSON.parse(step.content), null, 2); } catch { return step.content; }
  }, [step.content, step.isJson]);

  return (
    <div className={`border-l-2 pl-4 py-2 ${step.isError ? 'border-red-500' : 'border-border'}`}>
      <div
        className={`flex items-center gap-2 ${isExpandable ? 'cursor-pointer' : ''}`}
        onClick={() => isExpandable && setExpanded(e => !e)}
      >
        {stepIcon[normalizedType]}
        <span className="text-text text-sm font-medium flex-1">{getStepLabel(step)}</span>
        {step.durationMs != null && <span className="text-xs text-textMuted">{step.durationMs}ms</span>}
        <span className="text-xs text-textMuted">{new Date(step.timestamp).toLocaleTimeString()}</span>
        {isExpandable && (expanded ? <ChevronDown className="w-3 h-3 text-textMuted shrink-0" /> : <ChevronRight className="w-3 h-3 text-textMuted shrink-0" />)}
      </div>
      {expanded && step.content && (
        <div className="mt-2">
          {step.isJson
            ? <pre className="bg-black text-emerald-400 rounded p-3 text-xs font-mono overflow-x-auto whitespace-pre-wrap">{fmtJson}</pre>
            : <MarkdownContent content={step.content} />}
        </div>
      )}
    </div>
  );
};

// ── Panel ─────────────────────────────────────────────────────────────────────

interface JobDetailPanelProps {
  step: WorkflowStep;
  jobId?: string;
  onClose: () => void;
}

const JobDetailPanel: React.FC<JobDetailPanelProps> = ({ step, jobId, onClose }) => {
  const [job, setJob] = useState<JobDetail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const isSignalRReady = useSignalRReady();

  const fetchJob = useCallback(async (id: string) => {
    setIsLoading(true);
    try {
      const data = await getJobDetail(id);
      setJob(data);
    } catch {
      // silently fail — panel shows placeholder
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!jobId) { setJob(null); return; }
    fetchJob(jobId);
  }, [jobId, fetchJob]);

  useEffect(() => {
    if (!isSignalRReady || !jobId) return;

    const offStep = onJobStepAdded((data: any) => {
      if (data.jobId !== jobId) return;
      const newStep: JobStep = {
        id: data.stepId, stepType: data.stepType as JobStepType,
        sequence: data.sequence, timestamp: data.timestamp,
        content: data.content, toolName: data.toolName,
        isJson: data.isJson, durationMs: data.durationMs,
        isError: data.isError,
        parentStepId: data.parentStepId ?? undefined,
        agentId: data.agentId ?? undefined,
        agentName: data.agentName ?? undefined,
      };
      setJob(prev => {
        if (!prev) return prev;
        const steps = [...prev.steps, newStep].sort((a, b) => a.sequence - b.sequence);
        return { ...prev, steps };
      });
    });

    const offStatus = onJobStatusChanged((data: any) => {
      if (data.jobId !== jobId) return;
      setJob(prev => prev ? { ...prev, status: data.newStatus } : prev);
    });

    return () => { offStep(); offStatus(); };
  }, [isSignalRReady, jobId]);

  const isCondition = step.stepType === 'Condition';
  const duration = job?.startedAt && job?.completedAt
    ? Math.round((new Date(job.completedAt).getTime() - new Date(job.startedAt).getTime()) / 1000)
    : null;

  return (
    <div className="flex flex-col h-full w-[380px] min-w-[320px] max-w-[420px] bg-surface border-l border-border">
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border shrink-0">
        <div className="flex items-center gap-2 min-w-0">
          <GitBranch className="w-4 h-4 text-indigo-400 shrink-0" />
          <span className="text-sm font-semibold text-text truncate">
            {isCondition ? 'Condition' : (step.agentName || 'Agent step')}
          </span>
        </div>
        <div className="flex items-center gap-2 ml-2 shrink-0">
          {job && <JobStatusBadge status={job.status} />}
          <button
            onClick={onClose}
            className="p-1 rounded text-textMuted hover:text-text hover:bg-surfaceHighlight transition-colors"
          >
            <X className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto px-4 py-4 space-y-4">
        {isCondition ? (
          <div className="space-y-3">
            <div>
              <p className="text-xs font-semibold text-textMuted uppercase tracking-wider mb-1">Condition</p>
              <p className="text-sm text-text bg-black/20 rounded-lg p-3 italic">
                {step.condition || 'No condition set'}
              </p>
            </div>
            <p className="text-xs text-textMuted">
              Conditions are evaluated by the AI model against the previous step's output.
            </p>
          </div>
        ) : (
          <>
            {/* Meta */}
            {(duration !== null || job) && (
              <div className="flex items-center gap-4 text-xs text-textMuted">
                {duration !== null && (
                  <span className="flex items-center gap-1">
                    <Clock className="w-3 h-3" /> {duration}s
                  </span>
                )}
              </div>
            )}

            {/* Steps timeline */}
            {isLoading && (
              <p className="text-sm text-textMuted text-center py-4">Loading…</p>
            )}

            {!isLoading && !jobId && (
              <p className="text-sm text-textMuted text-center py-4">Not started yet.</p>
            )}

            {!isLoading && jobId && job && job.steps.length === 0 && (
              <p className="text-sm text-textMuted text-center py-4">No steps recorded yet.</p>
            )}

            {!isLoading && job && job.steps.length > 0 && (
              <div className="space-y-1">
                <p className="text-xs font-semibold text-textMuted uppercase tracking-wider mb-2">Execution Timeline</p>
                {buildExecutionTree(job.steps).map(node => {
                  const t: JobStepType = typeof node.step.stepType === 'number'
                    ? numericStepTypeMap[node.step.stepType] ?? 'AgentStarted'
                    : node.step.stepType;
                  if (t === 'SubAgentCallStarted') {
                    return (
                      <SubAgentCallNode
                        key={node.step.id}
                        node={node}
                        renderStepRow={s => <StepRow key={s.id} step={s} />}
                      />
                    );
                  }
                  return <StepRow key={node.step.id} step={node.step} />;
                })}
              </div>
            )}

            {!isLoading && job?.finalResponse && (
              <div className="bg-black/20 rounded-xl p-3">
                <p className="text-xs font-semibold text-textMuted uppercase tracking-wider mb-2">Final Response</p>
                <MarkdownContent content={job.finalResponse} />
              </div>
            )}

            {!isLoading && job?.errorMessage && (
              <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-3">
                <p className="text-xs font-semibold text-red-400 uppercase tracking-wider mb-2">Error</p>
                <p className="text-red-400 text-sm font-mono">{job.errorMessage}</p>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
};

export default JobDetailPanel;
