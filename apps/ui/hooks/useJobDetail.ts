import { useState, useEffect, useCallback } from 'react';
import { JobDetail, JobStep, JobStepType } from '../types';
import { getJobDetail } from '../services/jobService';
import { onJobStepAdded, onJobStatusChanged, onReconnected } from '../services/signalRService';
import { useSignalRReady } from './useSignalRReady';

export const useJobDetail = (jobId: string) => {
  const [job, setJob] = useState<JobDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const isSignalRReady = useSignalRReady();

  useEffect(() => {
    const fetch = async () => {
      try {
        const data = await getJobDetail(jobId);
        setJob(data);
      } catch {
        setError('Failed to load job detail');
      } finally {
        setIsLoading(false);
      }
    };
    fetch();
  }, [jobId]);

  const refetch = useCallback(async () => {
    if (!jobId) return;
    try {
      const data = await getJobDetail(jobId);
      setJob(data);
      setError(null);
    } catch (err) {
      console.error('Failed to refetch job detail:', err);
      setError('Failed to refetch job detail');
    }
  }, [jobId]);

  useEffect(() => {
    if (!isSignalRReady || !jobId) return;

    const offStepAdded = onJobStepAdded((event: any) => {
      if (event.jobId !== jobId) return;
      const newStep: JobStep = {
        id: event.stepId,
        stepType: event.stepType as JobStepType,
        sequence: event.sequence,
        timestamp: event.timestamp,
        content: event.content,
        toolName: event.toolName,
        isJson: event.isJson,
        durationMs: event.durationMs,
        isError: event.isError,
        parentStepId: event.parentStepId ?? undefined,
        agentId: event.agentId ?? undefined,
        agentName: event.agentName ?? undefined,
      };
      setJob(prev => {
        if (!prev) return prev;
        const steps = [...prev.steps, newStep].sort((a, b) => a.sequence - b.sequence);
        return { ...prev, steps };
      });
    });

    const offStatusChanged = onJobStatusChanged((event: any) => {
      if (event.jobId !== jobId) return;
      setJob(prev => prev ? { ...prev, status: event.newStatus } : prev);
    });

    const unsubscribeReconnected = onReconnected(refetch);

    return () => {
      offStepAdded();
      offStatusChanged();
      unsubscribeReconnected();
    };
  }, [isSignalRReady, jobId, refetch]);

  return { job, isLoading, error, setJob, refetch };
};
