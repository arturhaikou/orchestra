import React, { useEffect, useState, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ChevronLeft, Play, CheckCircle, XCircle, Wrench, MessageSquare, Zap, ChevronRight, ChevronDown, FileText, Square } from 'lucide-react';
import { marked } from 'marked';
import { JobStep, JobStepType } from '../../types';
import { cancelJob } from '../../services/jobService';
import { useJobDetail } from '../../hooks/useJobDetail';
import { useSignalRReady } from '../../hooks/useSignalRReady';
import { onJobStepAdded, onJobStatusChanged } from '../../services/signalRService';
import JobStatusBadge from '../jobs/JobStatusBadge';
import SubAgentCallNode from '../jobs/SubAgentCallNode';
import { buildExecutionTree } from '../../utils/executionTree';

const stepIcon: Record<JobStepType, React.ReactNode> = {
  AgentStarted:           <Play className="w-4 h-4 text-emerald-400" />,
  ThinkingMessage:        <MessageSquare className="w-4 h-4 text-blue-400" />,
  ToolCallStarted:        <Wrench className="w-4 h-4 text-yellow-400" />,
  ToolCallCompleted:      <CheckCircle className="w-4 h-4 text-emerald-400" />,
  AgentCompleted:         <Zap className="w-4 h-4 text-emerald-400" />,
  AgentFailed:            <XCircle className="w-4 h-4 text-red-400" />,
  SubAgentCallStarted:    null, // rendered by SubAgentCallNode
  SubAgentCallCompleted:  null, // rendered by SubAgentCallNode
};

// Map numeric enum values to string enum names (fallback for enum serialization issues)
const numericStepTypeMap: Record<number, JobStepType> = {
  0: 'AgentStarted',
  1: 'ThinkingMessage',
  2: 'ToolCallStarted',
  3: 'ToolCallCompleted',
  4: 'AgentCompleted',
  5: 'AgentFailed',
  6: 'SubAgentCallStarted',
  7: 'SubAgentCallCompleted',
};

const getStepLabel = (step: JobStep): string => {
  const normalizedType: JobStepType = 
    typeof step.stepType === 'number' 
      ? numericStepTypeMap[step.stepType] ?? 'AgentStarted'
      : step.stepType;

  switch (normalizedType) {
    case 'AgentStarted':          return 'Agent started';
    case 'ThinkingMessage':       return 'Thinking…';
    case 'ToolCallStarted':       return step.toolName ? `Call: ${step.toolName}` : 'Tool call';
    case 'ToolCallCompleted':     return step.toolName ? `Result: ${step.toolName}` : 'Tool result';
    case 'AgentCompleted':        return 'Agent completed';
    case 'AgentFailed':           return 'Agent failed';
    case 'SubAgentCallStarted':   return step.agentName ?? step.toolName ?? 'Sub-agent';
    case 'SubAgentCallCompleted': return `${step.agentName ?? step.toolName ?? 'Sub-agent'} done`;
    default:                      return 'Unknown step';
  }
};

const PROSE_CLASSES = 'prose prose-sm max-w-none prose-headings:text-indigo-400 prose-headings:font-semibold prose-p:text-text prose-li:text-text prose-strong:text-text prose-a:text-indigo-400 prose-code:text-emerald-400 prose-pre:bg-black';

function unwrapJsonString(raw: string): string {
  const t = raw.trim();
  if (t.startsWith('"') && t.endsWith('"')) {
    try { return JSON.parse(t) as string; } catch { /* not valid JSON */ }
  }
  return raw;
}

