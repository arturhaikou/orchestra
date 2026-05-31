import React, { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { JobStatus, JobStep, JobStepType, JobSummary } from '../../types';
import { useJobs } from '../../hooks/useJobs';
import { useSignalRReady } from '../../hooks/useSignalRReady';
import JobCard from '../jobs/JobCard';
import WorkflowJobCard from '../jobs/WorkflowJobCard';
import JobStatusFilter from '../jobs/JobStatusFilter';
import { Activity } from 'lucide-react';
import { onJobCreated, onJobStatusChanged, onJobStepAdded, onReconnected } from '../../services/signalRService';

const JobsPage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const [statusFilter, setStatusFilter] = useState<JobStatus | undefined>();
  const [liveStepsMap, setLiveStepsMap] = useState<Map<string, JobStep[]>>(new Map());
  const { items, isLoading, error, refetch } = useJobs(workspaceId!, statusFilter);
  const isSignalRReady = useSignalRReady();

  useEffect(() => {
    if (!isSignalRReady) return;

    const offCreated = onJobCreated(() => refetch());
    const offChanged = onJobStatusChanged(() => refetch());
    const offStepAdded = onJobStepAdded((event: any) => {
      const step: JobStep = {
        id: event.stepId,
        stepType: event.stepType as JobStepType,
        sequence: event.sequence,
        timestamp: event.timestamp,
        content: event.content,
        toolName: event.toolName,
        isJson: event.isJson,
        durationMs: event.durationMs,
        isError: event.isError,
      };
      setLiveStepsMap(prev => {
        const current = prev.get(event.jobId) ?? [];
        const updated = [...current, step].slice(-3);
        const next = new Map(prev);
        next.set(event.jobId, updated);
        return next;
      });
    });

    const unsubscribeReconnected = onReconnected(refetch);

    return () => {
      offCreated();
      offChanged();
      offStepAdded();
      unsubscribeReconnected();
    };
  }, [isSignalRReady, refetch]);

  if (!workspaceId) return null;

  return (
    <div className="flex-1 p-6 max-w-6xl mx-auto w-full">
      <div className="flex justify-between items-center mb-6">
        <div>
          <h1 className="text-2xl font-bold text-text">Jobs</h1>
          <p className="text-textMuted text-sm mt-1">Agent execution history for this workspace.</p>
        </div>
        <JobStatusFilter selected={statusFilter} onChange={setStatusFilter} />
      </div>

      {isLoading && (
        <div className="flex items-center justify-center h-48 text-textMuted">Loading...</div>
      )}

      {error && (
        <div className="flex items-center justify-center h-48 text-red-400">{error}</div>
      )}

      {!isLoading && !error && items.length === 0 && (
        <div className="flex flex-col items-center justify-center h-64 border-2 border-dashed border-border rounded-lg">
          <Activity className="w-12 h-12 text-textMuted opacity-20 mb-4" />
          <p className="text-textMuted">No jobs yet. Run an agent to see executions here.</p>
        </div>
      )}

      {!isLoading && !error && items.length > 0 && (() => {
        const childrenByParent = new Map<string, JobSummary[]>();
        for (const job of items) {
          if (job.parentJobId) {
            const existing = childrenByParent.get(job.parentJobId) ?? [];
            childrenByParent.set(job.parentJobId, [...existing, job]);
          }
        }
        const displayItems = items.filter(j => !j.parentJobId);

        return (
          <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
            {displayItems.map(job => {
              if (job.workflowExecutionId) {
                return (
                  <WorkflowJobCard
                    key={job.id}
                    job={job}
                    stepJobs={childrenByParent.get(job.id) ?? []}
                    workspaceId={workspaceId}
                  />
                );
              }
              return (
                <JobCard
                  key={job.id}
                  job={job}
                  workspaceId={workspaceId}
                  liveSteps={liveStepsMap.get(job.id) ?? []}
                />
              );
            })}
          </div>
        );
      })()}
    </div>
  );
};

export default JobsPage;
