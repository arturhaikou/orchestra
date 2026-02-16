
import React, { useState, useEffect, useRef } from 'react';
import { Bot, Sparkles, RefreshCw, X, Send, Loader2, MessageSquare, Plus, Save, Database, Globe, Workflow as WorkflowIcon, Flag, Activity, ChevronDown, Clock, ChevronRight, Layers, Smile, Meh, Frown, ExternalLink, Zap, Trash2, AlertTriangle, User } from 'lucide-react';
import { marked } from 'marked';
import { Ticket, TicketPriority, TicketStatus, Comment, Workflow, Agent } from '../types';
import { addComment, createTicket, updateTicket, getTickets, convertToExternal, deleteTicket, getTicketStatuses, getTicketPriorities, generateSummary } from '../services/ticketService';
import { getWorkspacesWorkflows } from '../services/workflowService';
import { getUser } from '../services/authService';
import { getAgents } from '../services/agentService';
import { IntegrationSelector } from './IntegrationSelector';
import { IssueTypeSelector } from './IssueTypeSelector';

interface TicketListProps {
  workspaceId: string;
  onNavigateToTickets?: () => void;
}

const Markdown: React.FC<{ content: string; className?: string }> = ({ content, className = '' }) => {
  if (!content) return null;
  const html = marked.parse(content, { breaks: true });
  return (
    <div 
      className={`prose prose-sm dark:prose-invert max-w-none prose-p:leading-relaxed prose-headings:mb-2 prose-headings:mt-4 first:prose-headings:mt-0 ${className}`}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
};

const TicketList: React.FC<TicketListProps> = ({ workspaceId, onNavigateToTickets }) => {
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const selectedTicketRef = useRef<Ticket | null>(null);
  const [workflows, setWorkflows] = useState<Workflow[]>([]);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [statuses, setStatuses] = useState<TicketStatus[]>([]);
  const [priorities, setPriorities] = useState<TicketPriority[]>([]);
  
  // Pagination state
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [nextPageToken, setNextPageToken] = useState<string | undefined>(undefined);
  const [isLastPage, setIsLastPage] = useState(false);

  const [summary, setSummary] = useState<string>('');
  const [loadingSummary, setLoadingSummary] = useState(false);
  
  // Comment state
  const [newComment, setNewComment] = useState('');
  const [isPostingComment, setIsPostingComment] = useState(false);
  
  // Edit Ticket State (Assignments & Details)
  const [editForm, setEditForm] = useState<{ 
    assignedAgentId?: string; 
    assignedWorkflowId?: string;
    status?: string;
    priority?: string;
  }>({});
  const [isUpdating, setIsUpdating] = useState(false);
  
  // Description editing state
  const [isEditingDescription, setIsEditingDescription] = useState(false);
  const [descriptionValue, setDescriptionValue] = useState('');
  const [isSavingDescription, setIsSavingDescription] = useState(false);
  const [isConverting, setIsConverting] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [deleteConfirmationId, setDeleteConfirmationId] = useState<string | null>(null);
  
  // Conversion state
  const [conversionConfig, setConversionConfig] = useState<{
    integrationId: string;
    integrationName: string;
    issueTypeName: string;
  } | null>(null);
  const [showConversionForm, setShowConversionForm] = useState(false);

  // Create Ticket State
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isCreating, setIsCreating] = useState(false);
  const [newTicketData, setNewTicketData] = useState({
      title: '',
      description: '',
      assignedAgentId: '',
      assignedWorkflowId: '',
      priorityId: '',
      statusId: ''
  });
  
  // Read state for unread counts
  const [lastReadCounts, setLastReadCounts] = useState<Record<string, number>>({});

  useEffect(() => {
      selectedTicketRef.current = selectedTicket;
  }, [selectedTicket]);

  useEffect(() => {
    const loadMetadata = async () => {
      try {
        const [statusesData, prioritiesData, agentsData] = await Promise.all([
          getTicketStatuses(),
          getTicketPriorities(),
          getAgents(workspaceId)
        ]);
        setStatuses(statusesData);
        setPriorities(prioritiesData);
        setAgents(agentsData);
        // Set defaults if not already set
        if (!newTicketData.statusId && statusesData.length > 0) {
          setNewTicketData(prev => ({ ...prev, statusId: statusesData[0].id }));
        }
        if (!newTicketData.priorityId && prioritiesData.length > 0) {
          const mediumPriority = prioritiesData.find(p => p.name === 'Medium') || prioritiesData[0];
          setNewTicketData(prev => ({ ...prev, priorityId: mediumPriority.id }));
        }
      } catch (err) {
        console.error('Failed to load ticket metadata', err);
      }
    };
    loadMetadata();
  }, [workspaceId]);

  const getPriorityWeight = (p: TicketPriority | null) => {
    return p?.value ?? 0; // Default to 0 if priority is null
  };

  const loadInitialTickets = async () => {
    setIsLoading(true);
    try {
      const response = await getTickets(workspaceId, undefined, 10); 
      const sortedItems = [...response.items].sort((a, b) => getPriorityWeight(b.priority) - getPriorityWeight(a.priority));
      setTickets(sortedItems);
      setNextPageToken(response.nextPageToken);
      setIsLastPage(response.isLast);

      // Initialize read counts
      const reads: Record<string, number> = {};
      sortedItems.forEach(t => {
          const key = `nexus_read_count_${t.id}`;
          const stored = localStorage.getItem(key);
          reads[t.id] = stored ? parseInt(stored, 10) : (t.comments?.length || 0);
      });
      setLastReadCounts(reads);
    } catch (err) {
      console.error("Failed to load initial tickets", err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleLoadMore = async () => {
    if (isLastPage || isLoadingMore) return;
    
    setIsLoadingMore(true);
    try {
      const response = await getTickets(workspaceId, nextPageToken, 10);
      // Deduplicate tickets by ID before adding to list
      const combinedTickets = [...tickets, ...response.items];
      const uniqueTickets = combinedTickets.filter((ticket, index, arr) => 
        arr.findIndex(t => t.id === ticket.id) === index
      );
      const sortedTickets = [...uniqueTickets].sort((a, b) => getPriorityWeight(b.priority) - getPriorityWeight(a.priority));
      
      setTickets(sortedTickets);
      setNextPageToken(response.nextPageToken);
      setIsLastPage(response.isLast);

      const reads = { ...lastReadCounts };
      response.items.forEach(t => {
          const key = `nexus_read_count_${t.id}`;
          const stored = localStorage.getItem(key);
          reads[t.id] = stored ? parseInt(stored, 10) : (t.comments?.length || 0);
      });
      setLastReadCounts(reads);
    } catch (err) {
      console.error("Failed to load more tickets", err);
    } finally {
      setIsLoadingMore(false);
    }
  };

  useEffect(() => {
    loadInitialTickets();

    const loadWorkflows = async () => {
        const wfs = await getWorkspacesWorkflows(workspaceId);
        setWorkflows(wfs);
    };
    loadWorkflows();
  }, [workspaceId]);

  const updateReadCount = (ticketId: string, count: number) => {
      localStorage.setItem(`nexus_read_count_${ticketId}`, count.toString());
      setLastReadCounts(prev => ({ ...prev, [ticketId]: count }));
  };

  const handleOpenTicket = (ticket: Ticket) => {
    setSelectedTicket(ticket);
    setEditForm({ 
        assignedAgentId: ticket.assignedAgentId || '',
        assignedWorkflowId: ticket.assignedWorkflowId || '',
        status: ticket.status?.name || 'Unknown',
        priority: ticket.priority?.name || 'Unknown'
    });
    setSummary(ticket.summary || ''); 
    setNewComment('');
    updateReadCount(ticket.id, ticket.comments?.length || 0);
  };

  const handleUpdateTicket = async () => {
    if (!selectedTicket) return;
    setIsUpdating(true);
    try {
        const updates: any = {
            assignedAgentId: editForm.assignedAgentId || undefined,
            assignedWorkflowId: editForm.assignedWorkflowId || undefined
        };

        if (selectedTicket.internal) {
           const statusObj = statuses.find(s => s.name === editForm.status);
           if (statusObj) updates.statusId = statusObj.id;

           const priorityObj = priorities.find(p => p.name === editForm.priority);
           if (priorityObj) updates.priorityId = priorityObj.id;
        }

        const updatedTicket = await updateTicket(selectedTicket.id, updates);
        setTickets(prev => prev.map(t => t.id === selectedTicket.id ? updatedTicket : t));
        setSelectedTicket(updatedTicket);
    } catch (error) {
        console.error("Failed to update ticket", error);
        // Show error feedback to user
        const errorMessage = error instanceof Error ? error.message : "Failed to update ticket. Please try again.";
    } finally {
        setIsUpdating(false);
    }
  };

  const handleSaveDescription = async () => {
    if (!selectedTicket || !selectedTicket.internal) return;
    
    // Validation
    const trimmedDescription = descriptionValue.trim();
    if (!trimmedDescription) {
      alert('Description cannot be empty.');
      return;
    }
    if (trimmedDescription.length > 5000) {
      alert('Description cannot exceed 5,000 characters.');
      return;
    }

    setIsSavingDescription(true);
    try {
      const updates = { description: trimmedDescription };
      await updateTicket(selectedTicket.id, updates);
      
      // Update local state
      const updatedTicket = { ...selectedTicket, description: trimmedDescription };
      setTickets(prev => prev.map(t => t.id === selectedTicket.id ? updatedTicket : t));
      setSelectedTicket(updatedTicket);
      setIsEditingDescription(false);
    } catch (error) {
      console.error('Failed to update description', error);
      alert('Failed to save description. Please try again.');
    } finally {
      setIsSavingDescription(false);
    }
  };

  const handleCancelEditDescription = () => {
    setDescriptionValue(selectedTicket?.description || '');
    setIsEditingDescription(false);
  };

  const handleStartEditDescription = () => {
    if (selectedTicket) {
      setDescriptionValue(selectedTicket.description);
      setIsEditingDescription(true);
    }
  };

  const handleDeleteTicket = async () => {
    if (!deleteConfirmationId) return;
    setIsDeleting(true);
    try {
        await deleteTicket(deleteConfirmationId);
        setTickets(prev => prev.filter(t => t.id !== deleteConfirmationId));
        setDeleteConfirmationId(null);
        setSelectedTicket(null);
    } catch (error) {
        console.error("Failed to delete ticket", error);
    } finally {
        setIsDeleting(false);
    }
  };

  const handleConvertToExternal = async () => {
    if (!selectedTicket || !selectedTicket.internal || !conversionConfig) return;
    
    setIsConverting(true);
    try {
      const updated = await convertToExternal(
        selectedTicket.id,
        conversionConfig.integrationId,
        conversionConfig.issueTypeName
      );
      
      setTickets(prev => prev.map(t => t.id === selectedTicket.id ? updated : t));
      setSelectedTicket(null);
      setConversionConfig(null);
      setShowConversionForm(false);
    } catch (error: any) {
      console.error("Conversion failed", error);
      alert(error?.message || "Failed to convert ticket to external system");
    } finally {
      setIsConverting(false);
    }
  };

  const handleGenerateSummary = async (ticket: Ticket) => {
    setLoadingSummary(true);
    try {
        // Call backend API instead of directly calling external AI service
        const ticketWithSummary = await generateSummary(ticket.id);
        setSummary(ticketWithSummary.summary || "No summary generated.");
        
        // Update the selected ticket with the new summary
        setSelectedTicket(prev => prev ? { ...prev, summary: ticketWithSummary.summary } : null);
    } catch (e) {
        console.error("Failed to generate summary:", e);
        setSummary("Error generating summary. Please try again.");
    } finally {
        setLoadingSummary(false);
    }
  };

  const handlePostComment = async () => {
    if (!newComment.trim() || !selectedTicket) return;

    setIsPostingComment(true);
    try {
      const user = getUser();
      const authorName = user ? user.name : 'Current User';
      
      const comment = await addComment(selectedTicket.id, newComment, authorName);
      
      const updatedTicket = {
        ...selectedTicket,
        comments: [...(selectedTicket.comments || []), comment]
      };
      setSelectedTicket(updatedTicket);

      setTickets(prevTickets => 
        prevTickets.map(t => t.id === selectedTicket.id ? updatedTicket : t)
      );
      
      updateReadCount(selectedTicket.id, updatedTicket.comments?.length || 0);

      setNewComment('');
    } catch (error) {
      console.error("Failed to post comment", error);
    } finally {
      setIsPostingComment(false);
    }
  };

  const handleCreateTicket = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTicketData.title || !newTicketData.description) return;

    setIsCreating(true);
    try {
        const createdTicket = await createTicket(workspaceId, {
            title: newTicketData.title,
            description: newTicketData.description,
            statusId: newTicketData.statusId,
            priorityId: newTicketData.priorityId,
            assignedAgentId: newTicketData.assignedAgentId,
            assignedWorkflowId: newTicketData.assignedWorkflowId
        });
        // Ensure comments array exists
        if (!createdTicket.comments) {
            createdTicket.comments = [];
        }
        setTickets(prev => [createdTicket, ...prev]);
        onNavigateToTickets?.();
        setIsCreateModalOpen(false);
        const defaultStatus = statuses[0]?.id || '';
        const defaultPriority = priorities.find(p => p.name === 'Medium')?.id || priorities[0]?.id || '';
        setNewTicketData({ 
            title: '', 
            description: '', 
            assignedAgentId: '', 
            assignedWorkflowId: '',
            priorityId: defaultPriority,
            statusId: defaultStatus
        });
    } catch (error) {
        console.error("Failed to create ticket", error);
    } finally {
        setIsCreating(false);
    }
  };

  const getSatisfactionColor = (score: number) => {
    if (score >= 80) return 'bg-emerald-500 shadow-[0_0_10px_rgba(16,185,129,0.3)]';
    if (score >= 50) return 'bg-yellow-500 shadow-[0_0_10px_rgba(234,179,8,0.3)]';
    return 'bg-red-500 shadow-[0_0_10px_rgba(239,68,68,0.3)]';
  };

  const getSatisfactionTextColor = (score: number) => {
    if (score >= 80) return 'text-emerald-400';
    if (score >= 50) return 'text-yellow-400';
    return 'text-red-400';
  };

  const getSatisfactionIcon = (score: number, size: number = 4) => {
    if (score >= 80) return <Smile className={`w-${size} h-${size}`} />;
    if (score >= 50) return <Meh className={`w-${size} h-${size}`} />;
    return <Frown className={`w-${size} h-${size}`} />;
  };

  const getWorkflowName = (workflowId?: string) => {
    if (!workflowId) return null;
    return workflows.find(w => w.id === workflowId)?.name || 'Unknown Workflow';
  };

  const formatTimestamp = (timestamp: string | undefined) => {
    if (!timestamp) return null;
    const date = new Date(timestamp);
    return date.toLocaleString(); // Formats to a readable string like "10/1/2023, 12:00:00 PM"
  };

  return (
    <div className="h-full flex flex-col gap-4 md:gap-6 relative">
      {/* Header Actions */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3">
        <h2 className="text-xl md:text-2xl font-bold text-text">Tickets</h2>
        <div className="flex w-full sm:w-auto gap-2 md:gap-3">
           <div className="relative flex-1 sm:w-48 group">
              <input 
                type="text" 
                placeholder="Search..." 
                className="w-full bg-surface border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary transition-colors" 
              />
           </div>
           <button 
              onClick={() => setIsCreateModalOpen(true)}
              className="bg-primary hover:bg-primaryHover text-white px-4 py-2 rounded-md flex items-center gap-2 text-sm transition-all shadow-lg shadow-primary/20 shrink-0 active:scale-95"
            >
              <Plus className="w-4 h-4" /> <span className="hidden sm:inline">New Ticket</span><span className="sm:hidden">New</span>
           </button>
        </div>
      </div>

      {/* Main Content Area */}
      <div className="flex-1 bg-surface border border-border rounded-lg overflow-hidden flex flex-col min-h-[400px]">
        <div className="flex-1 overflow-auto relative custom-scrollbar">
          {isLoading ? (
            <div className="absolute inset-0 flex items-center justify-center bg-background/50 z-30">
              <Loader2 className="w-8 h-8 animate-spin text-primary" />
            </div>
          ) : tickets.length === 0 ? (
            <div className="p-8 text-center text-textMuted flex flex-col items-center gap-2">
              <Activity className="w-10 h-10 opacity-20" />
              <p>No tickets found in this workspace.</p>
            </div>
          ) : (
            <>
              {/* Desktop Table View */}
              <div className="hidden md:block min-w-full align-middle">
                <table className="w-full text-left border-collapse min-w-[900px]">
                  <thead className="bg-surfaceHighlight text-[10px] uppercase text-textMuted sticky top-0 z-20">
                    <tr>
                      <th className="p-4 border-b border-border">ID</th>
                      <th className="p-4 border-b border-border">Title</th>
                      <th className="p-4 border-b border-border">Status</th>
                      <th className="p-4 border-b border-border">Priority</th>
                      <th className="p-4 border-b border-border">Agent</th>
                      <th className="p-4 border-b border-border">Workflow</th>
                      <th className="p-4 border-b border-border">CSAT</th>
                      <th className="p-4 border-b border-border text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {tickets.map(ticket => {
                      const unreadCount = (ticket.comments?.length || 0) - (lastReadCounts[ticket.id] || 0);
                      const hasUnread = unreadCount > 0;
                      const workflowName = getWorkflowName(ticket.assignedWorkflowId);
                      
                      return (
                        <tr 
                          key={ticket.id} 
                          className="hover:bg-surfaceHighlight/50 transition-colors group cursor-pointer"
                          onDoubleClick={() => handleOpenTicket(ticket)}
                          title={ticket.title}
                        >
                          <td className="p-4 font-mono text-[11px] text-textMuted">
                            <div className="flex items-center gap-2">
                               {ticket.internal ? (
                                 <span title="Internal"><Database className="w-3 h-3 text-primary" /></span>
                               ) : (
                                 <span title="External"><Globe className="w-3 h-3 text-textMuted" /></span>
                               )}
                               {ticket.id}
                            </div>
                          </td>
                          <td className="p-4 text-text font-medium max-w-xs">
                            <div className="flex items-center justify-between gap-4">
                              <span className="truncate">{ticket.title}</span>
                              {hasUnread && (
                                <div className="flex items-center gap-1.5 px-2 py-1 bg-primary/20 text-primary rounded-md border border-primary/30 shrink-0 animate-pulse">
                                  <MessageSquare className="w-3 h-3" />
                                  <span className="text-[10px] font-mono font-bold">{unreadCount}</span>
                                </div>
                              )}
                            </div>
                          </td>
                          <td className="p-4">
                            {ticket.status && (
                              <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium whitespace-nowrap ${ticket.status.color}`}>
                                {ticket.status.name}
                              </span>
                            )}
                          </td>
                          <td className="p-4">
                            {ticket.priority && (
                              <span className={`text-[10px] uppercase font-bold px-2 py-0.5 rounded whitespace-nowrap ${ticket.priority.color}`}>
                                {ticket.priority.name}
                              </span>
                            )}
                          </td>
                          <td className="p-4">
                            {ticket.assignedAgentId ? (
                              <div className="flex items-center gap-2">
                                 <Bot className="w-3 h-3 text-primary" />
                                 <span className="text-xs text-text truncate max-w-[100px]">{agents.find(a => a.id === ticket.assignedAgentId)?.name || 'Agent'}</span>
                              </div>
                            ) : (
                              <span className="text-[10px] text-textMuted italic">Unassigned</span>
                            )}
                          </td>
                          <td className="p-4">
                            {workflowName ? (
                                <div className="flex items-center gap-2 text-primary">
                                    <Layers className="w-3 h-3" />
                                    <span className="text-[10px] font-bold truncate max-w-[120px]">{workflowName}</span>
                                </div>
                            ) : (
                                <span className="text-[10px] text-textMuted italic">-</span>
                            )}
                          </td>
                          <td className="p-4">
                            <div className="flex items-center gap-2">
                              <div className={`flex items-center justify-center w-6 h-6 rounded-full bg-background border border-border/50 ${getSatisfactionTextColor(ticket.satisfaction)} shadow-sm`}>
                                 {getSatisfactionIcon(ticket.satisfaction, 3.5)}
                              </div>
                              <span className={`text-xs font-mono font-bold ${getSatisfactionTextColor(ticket.satisfaction)}`}>
                                {ticket.satisfaction}%
                              </span>
                            </div>
                          </td>
                          <td className="p-4 text-right">
                            <button 
                              onClick={() => handleOpenTicket(ticket)}
                              className="text-primary hover:text-white text-xs font-bold uppercase tracking-widest px-3 py-1 rounded bg-primary/10 hover:bg-primary transition-all opacity-0 group-hover:opacity-100"
                            >
                              Details
                            </button>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {/* Mobile Card View */}
              <div className="md:hidden flex flex-col divide-y divide-border">
                {tickets.map(ticket => {
                  const unreadCount = (ticket.comments?.length || 0) - (lastReadCounts[ticket.id] || 0);
                  const hasUnread = unreadCount > 0;
                  const agent = agents.find(a => a.id === ticket.assignedAgentId);
                  const workflowName = getWorkflowName(ticket.assignedWorkflowId);

                  return (
                    <div 
                      key={ticket.id}
                      onClick={() => handleOpenTicket(ticket)}
                      className="p-4 space-y-3 active:bg-surfaceHighlight transition-colors"
                      title={ticket.title}
                    >
                      <div className="flex justify-between items-start">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-mono text-[10px] text-textMuted bg-surfaceHighlight px-1.5 py-0.5 rounded border border-border">
                             {ticket.id}
                          </span>
                          {ticket.priority && (
                            <span className={`text-[10px] uppercase font-bold px-2 py-0.5 rounded ${ticket.priority.color}`}>
                              {ticket.priority.name}
                            </span>
                          )}
                          {ticket.status && (
                            <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${ticket.status.color}`}>
                              {ticket.status.name}
                            </span>
                          )}
                        </div>
                        <div className="flex items-center gap-2">
                          {hasUnread && (
                            <div className="flex items-center gap-1.5 px-2 py-0.5 bg-primary/20 text-primary rounded-md border border-primary/30 animate-pulse">
                              <MessageSquare className="w-3 h-3" />
                              <span className="text-[10px] font-bold">{unreadCount}</span>
                            </div>
                          )}
                          <div className={`flex items-center gap-1.5 px-2 py-0.5 bg-surfaceHighlight rounded-full text-[10px] font-bold ${getSatisfactionTextColor(ticket.satisfaction)} border border-border/50`}>
                             {getSatisfactionIcon(ticket.satisfaction, 3)}
                             {ticket.satisfaction}%
                          </div>
                        </div>
                      </div>
                      
                      <div className="flex flex-col gap-1">
                        <div className="flex items-start gap-3">
                           {ticket.internal ? (
                             <Database className="w-4 h-4 text-primary shrink-0 mt-0.5" />
                           ) : (
                             <div className="flex items-center gap-1.5">
                               <Globe className="w-4 h-4 text-textMuted shrink-0 mt-0.5" />
                               {(ticket.assignedAgentId || ticket.assignedWorkflowId) && (
                                 <span className="text-[9px] font-bold uppercase tracking-wider text-primary/70 bg-primary/10 px-1.5 py-0.5 rounded border border-primary/20" title="Tracked internally with agent/workflow">
                                   Tracked
                                 </span>
                               )}
                             </div>
                           )}
                           <h3 className="text-sm font-semibold text-text leading-snug line-clamp-2">{ticket.title}</h3>
                        </div>
                        {workflowName && (
                            <div className="flex items-center gap-1.5 ml-7 text-[10px] text-primary font-bold">
                                <Layers className="w-3 h-3" />
                                <span>{workflowName}</span>
                            </div>
                        )}
                      </div>

                      <div className="flex items-center justify-between pt-1">
                        <div className="flex items-center gap-2">
                           {agent ? (
                             <div className="flex items-center gap-1.5 text-xs text-text">
                                <Bot className="w-3 h-3 text-primary" />
                                <span>{agent.name}</span>
                             </div>
                           ) : (
                             <span className="text-[10px] text-textMuted italic">Manual triage</span>
                           )}
                        </div>
                        <div className="flex items-center gap-2 text-[10px] text-textMuted">
                           <Clock className="w-3 h-3" />
                           <span>Updated 5m ago</span>
                           <ChevronRight className="w-3 h-3" />
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </div>

        {!isLastPage && tickets.length > 0 && !isLoading && (
          <div className="p-4 border-t border-border flex justify-center bg-surfaceHighlight/5">
             <button 
              onClick={handleLoadMore}
              disabled={isLoadingMore}
              className="flex items-center gap-2 px-8 py-2.5 border border-border rounded-full text-xs font-bold uppercase tracking-widest text-textMuted hover:text-text hover:border-primary transition-all group active:scale-95 shadow-sm"
             >
               {isLoadingMore ? (
                  <Loader2 className="w-3 h-3 animate-spin text-primary" />
               ) : (
                  <ChevronDown className="w-3 h-3 group-hover:translate-y-0.5 transition-transform" />
               )}
               {isLoadingMore ? 'Loading...' : 'Load More'}
             </button>
          </div>
        )}
      </div>

      {/* Ticket Details Modal */}
      {selectedTicket && (
        <div 
          className="fixed inset-0 z-50 flex items-center justify-center p-2 sm:p-4 bg-black/80 backdrop-blur-sm animate-fade-in"
          onClick={() => setSelectedTicket(null)}
        >
          <div 
            className="bg-surface border border-border w-full max-w-4xl rounded-xl shadow-2xl overflow-hidden flex flex-col h-[95vh] sm:h-[90vh] animate-scale-in"
            onClick={e => e.stopPropagation()}
          >
            {/* Modal Header */}
            <div className="px-4 md:px-6 py-4 border-b border-border flex justify-between items-start bg-surfaceHighlight/30 shrink-0">
              <div className="flex-1 mr-4 min-w-0">
                <div className="flex flex-wrap items-center gap-2 mb-2">
                  <span className="font-mono text-[10px] text-textMuted bg-surfaceHighlight px-2 py-0.5 rounded border border-border">
                    {selectedTicket.id}
                  </span>
                  {selectedTicket.priority && (
                    <span className={`text-[10px] uppercase font-bold px-2 py-0.5 rounded ${selectedTicket.priority.color}`}>
                      {selectedTicket.priority.name}
                    </span>
                  )}
                  {selectedTicket.status && (
                    <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${selectedTicket.status.color}`}>
                      {selectedTicket.status.name}
                    </span>
                  )}
                </div>
                <div className="flex items-center gap-2">
                    {selectedTicket.source === 'JIRA' && <Layers className="w-5 h-5 text-blue-500 shrink-0" />}
                    {!selectedTicket.internal && (selectedTicket.assignedAgentId || selectedTicket.assignedWorkflowId) && (
                      <span className="text-[9px] font-bold uppercase tracking-wider text-primary/70 bg-primary/10 px-2 py-1 rounded border border-primary/20 shrink-0" title="Tracked internally - uses internal status/priority for agent execution">
                        Tracked Internally
                      </span>
                    )}
                    <h3 className="text-base md:text-xl font-bold text-text leading-tight truncate sm:whitespace-normal">{selectedTicket.title}</h3>
                </div>
              </div>
              <button 
                onClick={() => setSelectedTicket(null)} 
                className="p-1.5 hover:bg-surfaceHighlight rounded-full text-textMuted hover:text-white transition-colors shrink-0"
              >
                <X className="w-6 h-6" />
              </button>
            </div>

            {/* Modal Content */}
            <div className="p-4 md:p-6 overflow-y-auto custom-scrollbar flex-1 space-y-6">
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                    <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Initial Context / Description</h4>
                    <div className="flex items-center gap-2">
                      {selectedTicket.internal && !isEditingDescription && (
                        <button
                          onClick={handleStartEditDescription}
                          className="text-[10px] font-bold text-primary hover:text-primary/80 px-2 py-1 rounded border border-primary/20 hover:border-primary/40 transition-colors flex items-center gap-1"
                        >
                          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="w-3 h-3">
                            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
                          </svg>
                          Edit
                        </button>
                      )}
                      <span className="text-[10px] text-textMuted bg-surfaceHighlight px-2 py-0.5 rounded border border-border uppercase font-mono">{selectedTicket.source} Original</span>
                    </div>
                </div>
                {isEditingDescription ? (
                  <div className="space-y-2">
                    <textarea
                      value={descriptionValue}
                      onChange={(e) => setDescriptionValue(e.target.value)}
                      className="w-full bg-background border border-border rounded-lg p-3 text-sm text-text focus:outline-none focus:border-primary min-h-[200px] font-mono"
                      placeholder="Enter ticket description (supports Markdown)..."
                    />
                    <div className="flex items-center justify-between">
                      <span className={`text-xs font-mono ${
                        descriptionValue.length > 5000 ? 'text-red-500' : 
                        descriptionValue.length > 4500 ? 'text-amber-500' : 
                        'text-textMuted'
                      }`}>
                        {descriptionValue.length} / 5,000 characters
                      </span>
                      <div className="flex items-center gap-2">
                        <button
                          onClick={handleCancelEditDescription}
                          disabled={isSavingDescription}
                          className="px-3 py-1.5 text-sm font-medium text-textMuted hover:text-text border border-border rounded-md hover:bg-surfaceHighlight transition-colors"
                        >
                          Cancel
                        </button>
                        <button
                          onClick={handleSaveDescription}
                          disabled={isSavingDescription || !descriptionValue.trim() || descriptionValue.length > 5000}
                          className="px-3 py-1.5 text-sm font-medium bg-primary text-white rounded-md hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center gap-2"
                        >
                          {isSavingDescription ? (
                            <>
                              <Loader2 className="w-4 h-4 animate-spin" />
                              Saving...
                            </>
                          ) : (
                            <>
                              <Save className="w-4 h-4" />
                              Save
                            </>
                          )}
                        </button>
                      </div>
                    </div>
                  </div>
                ) : (
                  <div className="bg-surfaceHighlight/20 p-4 rounded-lg border border-border text-sm text-text shadow-inner">
                    <Markdown content={selectedTicket.description} />
                  </div>
                )}
              </div>

              <div className="space-y-3">
                <h4 className="text-sm font-semibold text-text flex items-center gap-2">
                  <Zap className="w-4 h-4 text-amber-400" /> AI-Powered Summary
                </h4>
                
                {!summary ? (
                  <button 
                    onClick={() => handleGenerateSummary(selectedTicket)}
                    disabled={loadingSummary}
                    className="w-full bg-primary/5 hover:bg-primary/10 text-primary border border-primary/20 border-dashed py-5 rounded-xl text-sm font-bold flex items-center justify-center gap-3 transition-all group active:scale-[0.98]"
                  >
                    {loadingSummary ? <RefreshCw className="w-5 h-5 animate-spin text-primary" /> : <Sparkles className="w-5 h-5 group-hover:rotate-12 transition-transform" />}
                    Analyze Context & Generate Executive Summary
                  </button>
                ) : (
                  <div className="relative group overflow-hidden">
                    <div className="absolute inset-0 bg-gradient-to-r from-primary/10 via-purple-500/10 to-primary/10 opacity-30 animate-gradient-x blur-xl" />
                    <div className="relative bg-surface border-2 border-primary/20 p-5 rounded-xl text-sm animate-fade-in shadow-lg group-hover:border-primary/40 transition-colors">
                        <div className="flex items-center justify-between mb-3">
                            <p className="font-bold text-[10px] uppercase tracking-wider text-primary flex items-center gap-2">
                                <Bot className="w-3.5 h-3.5" /> Intelligence Output
                            </p>
                            <button 
                                onClick={() => handleGenerateSummary(selectedTicket)} 
                                disabled={loadingSummary}
                                className="text-[10px] font-bold text-textMuted hover:text-primary transition-colors flex items-center gap-1"
                            >
                                <RefreshCw className={`w-3 h-3 ${loadingSummary ? 'animate-spin' : ''}`} /> Recalculate
                            </button>
                        </div>
                        <Markdown content={summary} className="text-text leading-relaxed font-medium" />
                    </div>
                  </div>
                )}
              </div>

              <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
                 <div className="lg:col-span-1 space-y-4">
                    <div className="space-y-5 p-5 border border-border rounded-xl bg-surfaceHighlight/5 shadow-sm">
                        <div className="flex items-center justify-between border-b border-border/50 pb-2">
                            <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest">Configuration</h4>
                        </div>
                        
                        <div className="space-y-1.5 pt-1">
                            <label className="text-[10px] font-bold text-textMuted uppercase flex items-center gap-2">
                                Satisfaction Score (CSAT)
                            </label>
                            <div className="bg-background border border-border rounded-lg p-4 shadow-sm relative overflow-hidden group">
                                <div className="flex items-center justify-between mb-3">
                                    <div className={`flex items-center gap-2.5 font-bold ${getSatisfactionTextColor(selectedTicket.satisfaction)}`}>
                                        <div className="p-1.5 bg-surfaceHighlight rounded-full shadow-inner border border-border/30">
                                            {getSatisfactionIcon(selectedTicket.satisfaction, 5)}
                                        </div>
                                        <span className="text-2xl font-mono tracking-tight">{selectedTicket.satisfaction}%</span>
                                    </div>
                                    <span className="text-[9px] text-textMuted font-bold uppercase tracking-widest bg-surfaceHighlight px-2 py-1 rounded-md border border-border/50">
                                        {selectedTicket.satisfaction >= 80 ? 'Excellent' : selectedTicket.satisfaction >= 50 ? 'Average' : 'Critical'}
                                    </span>
                                </div>
                                <div className="w-full bg-surfaceHighlight rounded-full h-1.5 overflow-hidden">
                                    <div 
                                        className={`h-full rounded-full transition-all duration-1000 ${getSatisfactionColor(selectedTicket.satisfaction)}`} 
                                        style={{ width: `${selectedTicket.satisfaction}%` }} 
                                    />
                                </div>
                            </div>
                        </div>

                        {selectedTicket.internal && (
                          <>
                            <div className="space-y-1.5">
                                <label className="text-[10px] font-bold text-textMuted uppercase">Status</label>
                                <select 
                                  value={editForm.status || ''}
                                  onChange={(e) => setEditForm({...editForm, status: e.target.value})}
                                  className="w-full bg-background border border-border rounded-md p-2.5 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                                >
                                {statuses.map(s => (
                                    <option key={s.id} value={s.name}>{s.name}</option>
                                ))}
                                </select>
                            </div>
                            <div className="space-y-1.5">
                                <label className="text-[10px] font-bold text-textMuted uppercase">Priority</label>
                                <select 
                                  value={editForm.priority || ''}
                                  onChange={(e) => setEditForm({...editForm, priority: e.target.value})}
                                  className="w-full bg-background border border-border rounded-md p-2.5 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                                >
                                {priorities.map(p => (
                                    <option key={p.id} value={p.name}>{p.name}</option>
                                ))}
                                </select>
                            </div>
                          </>
                        )}
                        
                        <div className="space-y-1.5">
                            <label className="text-[10px] font-bold text-textMuted uppercase">Assignee</label>
                            <select 
                              value={editForm.assignedAgentId || ''}
                              onChange={(e) => setEditForm({...editForm, assignedAgentId: e.target.value})}
                              className="w-full bg-background border border-border rounded-md p-2.5 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                            >
                              <option value="">Unassigned</option>
                              {agents.map(agent => (
                                <option key={agent.id} value={agent.id}>{agent.name}</option>
                              ))}
                            </select>
                        </div>

                        <div className="space-y-1.5 pt-2 border-t border-border/50">
                            <label className="text-[10px] font-bold text-textMuted uppercase flex items-center gap-2">
                                <Layers className="w-3 h-3" /> Automation Workflow
                            </label>
                            <select 
                              value={editForm.assignedWorkflowId || ''}
                              onChange={(e) => setEditForm({...editForm, assignedWorkflowId: e.target.value})}
                              className="w-full bg-background border border-border rounded-md p-2.5 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                            >
                              <option value="">None / Manual Execution</option>
                              {workflows.map(wf => (
                                <option key={wf.id} value={wf.id}>{wf.name}</option>
                              ))}
                            </select>
                            <p className="text-[9px] text-textMuted italic">Assigning a workflow allows the agent to execute autonomous sequences.</p>
                        </div>
                    </div>
                 </div>

                 <div className="lg:col-span-2 flex flex-col gap-5">
                    <div className="flex items-center justify-between border-b border-border pb-3">
                       <h4 className="text-sm font-semibold text-text flex items-center gap-2">
                          <Activity className="w-4 h-4 text-emerald-400" /> Activity Log
                       </h4>
                    </div>
                    
                    <div className="space-y-5 max-h-[500px] overflow-y-auto custom-scrollbar pr-2 pb-4">
                      {(selectedTicket.comments?.length || 0) === 0 ? (
                        <div className="text-center py-10 opacity-30">
                           <MessageSquare className="w-12 h-12 mx-auto mb-2" />
                           <p className="text-sm italic">No conversation started yet.</p>
                        </div>
                      ) : (
                        (selectedTicket.comments || []).map(comment => (
                          <div key={comment.id} className="flex gap-4 text-sm animate-fade-in group">
                            <div className="mt-0.5 w-8 h-8 rounded-lg bg-surfaceHighlight border border-border flex items-center justify-center shrink-0 text-xs font-bold text-textMuted group-hover:border-primary/50 transition-all shadow-sm">
                                {comment.author.charAt(0)}
                            </div>
                            <div className="flex-1 min-w-0">
                                <div className="flex items-center gap-2 mb-1.5">
                                  <span className="font-bold text-text text-xs truncate">{comment.author}</span>
                                  {comment.timestamp && (
                                    <span className="text-[10px] text-textMuted font-mono">{formatTimestamp(comment.timestamp)}</span>
                                  )}
                                </div>
                                <div className="text-sm text-text leading-relaxed bg-surfaceHighlight/10 p-4 rounded-xl border border-transparent group-hover:border-border transition-all shadow-sm">
                                  <Markdown content={comment.content} />
                                </div>
                            </div>
                          </div>
                        ))
                      )}
                    </div>

                    <div className="mt-auto relative group">
                        <textarea 
                          value={newComment}
                          onChange={(e) => setNewComment(e.target.value)}
                          placeholder="Type your response... (Markdown supported)"
                          className="w-full bg-background border border-border rounded-xl p-4 pr-14 text-sm text-text focus:outline-none focus:border-primary resize-none min-h-[110px] shadow-lg transition-all"
                        />
                        <button 
                          onClick={handlePostComment}
                          disabled={!newComment.trim() || isPostingComment}
                          className="absolute bottom-4 right-4 bg-primary hover:bg-primaryHover text-white p-2.5 rounded-lg transition-all disabled:opacity-30 active:scale-90 shadow-md shadow-primary/20"
                        >
                          {isPostingComment ? <Loader2 className="w-5 h-5 animate-spin" /> : <Send className="w-5 h-5" />}
                        </button>
                    </div>
                 </div>
              </div>
            </div>
            
            {/* Modal Footer */}
            <div className="p-4 border-t border-border bg-surfaceHighlight/20 shrink-0">
                {selectedTicket.internal && showConversionForm ? (
                  <div className="space-y-4 mb-4">
                    <div className="flex items-center justify-between">
                      <h4 className="text-sm font-bold text-text">Convert to External Tracker</h4>
                      <button
                        onClick={() => {
                          setShowConversionForm(false);
                          setConversionConfig(null);
                        }}
                        className="text-textMuted hover:text-text transition-colors"
                      >
                        <X className="w-4 h-4" />
                      </button>
                    </div>
                    <div className="space-y-3">
                      <div>
                        <label className="block text-xs font-semibold text-textMuted mb-2">Integration</label>
                        <IntegrationSelector
                          workspaceId={workspaceId}
                          value={conversionConfig?.integrationId || null}
                          onChange={(integrationId, integrationName) =>
                            setConversionConfig(prev => ({
                              ...prev,
                              integrationId,
                              integrationName,
                              issueTypeName: prev?.issueTypeName || ''
                            }))
                          }
                          disabled={isConverting}
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-semibold text-textMuted mb-2">Issue Type</label>
                        <IssueTypeSelector
                          value={conversionConfig?.issueTypeName || null}
                          onChange={(issueTypeName) =>
                            setConversionConfig(prev => ({
                              ...prev!,
                              issueTypeName
                            }))
                          }
                          disabled={isConverting}
                        />
                      </div>
                      <button
                        onClick={handleConvertToExternal}
                        disabled={!conversionConfig?.integrationId || !conversionConfig?.issueTypeName || isConverting}
                        className="w-full px-5 py-2.5 bg-primary hover:bg-primaryHover text-white text-xs font-bold uppercase tracking-widest rounded-lg transition-all shadow-lg shadow-primary/20 flex items-center justify-center gap-2 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed"
                      >
                        {isConverting ? (
                          <>
                            <Loader2 className="w-4 h-4 animate-spin" />
                            Converting...
                          </>
                        ) : (
                          <>
                            <Globe className="w-4 h-4" />
                            {conversionConfig?.issueTypeName && conversionConfig?.integrationName
                              ? `Create ${conversionConfig.issueTypeName} in ${conversionConfig.integrationName}`
                              : 'Create External Issue'}
                          </>
                        )}
                      </button>
                    </div>
                  </div>
                ) : null}
                
                <div className="flex justify-between items-center">
                    <button onClick={() => setSelectedTicket(null)} className="px-5 py-2.5 text-xs font-bold uppercase tracking-widest text-textMuted hover:text-text transition-colors">Dismiss</button>
                    <div className="flex items-center gap-3">
                        {selectedTicket.internal && (
                            <>
                              <button 
                                onClick={() => setDeleteConfirmationId(selectedTicket.id)}
                                className="p-2.5 text-textMuted hover:text-red-500 transition-colors rounded-lg border border-transparent hover:border-red-500/20"
                                title="Delete Ticket"
                                disabled={isConverting}
                              >
                                <Trash2 className="w-5 h-5" />
                              </button>
                              {!showConversionForm && (
                                <button 
                                  onClick={() => setShowConversionForm(true)}
                                  disabled={isConverting}
                                  className="px-5 py-2.5 bg-background border border-border hover:border-primary/50 text-text text-xs font-bold uppercase tracking-widest rounded-lg transition-all flex items-center gap-2 active:scale-95 shadow-sm disabled:opacity-50 disabled:cursor-not-allowed"
                                >
                                  <Globe className="w-4 h-4" />
                                  Convert to External
                                </button>
                              )}
                            </>
                        )}
                        {!showConversionForm && (
                          <button 
                            onClick={handleUpdateTicket}
                            disabled={isUpdating || isConverting}
                            className="px-8 py-2.5 bg-primary hover:bg-primaryHover text-white text-xs font-bold uppercase tracking-widest rounded-lg transition-all shadow-lg shadow-primary/20 flex items-center gap-2 active:scale-95 disabled:opacity-50 disabled:cursor-not-allowed"
                          >
                            {isUpdating ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Apply Updates'}
                          </button>
                        )}
                    </div>
                </div>
            </div>
          </div>
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {deleteConfirmationId && (
        <div className="fixed inset-0 z-[70] flex items-center justify-center p-4 bg-black/90 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-sm rounded-xl shadow-2xl p-6 space-y-4 animate-scale-in">
             <div className="flex items-center gap-3 text-red-500">
                <AlertTriangle className="w-6 h-6" />
                <h3 className="text-lg font-bold text-text">Remove Ticket?</h3>
             </div>
             <p className="text-sm text-textMuted leading-relaxed">
                This will permanently delete ticket <span className="font-mono font-bold text-text">{deleteConfirmationId}</span> and all associated conversation history. This action cannot be undone.
             </p>
             <div className="flex gap-3 pt-2">
                <button 
                  onClick={() => setDeleteConfirmationId(null)} 
                  className="flex-1 px-4 py-2 border border-border rounded-lg text-xs font-bold uppercase tracking-widest text-text hover:bg-surfaceHighlight transition-colors"
                  disabled={isDeleting}
                >
                  Cancel
                </button>
                <button 
                  onClick={handleDeleteTicket} 
                  disabled={isDeleting} 
                  className="flex-1 px-4 py-2 bg-red-600 text-white rounded-lg text-xs font-bold uppercase tracking-widest shadow-lg shadow-red-500/20 active:scale-95 transition-all"
                >
                    {isDeleting ? <Loader2 className="w-4 h-4 animate-spin mx-auto" /> : 'Confirm Delete'}
                </button>
             </div>
          </div>
        </div>
      )}

      {/* Create Ticket Modal - Matched to provided mockup */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-2 sm:p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-[480px] rounded-xl shadow-2xl overflow-hidden animate-scale-in flex flex-col max-h-[95vh]">
                <div className="px-6 py-5 flex justify-between items-center bg-surface shrink-0">
                    <h3 className="text-xl font-bold text-text">Create New Ticket</h3>
                    <button onClick={() => setIsCreateModalOpen(false)} className="text-textMuted hover:text-text transition-colors">
                        <X className="w-6 h-6" />
                    </button>
                </div>
                
                <form onSubmit={handleCreateTicket} className="px-6 pb-6 space-y-4 overflow-y-auto custom-scrollbar flex-1">
                   <div className="space-y-1.5">
                     <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Title</label>
                     <input 
                       type="text" 
                       value={newTicketData.title}
                       onChange={(e) => setNewTicketData({...newTicketData, title: e.target.value})}
                       placeholder="e.g., Fix login page styling"
                       className="w-full bg-background border border-border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary transition-all shadow-sm placeholder:text-textMuted/40"
                       autoFocus
                       required
                     />
                   </div>

                   <div className="space-y-1.5">
                     <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider">Description</label>
                     <textarea 
                       value={newTicketData.description}
                       onChange={(e) => setNewTicketData({...newTicketData, description: e.target.value})}
                       placeholder="Describe the issue in detail..."
                       className="w-full bg-background border border-border rounded-lg px-4 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary min-h-[120px] resize-none shadow-sm placeholder:text-textMuted/40"
                       required
                     />
                   </div>

                   <div className="grid grid-cols-2 gap-4">
                     <div className="space-y-1.5">
                        <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider flex items-center gap-1.5">
                            <Activity className="w-3.5 h-3.5" /> Status
                        </label>
                        <div className="relative">
                            <select 
                                value={newTicketData.statusId}
                                onChange={(e) => setNewTicketData({...newTicketData, statusId: e.target.value})}
                                className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
                            >
                                {statuses.map(s => (
                                    <option key={s.id} value={s.id}>{s.name}</option>
                                ))}
                            </select>
                            <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
                        </div>
                     </div>
                     <div className="space-y-1.5">
                        <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider flex items-center gap-1.5">
                            <Flag className="w-3.5 h-3.5" /> Priority
                        </label>
                        <div className="relative">
                            <select 
                                value={newTicketData.priorityId}
                                onChange={(e) => setNewTicketData({...newTicketData, priorityId: e.target.value})}
                                className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
                            >
                                {priorities.map(p => (
                                    <option key={p.id} value={p.id}>{p.name}</option>
                                ))}
                            </select>
                            <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
                        </div>
                     </div>
                   </div>

                   <div className="grid grid-cols-2 gap-4">
                     <div className="space-y-1.5">
                       <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider flex items-center gap-1.5">
                            <Bot className="w-3.5 h-3.5" /> Assign Agent
                       </label>
                       <div className="relative">
                            <select 
                                value={newTicketData.assignedAgentId}
                                onChange={(e) => setNewTicketData({...newTicketData, assignedAgentId: e.target.value})}
                                className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
                            >
                                <option value="">Unassigned</option>
                                {agents.map(agent => (
                                    <option key={agent.id} value={agent.id}>{agent.name}</option>
                                ))}
                            </select>
                            <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
                       </div>
                     </div>
                     <div className="space-y-1.5">
                       <label className="text-[10px] font-bold text-textMuted uppercase tracking-wider flex items-center gap-1.5">
                            <WorkflowIcon className="w-3.5 h-3.5" /> Assign Workflow
                       </label>
                       <div className="relative">
                            <select 
                                value={newTicketData.assignedWorkflowId}
                                onChange={(e) => setNewTicketData({...newTicketData, assignedWorkflowId: e.target.value})}
                                className="w-full bg-background border border-border rounded-lg pl-3 pr-10 py-2.5 text-sm text-text focus:outline-none focus:ring-1 focus:ring-primary focus:border-primary appearance-none transition-all shadow-sm"
                            >
                                <option value="">No Workflow</option>
                                {workflows.map(wf => (
                                    <option key={wf.id} value={wf.id}>{wf.name}</option>
                                ))}
                            </select>
                            <ChevronDown className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 text-textMuted pointer-events-none" />
                       </div>
                     </div>
                   </div>

                   <div className="pt-4 flex gap-4">
                      <button 
                        type="button" 
                        onClick={() => setIsCreateModalOpen(false)}
                        className="flex-1 px-4 py-2.5 border border-border rounded-lg text-sm font-bold text-text hover:bg-surfaceHighlight transition-all active:scale-[0.98]"
                        disabled={isCreating}
                      >
                        Cancel
                      </button>
                      <button 
                        type="submit" 
                        disabled={!newTicketData.title || !newTicketData.description || isCreating}
                        className="flex-1 px-4 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-lg text-sm font-bold transition-all shadow-lg shadow-primary/20 flex items-center justify-center gap-2 active:scale-[0.98]"
                      >
                        {isCreating ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                        {isCreating ? 'Creating...' : 'Create Ticket'}
                      </button>
                   </div>
                </form>
            </div>
          </div>
        )}
      </div>
  );
};

export default TicketList;