const MarkdownContent: React.FC<{ content: string; className?: string }> = ({ content, className }) => {
  const html = useMemo(() => marked.parse(unwrapJsonString(content), { async: false }) as string, [content]);
  return (
    <div
      className={`${PROSE_CLASSES} ${className ?? ''}`}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
};

const InitialContextSection: React.FC<{ initialPrompt: string }> = ({ initialPrompt }) => {
  const [expanded, setExpanded] = useState(false);

  if (!initialPrompt) return null;

  return (
    <div className="mb-6 bg-surface border border-border rounded-xl overflow-hidden">
      <button
        onClick={() => setExpanded(e => !e)}
        className="w-full flex items-center gap-3 p-4 hover:bg-opacity-50 transition-colors"
      >
        <FileText className="w-5 h-5 text-blue-400 flex-shrink-0" />
        <span className="text-sm font-semibold text-textMuted uppercase tracking-wider flex-1">Initial Context</span>
        {expanded
          ? <ChevronDown className="w-4 h-4 text-textMuted flex-shrink-0" />
          : <ChevronRight className="w-4 h-4 text-textMuted flex-shrink-0" />
        }
      </button>
      {expanded && (
        <div className="border-t border-border px-4 py-3 bg-black/20">
          <MarkdownContent content={initialPrompt} className="text-text text-sm" />
        </div>
      )}
    </div>
  );
};

const StepRow: React.FC<{ step: JobStep }> = ({ step }) => {
  const [expanded, setExpanded] = useState(false);

  const normalizedType: JobStepType =
    typeof step.stepType === 'number'
      ? numericStepTypeMap[step.stepType] ?? 'AgentStarted'
      : step.stepType;

  const isExpandable = !!(step.content && normalizedType !== 'AgentStarted');

  const formattedJson = useMemo(() => {
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
        <span className="text-text text-sm font-medium flex-1">
          {getStepLabel(step)}
        </span>
        {step.durationMs !== undefined && step.durationMs !== null && (
          <span className="text-xs text-textMuted">{step.durationMs}ms</span>
        )}
        <span className="text-xs text-textMuted ml-2">{new Date(step.timestamp).toLocaleTimeString()}</span>
        {isExpandable && (
          expanded
            ? <ChevronDown className="w-3 h-3 text-textMuted shrink-0" />
            : <ChevronRight className="w-3 h-3 text-textMuted shrink-0" />
        )}
      </div>
      {expanded && step.content && (
        <div className="mt-2">
          {step.isJson ? (
            <pre className="bg-black text-emerald-400 rounded p-3 text-xs font-mono overflow-x-auto whitespace-pre-wrap">{formattedJson}</pre>
          ) : (
            <MarkdownContent content={step.content} className="text-text" />
          )}
        </div>
      )}
    </div>
  );
};

const JobDetailPage: React.FC = () => {
  const { workspaceId, jobId } = useParams<{ workspaceId: string; jobId: string }>();
  const navigate = useNavigate();
  const { job, isLoading, error, refetch } = useJobDetail(jobId!);
  const isSignalRReady = useSignalRReady();
  const [cancelling, setCancelling] = useState(false);

  const isStoppable = job?.status === 'Running' || job?.status === 'WaitingForInput';

  const handleStop = async () => {
    if (!job) return;
    setCancelling(true);
    try {
      await cancelJob(job.id);
      refetch();
    } catch {
      setCancelling(false);
    }
  };

  useEffect(() => {
    if (!job || !isSignalRReady) return;
    
    const offStep = onJobStepAdded((data) => {
      if (data.jobId === jobId) refetch();
    });
    const offStatus = onJobStatusChanged((data) => {
      if (data.jobId === jobId) refetch();
    });
    return () => { offStep(); offStatus(); };
  }, [jobId, job, isSignalRReady, refetch]);

  if (!workspaceId || !jobId) return null;
  if (isLoading) return <div className="flex-1 flex items-center justify-center text-textMuted">Loading...</div>;
  if (error || !job) return <div className="flex-1 flex items-center justify-center text-red-400">{error || 'Job not found'}</div>;

  return (
    <div className="flex-1 p-6 max-w-4xl mx-auto w-full">
      <button
        onClick={() => navigate(`/workspaces/${workspaceId}/jobs`)}
        className="flex items-center gap-1 text-textMuted hover:text-text text-sm mb-6 transition-colors"
      >
        <ChevronLeft className="w-4 h-4" /> Back to Jobs
      </button>

      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-xl font-bold text-text">{job.agentName}</h1>
          {job.ticketTitle && <p className="text-textMuted text-sm mt-1">{job.ticketTitle}</p>}
        </div>
        <div className="flex items-center gap-3">
          {isStoppable && (
            <button
              onClick={handleStop}
              disabled={cancelling}
              className="flex items-center gap-1.5 text-red-400 hover:text-red-300 text-sm font-medium disabled:opacity-50"
            >
              <Square className="w-4 h-4" />
              {cancelling ? 'Stopping…' : 'Stop'}
            </button>
          )}
          <JobStatusBadge status={job.status} />
        </div>
      </div>

      <InitialContextSection initialPrompt={job.initialPrompt} />

      {job.steps.length > 0 && (
        <div className="space-y-1 mb-6">
          <h2 className="text-sm font-semibold text-textMuted uppercase tracking-wider mb-3">Execution Timeline</h2>
          {buildExecutionTree(job.steps).map(node => {
            const stepType = typeof node.step.stepType === 'number'
              ? numericStepTypeMap[node.step.stepType] ?? 'AgentStarted'
              : node.step.stepType;

            if (stepType === 'SubAgentCallStarted') {
              return (
                <SubAgentCallNode
                  key={node.step.id}
                  node={node}
                  renderStepRow={step => <StepRow key={step.id} step={step} />}
                />
              );
            }

            return <StepRow key={node.step.id} step={node.step} />;
          })}
        </div>
      )}

      {job.finalResponse && (
        <div className="bg-surface border border-border rounded-xl p-4">
          <h2 className="text-sm font-semibold text-textMuted uppercase tracking-wider mb-3">Final Response</h2>
          <MarkdownContent content={job.finalResponse} className="text-text" />
        </div>
      )}

      {job.errorMessage && (
        <div className="bg-red-500/10 border border-red-500/20 rounded-xl p-4">
          <h2 className="text-sm font-semibold text-red-400 uppercase tracking-wider mb-3">Error</h2>
          <p className="text-red-400 text-sm font-mono">{job.errorMessage}</p>
        </div>
      )}
    </div>
  );
};

export default JobDetailPage;
