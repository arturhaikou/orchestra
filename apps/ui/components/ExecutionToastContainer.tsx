import React from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import ExecutionToast from './ExecutionToast';
import { useExecutionToasts } from '../hooks/useExecutionToasts';

const ExecutionToastContainer: React.FC = () => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const { toasts, dismiss } = useExecutionToasts(workspaceId || '');
  const navigate = useNavigate();

  const handleViewTicket = (ticketId: string) => {
    navigate(`/tickets/${ticketId}`);
  };

  if (toasts.length === 0) return null;

  return (
    <div className="fixed bottom-6 right-6 z-[100] w-96 max-w-[calc(100vw-3rem)] flex flex-col gap-3">
      {toasts.map((toast) => (
        <ExecutionToast
          key={toast.id}
          toast={toast}
          onDismiss={dismiss}
          onViewTicket={handleViewTicket}
        />
      ))}
    </div>
  );
};

export default ExecutionToastContainer;
