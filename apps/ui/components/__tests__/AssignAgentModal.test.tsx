import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import '@testing-library/jest-dom';
import TicketList from '../TicketList';

const mockAgent = {
  id: 'agent-1',
  name: 'Support Agent',
  role: 'Support',
  capabilities: [],
  toolActionIds: [],
  customInstructions: '',
  projectPrinciples: '',
  model: null,
  isBuiltIn: false,
};

const mockTicket = {
  id: 'ticket-1',
  title: 'Test Ticket',
  description: 'A test ticket',
  status: 'Open',
  priority: 'Medium',
  internal: true,
  assignedAgentId: null,
  assignedWorkflowId: null,
  createdAt: '2026-01-01',
  updatedAt: '2026-01-01',
};

vi.mock('../../services/ticketService', () => ({
  getTickets: vi.fn().mockResolvedValue({
    items: [{
      id: 'ticket-1',
      title: 'Test Ticket',
      description: 'A test ticket',
      status: 'Open',
      priority: 'Medium',
      internal: true,
      assignedAgentId: null,
      assignedWorkflowId: null,
      createdAt: '2026-01-01',
      updatedAt: '2026-01-01',
    }],
    nextPageToken: null,
    isLast: true,
  }),
  updateTicket: vi.fn(),
  deleteTicket: vi.fn(),
  createTicket: vi.fn(),
  addComment: vi.fn(),
  convertToExternal: vi.fn(),
  getTicketStatuses: vi.fn().mockResolvedValue([]),
  getTicketPriorities: vi.fn().mockResolvedValue([]),
  generateSummary: vi.fn(),
}));

vi.mock('../../services/agentService', () => ({
  getAgents: vi.fn().mockResolvedValue([{
    id: 'agent-1',
    name: 'Support Agent',
    role: 'Support',
    capabilities: [],
    toolActionIds: [],
    customInstructions: '',
    projectPrinciples: '',
    model: null,
    isBuiltIn: false,
  }]),
  deleteAgent: vi.fn(),
}));

vi.mock('../../services/workflowService', () => ({
  getWorkflowDefinitions: vi.fn().mockResolvedValue([]),
}));

vi.mock('../../services/authService', () => ({
  getUser: vi.fn().mockResolvedValue({ id: 'user-1', name: 'Test User' }),
}));

const { updateTicket } = await import('../../services/ticketService');

function renderTicketList() {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-1/tickets']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/tickets" element={<TicketList />} />
      </Routes>
    </MemoryRouter>
  );
}

function getTicketLink() {
  return screen.getAllByRole('link', { name: /Test Ticket/ })[0];
}

describe('Assign Agent Modal - Save and Close Behavior (Scenario 2 & 3)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should close modal and update ticket row on successful agent assignment', async () => {
    const user = userEvent.setup();
    const updatedTicket = { ...mockTicket, assignedAgentId: 'agent-1' };
    (updateTicket as ReturnType<typeof vi.fn>).mockResolvedValueOnce(updatedTicket);
    renderTicketList();

    await waitFor(() => {
      expect(getTicketLink()).toBeInTheDocument();
    });

    await user.click(getTicketLink());

    await waitFor(() => {
      expect(screen.getByText(/save/i)).toBeInTheDocument();
    });

    const saveButton = screen.getByRole('button', { name: /save/i });
    await user.click(saveButton);

    await waitFor(() => {
      expect(updateTicket).toHaveBeenCalled();
    });
  });

  it('should display success toast with agent name after successful assignment', async () => {
    const user = userEvent.setup();
    const updatedTicket = { ...mockTicket, assignedAgentId: 'agent-1' };
    (updateTicket as ReturnType<typeof vi.fn>).mockResolvedValueOnce(updatedTicket);
    renderTicketList();

    await waitFor(() => {
      expect(getTicketLink()).toBeInTheDocument();
    });

    await user.click(getTicketLink());

    await waitFor(() => {
      expect(screen.getByText(/save/i)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByText(/assigned to ticket/i)).toBeInTheDocument();
    });
  });

  it('should keep modal open and show error on API failure during assignment', async () => {
    const user = userEvent.setup();
    (updateTicket as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new Error('The selected agent does not belong to this workspace.')
    );
    renderTicketList();

    await waitFor(() => {
      expect(getTicketLink()).toBeInTheDocument();
    });

    await user.click(getTicketLink());

    await waitFor(() => {
      expect(screen.getByText(/save/i)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByText(/does not belong to this workspace/i)).toBeInTheDocument();
    });
  });

  it('should show Retry button label after API error', async () => {
    const user = userEvent.setup();
    (updateTicket as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('Server error'));
    renderTicketList();

    await waitFor(() => {
      expect(getTicketLink()).toBeInTheDocument();
    });

    await user.click(getTicketLink());

    await waitFor(() => {
      expect(screen.getByText(/save/i)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });
  });

  it('should apply highlight animation to updated ticket row after modal closes', async () => {
    const user = userEvent.setup();
    const updatedTicket = { ...mockTicket, assignedAgentId: 'agent-1' };
    (updateTicket as ReturnType<typeof vi.fn>).mockResolvedValueOnce(updatedTicket);
    renderTicketList();

    await waitFor(() => {
      expect(getTicketLink()).toBeInTheDocument();
    });

    await user.click(getTicketLink());

    await waitFor(() => {
      expect(screen.getByText(/save/i)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      const ticketRow = screen.getAllByText('Test Ticket')[0].closest('[data-ticket-id]') || screen.getAllByText('Test Ticket')[0].parentElement;
      expect(ticketRow?.className).toMatch(/bg-primary/);
    });
  });

  it('should clear error when modal is closed after an error', async () => {
    const user = userEvent.setup();
    (updateTicket as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('Error'));
    renderTicketList();

    await waitFor(() => {
      expect(getTicketLink()).toBeInTheDocument();
    });

    await user.click(getTicketLink());

    await waitFor(() => {
      expect(screen.getByText(/save/i)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() => {
      expect(screen.getByText(/Error/)).toBeInTheDocument();
    });

    const cancelButton = screen.getByRole('button', { name: /cancel|close/i });
    await user.click(cancelButton);

    await user.click(getTicketLink());

    await waitFor(() => {
      expect(screen.queryByText(/Error/)).not.toBeInTheDocument();
    });
  });
});
