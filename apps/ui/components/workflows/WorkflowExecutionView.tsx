import React, { useEffect, useState } from 'react';
import { WorkflowDefinition, WorkflowExecution, WorkflowExecutionStatus } from '../../types';
import { getWorkflowDefinition, getWorkflowExecutionsByTicket } from '../../services/workflowService';
import { Loader2, CheckCircle2, XCircle, Clock, Pause, ArrowDown } from 'lucide-react';
import {
  onWorkflowStepStarted,
  onWorkflowStepCompleted,
  onWorkflowExecutionStatusChanged,
} from '../../services/signalRService';

interface WorkflowExecutionViewProps {
  ticketId: string;
  workflowDefinitionId: string;
}

const statusLabel: Record<string, string> = {
  Pending: 'Pending',
  Running: 'Running',
  WaitingForInput: 'Waiting for input',
  Completed: 'Completed',
  Failed: 'Failed',
};

const statusColor: Record<string, string> = {
  Pending: 'text-textMuted border-border',
  Running: 'text-blue-400 border-blue-400/40 bg-blue-400/5',
  WaitingForInput: 'text-yellow-400 border-yellow-400/40 bg-yellow-400/5',
  Completed: 'text-green-400 border-green-400/40 bg-green-400/5',
  Failed: 'text-red-400 border-red-400/40 bg-red-400/5',
};

const StepStatusIcon: React.FC<{ status: string }> = ({ status }) => {
  if (status === 'Completed') return <CheckCircle2 className="w-4 h-4 text-green-400" />;
  if (status === 'Failed') return <XCircle className="w-4 h-4 text-red-400" />;
  if (status === 'Running') return <Loader2 className="w-4 h-4 text-blue-400 animate-spin" />;
  if (status === 'WaitingForInput') return <Pause className="w-4 h-4 text-yellow-400" />;
  return <Clock className="w-4 h-4 text-textMuted" />;
};

const WorkflowExecutionView: React.FC<WorkflowExecutionViewProps> = ({ ticketId, workflowDefinitionId }) => {
  const [definition, setDefinition] = useState<WorkflowDefinition | null>(null);
  const [execution, setExecution] = useState<WorkflowExecution | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    setIsLoading(true);
    Promise.all([
      getWorkflowDefinition(workflowDefinitionId),
      getWorkflowExecutionsByTicket(ticketId),
    ])
      .then(([def, execs]) => {
        setDefinition(def);
        if (execs.length > 0) setExecution(execs[execs.length - 1]);
      })
      .catch(console.error)
      .finally(() => setIsLoading(false));
  }, [ticketId, workflowDefinitionId]);

  useEffect(() => {
    const offStepStarted = onWorkflowStepStarted(event => {
      if (event.ticketId !== ticketId) return;
      setExecution(prev => {
        if (!prev) return prev;
        return {
          ...prev,
          status: 'Running',
          currentStepIndex: event.stepIndex,
          stepExecutions: prev.stepExecutions.map(se =>
            se.stepIndex === event.stepIndex ? { ...se, status: 'Running' } : se
          ),
        };
      });
    });

    const offStepCompleted = onWorkflowStepCompleted(event => {
      if (event.ticketId !== ticketId) return;
      setExecution(prev => {
        if (!prev) return prev;
        return {
          ...prev,
          stepExecutions: prev.stepExecutions.map(se =>
            se.stepIndex === event.stepIndex ? { ...se, status: event.status as WorkflowExecutionStatus } : se
          ),
        };
      });
    });

    const offStatusChanged = onWorkflowExecutionStatusChanged(event => {
      if (event.ticketId !== ticketId) return;
      setExecution(prev => {
        if (!prev) return prev;
        return { ...prev, status: event.status as WorkflowExecutionStatus };
      });
    });

    return () => {
      offStepStarted();
      offStepCompleted();
      offStatusChanged();
    };
  }, [ticketId]);

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 py-3 text-textMuted text-sm">
        <Loader2 className="w-4 h-4 animate-spin" />
        Loading workflow...
      </div>
    );
  }

  if (!definition) return null;

  const getStepStatus = (stepIndex: number): string => {
    if (!execution) return 'Pending';
    const se = execution.stepExecutions.find(s => s.stepIndex === stepIndex);
    return se?.status ?? 'Pending';
  };

  const overallStatus = execution?.status ?? 'Pending';

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <span className="text-xs font-bold text-textMuted uppercase tracking-wider">{definition.name}</span>
        <span className={`text-xs px-2 py-0.5 rounded border ${statusColor[overallStatus]}`}>
          {statusLabel[overallStatus] ?? overallStatus}
        </span>
      </div>

      {definition.steps.map((step, index) => {
        const stepStatus = getStepStatus(index);
        return (
          <div key={step.id}>
            <div className={`flex items-center gap-3 p-3 rounded-lg border ${statusColor[stepStatus]}`}>
              <StepStatusIcon status={stepStatus} />
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-text truncate">{step.agentName}</p>
                <p className="text-xs text-textMuted">Step {index + 1}</p>
              </div>
              {step.passPreviousOutput && (
                <span className="text-[10px] text-primary bg-primary/10 px-1.5 py-0.5 rounded shrink-0">↻ Chain</span>
              )}
            </div>
            {index < definition.steps.length - 1 && (
              <div className="flex justify-center py-1">
                <ArrowDown className="w-3 h-3 text-textMuted" />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
};

export default WorkflowExecutionView;
