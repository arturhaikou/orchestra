
import React, { useState, useEffect } from 'react';
import { Loader2, Terminal, RefreshCw, XCircle, Clock } from 'lucide-react';
import { Job } from '../types';
import { getJobs, cancelJob } from '../services/jobService';

interface JobsListProps {
  workspaceId: string;
}

const JobsList: React.FC<JobsListProps> = ({ workspaceId }) => {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const fetchJobs = async () => {
    try {
      const data = await getJobs(workspaceId);
      setJobs(data);
    } catch (e) {
      console.error(e);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchJobs();
    const interval = setInterval(fetchJobs, 5000);
    return () => clearInterval(interval);
  }, [workspaceId]);

  const handleCancel = async (id: string) => {
    await cancelJob(id);
    await fetchJobs();
  };

  if (isLoading && jobs.length === 0) {
      return (
          <div className="flex h-full items-center justify-center">
              <Loader2 className="w-8 h-8 animate-spin text-primary" />
          </div>
      );
  }

  return (
    <div className="h-full flex flex-col gap-6 animate-fade-in">
       <div className="flex justify-between items-center">
          <div>
            <h2 className="text-2xl font-bold text-text">Job Operations</h2>
            <p className="text-textMuted text-sm mt-1">Real-time execution monitoring for active tasks.</p>
          </div>
          <button onClick={fetchJobs} className="p-2 hover:bg-surfaceHighlight rounded-md text-textMuted transition-colors">
            <RefreshCw className={`w-5 h-5 ${isLoading ? 'animate-spin' : ''}`} />
          </button>
       </div>
       
       {jobs.length === 0 ? (
         <div className="flex-1 flex flex-col items-center justify-center border-2 border-dashed border-border rounded-lg bg-surface/30">
           <Terminal className="w-12 h-12 text-textMuted mb-4 opacity-20" />
           <p className="text-textMuted">No active jobs in queue.</p>
         </div>
       ) : (
         <div className="grid grid-cols-1 xl:grid-cols-2 gap-6 overflow-y-auto pb-10 pr-2 custom-scrollbar">
            {jobs.map(job => (
              <div key={job.id} className="bg-surface border border-border rounded-xl flex flex-col overflow-hidden shadow-xl hover:border-primary/40 transition-all">
                  <div className="bg-surfaceHighlight/50 px-4 py-3 border-b border-border flex justify-between items-center">
                    <div className="flex items-center gap-3">
                        <div className={`w-2.5 h-2.5 rounded-full ${job.status === 'IN_PROGRESS' ? 'bg-emerald-400 animate-pulse' : 'bg-blue-400'}`} />
                        <span className="text-text font-mono text-xs font-bold">{job.id}</span>
                        <span className="bg-primary/10 text-primary text-[10px] font-black px-2 py-0.5 rounded uppercase tracking-tighter">
                            {job.type || 'TASK'}
                        </span>
                    </div>
                    {job.status === 'IN_PROGRESS' && (
                        <button 
                            onClick={() => handleCancel(job.id)}
                            className="p-1 text-textMuted hover:text-red-500 transition-colors"
                            title="Cancel Execution"
                        >
                            <XCircle className="w-4 h-4" />
                        </button>
                    )}
                  </div>
                  
                  <div className="p-4 space-y-3">
                    <div className="flex justify-between items-end">
                      <div className="flex flex-col gap-1">
                          <span className="text-[10px] text-textMuted uppercase font-bold tracking-widest">Progress</span>
                          <span className="text-lg font-mono font-bold text-text">{job.progress}%</span>
                      </div>
                      <div className="flex flex-col items-end gap-1">
                          <span className="text-[10px] text-textMuted uppercase font-bold tracking-widest">Started At</span>
                          <div className="flex items-center gap-1.5 text-xs text-textMuted">
                              <Clock className="w-3 h-3" />
                              {new Date(job.startedAt).toLocaleTimeString()}
                          </div>
                      </div>
                    </div>
                    <div className="w-full bg-background border border-border h-2 rounded-full overflow-hidden">
                        <div className={`h-full transition-all duration-700 ease-out ${job.status === 'FAILED' ? 'bg-red-500' : 'bg-primary'}`} style={{ width: `${job.progress}%` }} />
                    </div>
                  </div>

                  <div className="bg-black p-4 space-y-1 min-h-[150px] max-h-[250px] overflow-y-auto custom-scrollbar font-mono text-[11px] selection:bg-emerald-500/30">
                    {job.logs.map((log, i) => (
                      <div key={i} className="text-emerald-500/70 hover:text-emerald-400 transition-colors py-0.5">
                        <span className="opacity-30 mr-2">[{i.toString().padStart(3, '0')}]</span>
                        {log}
                      </div>
                    ))}
                    {job.status === 'IN_PROGRESS' && (
                      <div className="text-emerald-500 animate-pulse mt-1 ml-9">_</div>
                    )}
                  </div>
              </div>
            ))}
         </div>
       )}
    </div>
  );
};

export default JobsList;
