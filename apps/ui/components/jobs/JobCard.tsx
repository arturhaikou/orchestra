import React from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Ticket, Terminal, Globe,
  Play, MessageSquare, Wrench, CheckCircle, Zap, XCircle, Bot,
} from 'lucide-react';
import { JobStep, JobStepType, JobSummary } from '../../types';
import JobStatusBadge from './JobStatusBadge';

interface Props {
  job: JobSummary;
  workspaceId: string;
  liveSteps?: JobStep[];
}

const triggerIcon: Record<string, React.ReactNode> = {
  Ticket: <Ticket className="w-3 h-3" />,
  ManualApi: <Globe className="w-3 h-3" />,
  Cli: <Terminal className="w-3 h-3" />,
};

const stepIcon: Record<JobStepType, React.ReactNode> = {
  AgentStarted:           <Play className="w-3 h-3 shrink-0 text-emerald-400" />,
  ThinkingMessage:        <MessageSquare className="w-3 h-3 shrink-0 text-blue-400" />,
  ToolCallStarted:        <Wrench className="w-3 h-3 shrink-0 text-yellow-400" />,
  ToolCallCompleted:      <CheckCircle className="w-3 h-3 shrink-0 text-emerald-400" />,
  AgentCompleted:         <Zap className="w-3 h-3 shrink-0 text-emerald-400" />,
  AgentFailed:            <XCircle className="w-3 h-3 shrink-0 text-red-400" />,
  SubAgentCallStarted:    <Bot className="w-3 h-3 shrink-0 text-purple-400" />,
  SubAgentCallCompleted:  <Bot className="w-3 h-3 shrink-0 text-purple-400" />,
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

const stepLabel = (step: JobStep): string => {
  // Normalize numeric stepType to string enum
  const normalizedType: JobStepType = 
    typeof step.stepType === 'number' 
      ? numericStepTypeMap[step.stepType] ?? 'AgentStarted'
      : step.stepType;

  switch (normalizedType) {
    case 'AgentStarted':      return 'Agent started';
    case 'ThinkingMessage':   return step.content ? step.content.slice(0, 48) + (step.content.length > 48 ? '…' : '') : 'Thinking…';
    case 'ToolCallStarted':   return step.toolName ? step.toolName : 'Tool call';
    case 'ToolCallCompleted': return step.toolName ? `${step.toolName} done` : 'Tool done';
    case 'AgentCompleted':    return 'Agent completed';
    case 'AgentFailed':       return 'Agent failed';
    default:                  return 'Unknown step';
  }
};

const JobCard: React.FC<Props> = ({ job, workspaceId, liveSteps = [] }) => {
  const navigate = useNavigate();

  const duration = job.completedAt && job.startedAt
    ? Math.round((new Date(job.completedAt).getTime() - new Date(job.startedAt).getTime()) / 1000)
    : null;

  const handleCardClick = () => navigate(`/workspaces/${workspaceId}/jobs/${job.id}`);

  const handleView = (e: React.MouseEvent) => {
    e.stopPropagation();
    navigate(`/workspaces/${workspaceId}/jobs/${job.id}`);
  };

  return (
    <div
      onClick={handleCardClick}
      className="bg-surface border border-border rounded-xl p-4 hover:border-primary/40 transition-all cursor-pointer flex flex-col gap-3"
    >
      <div className="flex justify-between items-start">
        <div>
          <p className="text-text font-semibold text-sm">{job.agentName}</p>
          {job.ticketTitle && (
            <p className="text-textMuted text-xs mt-0.5 truncate max-w-[220px]">{job.ticketTitle}</p>
          )}
        </div>
        <JobStatusBadge status={job.status} />
      </div>

      {liveSteps.length > 0 && (
        <div className="border-t border-border pt-2 space-y-1.5">
          {liveSteps.map(step => (
            <div key={step.id} className="flex items-center gap-2 min-w-0">
              {stepIcon[step.stepType]}
              <span className="text-xs text-textMuted truncate">{stepLabel(step)}</span>
            </div>
          ))}
        </div>
      )}

      <div className="flex items-center gap-3 text-xs text-textMuted border-t border-border pt-2 mt-auto">
        <span className="flex items-center gap-1">
          {triggerIcon[job.triggerType]}
          {job.triggerType}
        </span>
        {duration !== null && <span>{duration}s</span>}
        <span className="ml-auto">{new Date(job.createdAt).toLocaleString()}</span>
        <button
          onClick={handleView}
          className="text-primary hover:underline text-xs font-medium"
        >
          View →
        </button>
      </div>
    </div>
  );
};

export default JobCard;
