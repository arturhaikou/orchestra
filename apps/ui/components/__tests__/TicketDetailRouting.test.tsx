import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import TicketDetailPage from '../pages/TicketDetailPage';
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

const mockTicket = {
  id: 'ticket-route-test',
  workspaceId: 'ws-route',
  title: 'Route Test Ticket',
  description: 'Testing route parameters',
  source: 'INTERNAL',
  internal: true,
  status: { id: 's1', name: 'Open', color: '#22c55e' },
  priority: { id: 'p1', name: 'Medium', color: '#eab308', value: 2 },
  satisfaction: 75,
  comments: [],
};

describe('Ticket Detail Routing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
  });

  it('renders_ticket_detail_page_at_correct_route', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-route/tickets/ticket-route-test']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/tickets/:ticketId" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Route Test Ticket')).toBeInTheDocument();
    });
  });

  it('passes_ticketId_param_to_service_call', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-route/tickets/ticket-route-test']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/tickets/:ticketId" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(ticketService.getTicketById).toHaveBeenCalledWith('ticket-route-test');
    });
  });

  it('renders_back_link_with_correct_workspace_id', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-route/tickets/ticket-route-test']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/tickets/:ticketId" element={<TicketDetailPage />} />
          <Route path="/workspaces/:workspaceId/tickets" element={<div>List</div>} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      const backLink = screen.getByText(/Back to Tickets/i);
      expect(backLink.closest('a')).toHaveAttribute('href', '/workspaces/ws-route/tickets');
    });
  });

  it('renders_edit_link_with_correct_workspace_and_ticket_ids', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-route/tickets/ticket-route-test']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/tickets/:ticketId" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      const editLink = screen.getByRole('link', { name: /edit/i });
      expect(editLink).toHaveAttribute('href', '/workspaces/ws-route/tickets/ticket-route-test/edit');
    });
  });

  it('handles_composite_external_ticket_id_in_route', async () => {
    const externalTicket = {
      ...mockTicket,
      id: 'int-abc:EXT-456',
      internal: false,
      source: 'GITHUB',
    };
    vi.mocked(ticketService.getTicketById).mockResolvedValue(externalTicket);

    render(
      <MemoryRouter initialEntries={['/workspaces/ws-route/tickets/int-abc:EXT-456']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/tickets/:ticketId" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(ticketService.getTicketById).toHaveBeenCalledWith('int-abc:EXT-456');
    });
  });
});
