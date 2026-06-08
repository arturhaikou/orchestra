import Dagre from '@dagrejs/dagre';
import { type Node, type Edge, MarkerType } from '@xyflow/react';
import { WorkflowStep } from '../types';

export const STEP_W    = 240;
export const PILL_W    = 160;
export const NODE_Y_GAP = 140;
export const PILL_X_OFF = (STEP_W - PILL_W) / 2;

export const INDIGO  = '#6366f1';
export const EMERALD = '#10b981';
export const RED     = '#ef4444';
export const AMBER   = '#f59e0b';

export function computeAutoLayout(nodes: Node[], edges: Edge[]): Map<string, { x: number; y: number }> {
  const g = new Dagre.graphlib.Graph().setDefaultEdgeLabel(() => ({}));
  g.setGraph({ rankdir: 'TB', nodesep: 60, ranksep: 80 });

  nodes.forEach(n => {
    const isPill = n.type === 'startNode' || n.type === 'endNode';
    const isCond = n.type === 'condition' || n.type === 'execCondition';
    const w = isPill ? PILL_W : isCond ? 210 : STEP_W;
    const h = isPill ? 38     : isCond ? 80  : 72;
    g.setNode(n.id, { width: w, height: h });
  });

  edges.forEach(e => { try { g.setEdge(e.source, e.target); } catch { /* skip invalid */ } });
  Dagre.layout(g);

  const out = new Map<string, { x: number; y: number }>();
  nodes.forEach(n => {
    const nd = g.node(n.id);
    if (nd?.x != null) out.set(n.id, { x: nd.x - nd.width / 2, y: nd.y - nd.height / 2 });
  });
  return out;
}

function makeEdge(id: string, source: string, target: string, sourceHandle?: string): Edge {
  return {
    id,
    source,
    target,
    sourceHandle: sourceHandle ?? null,
    type: 'default',
    markerEnd: { type: MarkerType.ArrowClosed, width: 16, height: 16, color: INDIGO },
    style: { stroke: 'rgba(99,102,241,0.45)', strokeWidth: 2 },
    data: {},
  };
}

export interface ExecEdgeData extends Record<string, unknown> {
  conditionHandle?: 'true' | 'false';
}

export function buildExecutionGraph(steps: WorkflowStep[]): { nodes: Node[]; edges: Edge[] } {
  if (steps.length === 0) {
    const nodes: Node[] = [
      { id: 'start', type: 'startNode', position: { x: PILL_X_OFF, y: 0 }, data: {}, selectable: false },
      { id: 'end-default', type: 'endNode', position: { x: PILL_X_OFF, y: NODE_Y_GAP }, data: {}, selectable: false },
    ];
    return { nodes, edges: [makeEdge('e-start-end', 'start', 'end-default')] };
  }

  const nodes: Node[] = [
    { id: 'start', type: 'startNode', position: { x: PILL_X_OFF, y: 0 }, data: {}, selectable: false },
  ];
  const edges: Edge[] = [];
  const stepIdSet = new Set(steps.map(s => s.id));
  const sortedSteps = [...steps].sort((a, b) => a.order - b.order);
  const baseY = 120 + steps.length * NODE_Y_GAP;

  steps.forEach((step, i) => {
    const nodeId = `step-${step.id}`;
    nodes.push({
      id: nodeId,
      type: step.stepType === 'Condition' ? 'execCondition' : 'execAgent',
      position: { x: 0, y: 120 + i * NODE_Y_GAP },
      data: { step },
    });
  });

  const isLegacyLinear = steps.every(
    s => !s.trueNextStepId && !s.falseNextStepId && s.stepType !== 'Condition'
  );

  if (isLegacyLinear) {
    const endId = 'end-default';
    nodes.push({ id: endId, type: 'endNode', position: { x: PILL_X_OFF, y: baseY }, data: {}, selectable: false });
    sortedSteps.forEach((step, i) => {
      const cur  = `step-${step.id}`;
      const prev = i === 0 ? 'start' : `step-${sortedSteps[i - 1].id}`;
      edges.push(makeEdge(`e-${prev}-${step.id}`, prev, cur));
    });
    const lastId = `step-${sortedSteps[sortedSteps.length - 1].id}`;
    edges.push(makeEdge(`e-${lastId}-end`, lastId, endId));
  } else {
    edges.push(makeEdge(`e-start-${sortedSteps[0].id}`, 'start', `step-${sortedSteps[0].id}`));

    steps.forEach((step, i) => {
      const nodeId = `step-${step.id}`;
      if (step.stepType === 'Condition') {
        const trueResolved  = !!(step.trueNextStepId  && stepIdSet.has(step.trueNextStepId));
        const falseResolved = !!(step.falseNextStepId && stepIdSet.has(step.falseNextStepId));
        const trueTarget  = trueResolved  ? `step-${step.trueNextStepId}`  : `end-${step.id}-true`;
        const falseTarget = falseResolved ? `step-${step.falseNextStepId}` : `end-${step.id}-false`;

        if (!trueResolved)  nodes.push({ id: `end-${step.id}-true`,  type: 'endNode', position: { x: i * 60,      y: baseY }, data: {}, selectable: false });
        if (!falseResolved) nodes.push({ id: `end-${step.id}-false`, type: 'endNode', position: { x: i * 60 - 80, y: baseY }, data: {}, selectable: false });

        edges.push({ ...makeEdge(`e-${step.id}-true`,  nodeId, trueTarget,  'true'),  data: { conditionHandle: 'true'  } as ExecEdgeData });
        edges.push({ ...makeEdge(`e-${step.id}-false`, nodeId, falseTarget, 'false'), data: { conditionHandle: 'false' } as ExecEdgeData });
      } else {
        const nextResolved = !!(step.trueNextStepId && stepIdSet.has(step.trueNextStepId));
        const nextTarget   = nextResolved ? `step-${step.trueNextStepId}` : `end-${step.id}`;
        if (!nextResolved) nodes.push({ id: `end-${step.id}`, type: 'endNode', position: { x: PILL_X_OFF, y: baseY }, data: {}, selectable: false });
        edges.push(makeEdge(`e-${step.id}-next`, nodeId, nextTarget));
      }
    });
  }

  const layout = computeAutoLayout(nodes, edges);
  const layoutedNodes = nodes.map(n => {
    const pos = layout.get(n.id);
    return pos ? { ...n, position: pos } : n;
  });

  return { nodes: layoutedNodes, edges };
}
