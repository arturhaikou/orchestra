import React from 'react';
import { useParams } from 'react-router-dom';
import WorkflowBuilder from '../WorkflowBuilder';

const WorkflowsPage: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();

  return (
    <div className="flex-1 p-6 overflow-y-auto">
      <WorkflowBuilder workspaceId={workspaceId!} />
    </div>
  );
};

export default WorkflowsPage;
