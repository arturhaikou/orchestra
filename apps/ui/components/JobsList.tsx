
import React, { useState } from 'react';
import { Activity } from 'lucide-react';
import { JobStatus } from '../types';
import { useJobs } from '../hooks/useJobs';
import JobCard from './jobs/JobCard';
import JobStatusFilter from './jobs/JobStatusFilter';

interface Props { workspaceId: string; }

const JobsList: React.FC<Props> = ({ workspaceId }) => {
  const [statusFilter, setStatusFilter] = useState<JobStatus | undefined>();
  const { items, isLoading, error } = useJobs(workspaceId, statusFilter);

  if (isLoading) return (
    <div className="flex h-full items-center justify-center text-textMuted">Loading jobs...</div>
  );

  if (error) return (
    <div className="flex h-full items-center justify-center text-red-400">{error}</div>
  );

  return (
    <div className="h-full flex flex-col gap-6 animate-fade-in">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-2xl font-bold text-text">Jobs</h2>
          <p className="text-textMuted text-sm mt-1">Agent execution history for this workspace.</p>
        </div>
        <JobStatusFilter selected={statusFilter} onChange={setStatusFilter} />
      </div>

      {items.length === 0 ? (
        <div className="flex-1 flex flex-col items-center justify-center border-2 border-dashed border-border rounded-lg bg-surface/30">
          <Activity className="w-12 h-12 text-textMuted mb-4 opacity-20" />
          <p className="text-textMuted">No jobs found.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 xl:grid-cols-2 gap-4 overflow-y-auto pb-10 pr-2 custom-scrollbar">
          {items.map(job => <JobCard key={job.id} job={job} workspaceId={workspaceId} />)}
        </div>
      )}
    </div>
  );
};

export default JobsList;
