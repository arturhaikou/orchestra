import React, { useEffect, useState, useCallback, useMemo } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ChevronLeft, Square, Loader2 } from 'lucide-react';
import { WorkflowDefinition, WorkflowExecution, WorkflowStep } from '../../types';
import { getWorkflowExecution, getWorkflowDefinition } from '../../services/workflowService';
import { cancelJob } from '../../services/jobService';
import {
  onWorkflowStepStarted,
  onWorkflowStepCompleted,
  onWorkflowExecutionStatusChanged,
  onWorkflowStepJobAssigned,
} from '../../services/signalRService';
import { useSignalRReady } from '../../hooks/useSignalRReady';
import JobStatusBadge from '../jobs/JobStatusBadge';
import { type JobStatus } from '../../types';
import WorkflowExecutionCanvas from '../workflows/execution/WorkflowExecutionCanvas';
import JobDetailPanel from '../workflows/execution/JobDetailPanel';

type LoadState = 'loading' | 'ready' | 'error';

const WorkflowExecutionPage: React.FC = () => {
  const { workspaceId, executionId } = useParams<{ workspaceId: string; executionId: string }>();
  const navigate = useNavigate();
  const isSignalRReady = useSignalRReady();

  const [loadState, setLoadState] = useState<LoadState>('loading');
  const [execution, setExecution] = useState<WorkflowExecution | null>(null);
  const [definition, setDefinition] = useState<WorkflowDefinition | null>(null);
  const [cancelling, setCancelling] = useState(false);

  // Side panel state
  const [selectedStep, setSelectedStep] = useState<WorkflowStep | null>(null);

  const selectedJobId = useMemo(() => {
    if (!selectedStep || !execution || !definition) return undefined;
    const stepIndex = definition.steps.findIndex(s => s.id === selectedStep.id);
    if (stepIndex === -1) return undefined;
    return execution.stepExecutions.find(se => se.stepIndex === stepIndex)?.jobId ?? undefined;
  }, [selectedStep, execution, definition]);

  const loadData = useCallback(async () => {
    if (!executionId) return;
    try {
      const exec = await getWorkflowExecution(executionId);
      const def  = await getWorkflowDefinition(exec.workflowDefinitionId);
      setExecution(exec);
      setDefinition(def);
      setLoadState('ready');
    } catch {
      setLoadState('error');
    }
  }, [executionId]);

  useEffect(() => { loadData(); }, [loadData]);

  // Real-time updates
  useEffect(() => {
    if (!isSignalRReady || !executionId) return;

    const offStarted = onWorkflowStepStarted(ev => {
      if (ev.workflowExecutionId !== executionId) return;
      setExecution(prev => {
        if (!prev) return prev;
        const existing = prev.stepExecutions.find(se => se.stepIndex === ev.stepIndex);
        if (existing) return { ...prev, stepExecutions: prev.stepExecutions.map(se => se.stepIndex === ev.stepIndex ? { ...se, status: 'Running' } : se) };
        return {
          ...prev,
          stepExecutions: [
            ...prev.stepExecutions,
            {
              id: `temp-${ev.stepIndex}`,
              workflowExecutionId: executionId,
              stepIndex: ev.stepIndex,
              status: 'Running',
              startedAt: new Date().toISOString(),
            },
          ],
        };
      });
    });

    const offCompleted = onWorkflowStepCompleted(ev => {
      if (ev.workflowExecutionId !== executionId) return;
      // Refetch to get condition output
      getWorkflowExecution(executionId)
        .then(exec => setExecution(exec))
        .catch(() => {});
    });

    const offStatus = onWorkflowExecutionStatusChanged(ev => {
      if (ev.workflowExecutionId !== executionId) return;
      setExecution(prev => prev ? { ...prev, status: ev.status as WorkflowExecution['status'] } : prev);
    });

    const offJobAssigned = onWorkflowStepJobAssigned(ev => {
      if (ev.workflowExecutionId !== executionId) return;
      setExecution(prev => {
        if (!prev) return prev;
        return {
          ...prev,
          stepExecutions: prev.stepExecutions.map(se =>
            se.stepIndex === ev.stepIndex ? { ...se, jobId: ev.jobId } : se
          ),
        };
      });
    });

    return () => { offStarted(); offCompleted(); offStatus(); offJobAssigned(); };
  }, [isSignalRReady, executionId]);

  const handleStop = async () => {
    if (!execution?.workflowJobId) return;
    setCancelling(true);
    try {
      await cancelJob(execution.workflowJobId);
    } catch {
      // ignore
    } finally {
      setCancelling(false);
    }
  };

  const handleNodeClick = (stepId: string, step: WorkflowStep) => {
    setSelectedStep(prev => prev?.id === stepId ? null : step);
  };

  const isStoppable = execution?.status === 'Running' || execution?.status === 'WaitingForInput';

  if (loadState === 'loading') {
    return (
      <div className="flex-1 flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-primary" />
      </div>
    );
  }

  if (loadState === 'error' || !execution || !definition) {
    return (
      <div className="flex-1 flex items-center justify-center text-red-400">
        Failed to load workflow execution.
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between px-5 py-3 border-b border-border shrink-0 bg-surface">
        <div className="flex items-center gap-3 min-w-0">
          <button
            onClick={() => navigate(`/workspaces/${workspaceId}/jobs`)}
            className="flex items-center gap-1 text-textMuted hover:text-text text-sm transition-colors shrink-0"
          >
            <ChevronLeft className="w-4 h-4" />
            Jobs
          </button>
          <span className="text-textMuted text-sm">|</span>
          <span className="text-sm font-semibold text-text truncate">{definition.name}</span>
        </div>

        <div className="flex items-center gap-3 shrink-0 ml-3">
          <JobStatusBadge status={execution.status as JobStatus} />
          {isStoppable && (
            <button
              onClick={handleStop}
              disabled={cancelling}
              className="flex items-center gap-1.5 text-red-400 hover:text-red-300 text-sm font-medium disabled:opacity-50"
            >
              <Square className="w-3.5 h-3.5" />
              {cancelling ? 'Stopping…' : 'Stop'}
            </button>
          )}
        </div>
      </div>

      {/* Canvas + side panel */}
      <div className="flex flex-1 overflow-hidden">
        <div className="flex-1 overflow-hidden">
          <WorkflowExecutionCanvas
            definition={definition}
            execution={execution}
            onNodeClick={handleNodeClick}
          />
        </div>

        {selectedStep && (
          <JobDetailPanel
            step={selectedStep}
            jobId={selectedJobId}
            onClose={() => setSelectedStep(null)}
          />
        )}
      </div>
    </div>
  );
};

export default WorkflowExecutionPage;
