import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { GitBranch, ChevronDown, ChevronRight, Square } from 'lucide-react';
import { JobSummary } from '../../types';
import JobStatusBadge from './JobStatusBadge';
import { cancelJob } from '../../services/jobService';

interface Props {
  job: JobSummary;
  stepJobs: JobSummary[];
  workspaceId: string;
  onCancelled?: () => void;
}

const WorkflowJobCard: React.FC<Props> = ({ job, stepJobs, workspaceId, onCancelled }) => {
  const navigate = useNavigate();
  const [expanded, setExpanded] = useState(true);
  const [cancelling, setCancelling] = useState(false);

  const isStoppable = job.status === 'Running' || job.status === 'WaitingForInput';

  const handleStop = async (e: React.MouseEvent) => {
    e.stopPropagation();
    setCancelling(true);
    try {
      await cancelJob(job.id);
      onCancelled?.();
    } catch {
      setCancelling(false);
    }
  };

  const duration = job.completedAt && job.startedAt
    ? Math.round((new Date(job.completedAt).getTime() - new Date(job.startedAt).getTime()) / 1000)
    : null;

  const sortedSteps = [...stepJobs].sort(
    (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
  );

  return (
    <div className="bg-surface border border-border border-l-4 border-l-indigo-500/60 rounded-xl p-4 flex flex-col gap-3">
      <div
        className="flex justify-between items-start cursor-pointer"
        onClick={() => setExpanded(e => !e)}
      >
        <div className="flex items-center gap-2 min-w-0">
          <GitBranch className="w-4 h-4 text-indigo-400 shrink-0" />
          <div className="min-w-0">
            <p className="text-text font-semibold text-sm">{job.agentName}</p>
            {job.ticketTitle && (
              <p className="text-textMuted text-xs mt-0.5 truncate max-w-[200px]">{job.ticketTitle}</p>
            )}
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0 ml-2">
          {isStoppable && (
            <button
              onClick={handleStop}
              disabled={cancelling}
              className="flex items-center gap-1 text-red-400 hover:text-red-300 text-xs font-medium disabled:opacity-50"
            >
              <Square className="w-3 h-3" />
              {cancelling ? 'Stopping…' : 'Stop'}
            </button>
          )}
          <JobStatusBadge status={job.status} />
          {expanded
            ? <ChevronDown className="w-4 h-4 text-textMuted" />
            : <ChevronRight className="w-4 h-4 text-textMuted" />}
        </div>
      </div>

      {expanded && sortedSteps.length > 0 && (
        <div className="border-t border-border pt-2 space-y-1">
          {sortedSteps.map((step, idx) => (
            <div
              key={step.id}
              className="flex items-center gap-2 px-2 py-1.5 rounded-lg hover:bg-surfaceHighlight cursor-pointer transition-colors"
              onClick={() => navigate(`/workspaces/${workspaceId}/jobs/${step.id}`)}
            >
              <span className="text-[11px] text-textMuted w-4 shrink-0 text-center font-mono">{idx + 1}</span>
              <span className="text-xs text-text flex-1 truncate">{step.agentName}</span>
              <JobStatusBadge status={step.status} />
            </div>
          ))}
        </div>
      )}

      {expanded && sortedSteps.length === 0 && (
        <p className="text-xs text-textMuted border-t border-border pt-2">No steps started yet.</p>
      )}

      <div className="flex items-center gap-3 text-xs text-textMuted border-t border-border pt-2 mt-auto">
        <span className="flex items-center gap-1">
          <GitBranch className="w-3 h-3" />
          Workflow
        </span>
        {duration !== null && <span>{duration}s</span>}
        {sortedSteps.length > 0 && (
          <span>{sortedSteps.length} step{sortedSteps.length !== 1 ? 's' : ''}</span>
        )}
        <span className="ml-auto">{new Date(job.createdAt).toLocaleString()}</span>
      </div>
    </div>
  );
};

export default WorkflowJobCard;
