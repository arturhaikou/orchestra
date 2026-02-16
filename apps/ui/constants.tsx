import { MarkerType } from 'reactflow';

export const STATUSES = {
  OPEN: { name: 'Open', color: 'bg-blue-500/20 text-blue-400' },
  IN_PROGRESS: { name: 'In Progress', color: 'bg-yellow-500/20 text-yellow-400' },
  REVIEW: { name: 'Review', color: 'bg-purple-500/20 text-purple-400' },
  DONE: { name: 'Done', color: 'bg-emerald-500/20 text-emerald-400' },
  BLOCKED: { name: 'Blocked', color: 'bg-red-500/20 text-red-400' },
};

export const PRIORITIES = {
  LOW: { name: 'Low', color: 'bg-slate-500/10 text-slate-400 border border-slate-500/20', value: 0 },
  MEDIUM: { name: 'Medium', color: 'bg-blue-500/10 text-blue-400 border border-blue-500/20', value: 1 },
  HIGH: { name: 'High', color: 'bg-orange-500/10 text-orange-400 border border-orange-500/20', value: 2 },
  CRITICAL: { name: 'Critical', color: 'bg-red-500/10 text-red-400 border border-red-500/20', value: 3 },
};

export const INITIAL_WORKFLOW_NODES = [
  { 
    id: '1', 
    type: 'input', 
    data: { label: 'Ticket Created' }, 
    position: { x: 250, y: 0 },
    style: { background: '#1e293b', color: '#fff', border: '1px solid #475569' }
  },
  { 
    id: '2', 
    data: { label: 'Write Code (Agent)' }, 
    position: { x: 250, y: 100 },
    style: { background: '#1e293b', color: '#fff', border: '1px solid #6366f1' }
  },
  { 
    id: '3', 
    data: { label: 'Run Tests' }, 
    position: { x: 100, y: 200 },
    style: { background: '#1e293b', color: '#fff', border: '1px solid #10b981' }
  },
  { 
    id: '4', 
    data: { label: 'Manual Review' }, 
    position: { x: 400, y: 200 },
    style: { background: '#1e293b', color: '#fff', border: '1px solid #f59e0b' }
  },
];

export const INITIAL_WORKFLOW_EDGES = [
  { id: 'e1-2', source: '1', target: '2', animated: true, style: { stroke: '#64748b' } },
  { id: 'e2-3', source: '2', target: '3', label: 'Auto', markerEnd: { type: MarkerType.ArrowClosed }, style: { stroke: '#64748b' } },
  { id: 'e2-4', source: '2', target: '4', label: 'Manual', markerEnd: { type: MarkerType.ArrowClosed }, style: { stroke: '#64748b' } },
];
