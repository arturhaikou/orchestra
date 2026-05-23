import { useState, useEffect, useCallback } from 'react';
import { JobStatus, JobSummary } from '../types';
import { getJobs } from '../services/jobService';

export const useJobs = (workspaceId: string, statusFilter?: JobStatus) => {
  const [items, setItems] = useState<JobSummary[]>([]);
  const [total, setTotal] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchJobs = useCallback(async () => {
    try {
      setIsLoading(true);
      const result = await getJobs(workspaceId, statusFilter);
      setItems(result.items);
      setTotal(result.total);
      setError(null);
    } catch {
      setError('Failed to load jobs');
    } finally {
      setIsLoading(false);
    }
  }, [workspaceId, statusFilter]);

  useEffect(() => {
    fetchJobs();
  }, [fetchJobs]);

  return { items, setItems, total, isLoading, error, refetch: fetchJobs };
};
