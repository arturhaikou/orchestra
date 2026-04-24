import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import TicketEditPage from '../pages/TicketEditPage';
import * as ticketService from '../../services/ticketService';

vi.mock('../../services/ticketService', () => ({
  getTicketById: vi.fn(),
  getTicketStatuses: vi.fn(),
  getTicketPriorities: vi.fn(),
  updateTicket: vi.fn(),
  getTickets: vi.fn(),
  createTicket: vi.fn(),
  updateTicketStatus: vi.fn(),
  updateTicketPriority: vi.fn(),
  deleteTicket: vi.fn(),
  addComment: vi.fn(),
  convertToExternal: vi.fn(),
  generateSummary: vi.fn(),
}));

const mockStatuses = [
  { id: 'status-open', name: 'Open', color: '#22c55e' },
];

const mockPriorities = [
  { id: 'priority-medium', name: 'Medium', color: '#eab308', value: 2 },
];

const mockTicket = {
  id: 'ticket-abc',
  workspaceId: 'ws-test',
  title: 'Test Ticket',
  description: 'Test description',
  source: 'INTERNAL',
  internal: true,
  status: mockStatuses[0],
  priority: mockPriorities[0],
  satisfaction: 100,
  comments: [],
};

describe('Ticket Edit Routing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
    vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
    vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);
  });

  it('renders_ticket_edit_page_at_correct_route', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/tickets/ticket-abc/edit']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/tickets/:ticketId/edit" element={<TicketEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/edit ticket/i)).toBeInTheDocument();
    });
  });

  it('passes_url_params_to_ticket_edit_page', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/tickets/ticket-abc/edit']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/tickets/:ticketId/edit" element={<TicketEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(ticketService.getTicketById).toHaveBeenCalledWith('ticket-abc');
    });
  });

  it('renders_back_to_tickets_link_with_correct_workspace', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/tickets/ticket-abc/edit']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/tickets/:ticketId/edit" element={<TicketEditPage />} />
          <Route path="/workspaces/:workspaceId/tickets" element={<div>Tickets List</div>} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      const backLink = screen.getByText(/back to tickets/i);
      expect(backLink.closest('a')).toHaveAttribute('href', '/workspaces/ws-test/tickets');
    });
  });
});
