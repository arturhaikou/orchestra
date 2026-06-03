import React from 'react';
import { useParams, Link } from 'react-router-dom';
import {
  Loader2,
  AlertTriangle,
  ArrowLeft,
  Edit3,
  MessageSquare,
  Clock,
  ExternalLink,
} from 'lucide-react';
import { useTicketDetail } from '../../hooks/useTicketDetail';
import type { Comment } from '../../types';
import WorkflowExecutionView from '../workflows/WorkflowExecutionView';

const satisfactionColor = (score: number): string => {
  if (score >= 70) return 'text-green-400';
  if (score >= 40) return 'text-yellow-400';
  return 'text-red-400';
};

const satisfactionBgColor = (score: number): string => {
  if (score >= 70) return 'bg-green-400';
  if (score >= 40) return 'bg-yellow-400';
  return 'bg-red-400';
};

const TicketDetailPage: React.FC = () => {
  const { workspaceId, ticketId } = useParams<{ workspaceId: string; ticketId: string }>();
  const ticketsListPath = `/workspaces/${workspaceId}/tickets`;
  const editPath = `/workspaces/${workspaceId}/tickets/${ticketId}/edit`;

  const { ticket, isLoading, loadError } = useTicketDetail(workspaceId, ticketId);

  if (isLoading) {
    return (
      <div className="flex-1 p-6 max-w-5xl mx-auto">
        <Link to={ticketsListPath} className="flex items-center gap-2 text-sm text-textMuted hover:text-text transition-colors mb-6">
          <ArrowLeft className="w-4 h-4" /> Back to Tickets
        </Link>
        <div className="flex items-center justify-center py-20">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
        </div>
      </div>
    );
  }

  if (loadError || !ticket) {
    return (
      <div className="flex-1 p-6 max-w-5xl mx-auto">
        <div className="text-center py-20">
          <AlertTriangle className="w-12 h-12 text-red-400 mx-auto mb-4" />
          <h2 className="text-xl font-bold text-text mb-2">Ticket Not Found</h2>
          <p className="text-textMuted mb-6">The ticket you're looking for doesn't exist or has been removed.</p>
          <Link to={ticketsListPath} className="inline-flex items-center gap-2 px-4 py-2 bg-primary hover:bg-primaryHover text-white rounded-lg text-sm font-bold transition-all">
            Return to Tickets List
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="flex-1 p-6 max-w-5xl mx-auto" data-testid="ticket-detail-page">
      <div className="flex items-center justify-between mb-6">
        <Link to={ticketsListPath} className="flex items-center gap-2 text-sm text-textMuted hover:text-text transition-colors">
          <ArrowLeft className="w-4 h-4" /> Back to Tickets
        </Link>
        <Link
          to={editPath}
          className="inline-flex items-center gap-2 px-4 py-2 bg-primary hover:bg-primaryHover text-white rounded-lg text-sm font-bold transition-all"
        >
          <Edit3 className="w-4 h-4" /> Edit
        </Link>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <div>
            <div className="flex items-center gap-3 mb-2">
              <h1 className="text-2xl font-bold bg-gradient-to-r from-text to-textMuted bg-clip-text text-transparent">{ticket.title}</h1>
            </div>
            <p className="text-xs text-textMuted font-mono">
              {ticket.id.length > 20 ? ticket.id.substring(0, 8) + '...' : ticket.id}
              {!ticket.internal && ticket.externalTicketId && (
                <span className="ml-2 text-blue-400">{ticket.externalTicketId}</span>
              )}
            </p>
          </div>

          <div className="bg-surface border border-border rounded-lg p-4">
            <h2 className="text-sm font-bold text-textMuted uppercase tracking-wider mb-3">Description</h2>
            <p className="text-sm text-text whitespace-pre-wrap">{ticket.description || 'No description provided.'}</p>
          </div>

          {ticket.summary && (
            <div className="bg-surface border border-border rounded-lg p-4">
              <h2 className="text-sm font-bold text-textMuted uppercase tracking-wider mb-3">AI Summary</h2>
              <p className="text-sm text-text">{ticket.summary}</p>
            </div>
          )}

          <div className="bg-surface border border-border rounded-lg p-4">
            <h2 className="text-sm font-bold text-textMuted uppercase tracking-wider mb-3 flex items-center gap-2">
              <MessageSquare className="w-4 h-4" />
              Comments ({ticket.comments?.length || 0})
            </h2>
            {ticket.comments && ticket.comments.length > 0 ? (
              <div className="space-y-4">
                {ticket.comments.map((comment: Comment) => (
                  <div key={comment.id} data-testid="comment-item" className="border-b border-border pb-4 last:border-0 last:pb-0">
                    {comment.timestamp && (
                      <div className="flex items-center gap-1 mb-1">
                        <Clock className="w-3 h-3 text-textMuted" />
                        <span className="text-xs text-textMuted">
                          {new Date(comment.timestamp).toLocaleString()}
                        </span>
                      </div>
                    )}
                    <p className="text-sm text-text">{comment.content}</p>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-sm text-textMuted">No comments yet.</p>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <div className="bg-surface border border-border rounded-lg p-4 space-y-3">
            <h2 className="text-sm font-bold text-textMuted uppercase tracking-wider">Details</h2>

            <div className="flex items-center justify-between">
              <span className="text-xs text-textMuted">Status</span>
              {ticket.status ? (
                <span
                  className="px-2 py-0.5 rounded text-xs font-bold"
                  style={{ backgroundColor: ticket.status.color + '20', color: ticket.status.color }}
                >
                  {ticket.status.name}
                </span>
              ) : (
                <span className="text-xs text-textMuted">&mdash;</span>
              )}
            </div>

            <div className="flex items-center justify-between">
              <span className="text-xs text-textMuted">Priority</span>
              {ticket.priority ? (
                <span
                  className="px-2 py-0.5 rounded text-xs font-bold"
                  style={{ backgroundColor: ticket.priority.color + '20', color: ticket.priority.color }}
                >
                  {ticket.priority.name}
                </span>
              ) : (
                <span className="text-xs text-textMuted">&mdash;</span>
              )}
            </div>

            <div className="flex items-center justify-between">
              <span className="text-xs text-textMuted">Source</span>
              <span className="text-xs text-text">{ticket.internal ? 'Internal' : ticket.source}</span>
            </div>

            {!ticket.internal && ticket.externalUrl && (
              <div className="flex items-center justify-between">
                <span className="text-xs text-textMuted">External Link</span>
                <a
                  href={ticket.externalUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-xs text-primary hover:text-primaryHover flex items-center gap-1"
                >
                  <ExternalLink className="w-3 h-3" /> View in {ticket.source}
                </a>
              </div>
            )}
          </div>

          <div className="bg-surface border border-border rounded-lg p-4 space-y-3">
            <h2 className="text-sm font-bold text-textMuted uppercase tracking-wider">Assignment</h2>
            <div className="flex items-center justify-between">
              <span className="text-xs text-textMuted">Agent</span>
              <span className="text-xs text-text">{ticket.assignedAgentId || '\u2014'}</span>
            </div>
            {ticket.assignedWorkflowId ? (
              <WorkflowExecutionView ticketId={ticket.id} workflowDefinitionId={ticket.assignedWorkflowId} />
            ) : (
              <div className="flex items-center justify-between">
                <span className="text-xs text-textMuted">Workflow</span>
                <span className="text-xs text-text">&mdash;</span>
              </div>
            )}
          </div>

          <div className="bg-surface border border-border rounded-lg p-4 space-y-3">
            <h2 className="text-sm font-bold text-textMuted uppercase tracking-wider">Satisfaction</h2>
            {ticket.satisfaction != null ? (
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className={`text-2xl font-bold ${satisfactionColor(ticket.satisfaction)}`}>
                    {ticket.satisfaction}
                  </span>
                  <span className="text-xs text-textMuted">/ 100</span>
                </div>
                <div className="w-full bg-border rounded-full h-2">
                  <div
                    className={`h-2 rounded-full ${satisfactionBgColor(ticket.satisfaction)}`}
                    style={{ width: `${ticket.satisfaction}%` }}
                  />
                </div>
              </div>
            ) : (
              <p className="text-xs text-textMuted">Not available</p>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};

export default TicketDetailPage;
