import { JobStep, JobStepType } from '../types';

export interface ExecutionTreeNode {
  step: JobStep;
  children: ExecutionTreeNode[];
}

const numericTypeMap: Record<number, JobStepType> = {
  0: 'AgentStarted', 1: 'ThinkingMessage', 2: 'ToolCallStarted', 3: 'ToolCallCompleted',
  4: 'AgentCompleted', 5: 'AgentFailed', 6: 'SubAgentCallStarted', 7: 'SubAgentCallCompleted',
};

function resolveType(stepType: JobStepType | number): JobStepType {
  return typeof stepType === 'number' ? (numericTypeMap[stepType] ?? 'AgentStarted') : stepType;
}

export { resolveType };

export function buildExecutionTree(steps: JobStep[]): ExecutionTreeNode[] {
  const sorted = [...steps].sort((a, b) => a.sequence - b.sequence);
  const nodeMap = new Map<string, ExecutionTreeNode>();
  for (const step of sorted) nodeMap.set(step.id, { step, children: [] });

  // Pass 1: wire parentStepId-based children
  for (const step of sorted) {
    if (step.parentStepId && nodeMap.has(step.parentStepId)) {
      nodeMap.get(step.parentStepId)!.children.push(nodeMap.get(step.id)!);
    }
  }

  // Collect root steps in sequence order
  const rootSteps = sorted.filter(s => !s.parentStepId);
  const skipIds = new Set<string>();

  // Pass 2: remove ToolCallStarted/ToolCallCompleted wrappers around sub-agent calls
  //         and re-attach SubAgentCallCompleted as child of its SubAgentCallStarted
  for (let i = 0; i < rootSteps.length; i++) {
    const step = rootSteps[i];
    const type = resolveType(step.stepType);

    if (type === 'SubAgentCallStarted') {
      // The root step immediately before (by index) is the wrapping ToolCallStarted
      if (i > 0 && resolveType(rootSteps[i - 1].stepType) === 'ToolCallStarted') {
        skipIds.add(rootSteps[i - 1].id);
      }
    }

    if (type === 'SubAgentCallCompleted') {
      // The root step immediately after is the wrapping ToolCallCompleted
      if (i < rootSteps.length - 1 && resolveType(rootSteps[i + 1].stepType) === 'ToolCallCompleted') {
        skipIds.add(rootSteps[i + 1].id);
      }

      // Attach to the nearest preceding SubAgentCallStarted with the same toolName
      for (let j = i - 1; j >= 0; j--) {
        if (
          resolveType(rootSteps[j].stepType) === 'SubAgentCallStarted' &&
          rootSteps[j].toolName === step.toolName
        ) {
          nodeMap.get(rootSteps[j].id)!.children.push(nodeMap.get(step.id)!);
          skipIds.add(step.id);
          break;
        }
      }
    }
  }

  return rootSteps.filter(s => !skipIds.has(s.id)).map(s => nodeMap.get(s.id)!);
}
