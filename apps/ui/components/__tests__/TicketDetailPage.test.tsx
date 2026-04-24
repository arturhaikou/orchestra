import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
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

const mockStatuses = [
  { id: 'status-open', name: 'Open', color: '#22c55e' },
  { id: 'status-in-progress', name: 'In Progress', color: '#eab308' },
];

const mockPriorities = [
  { id: 'priority-low', name: 'Low', color: '#6b7280', value: 1 },
  { id: 'priority-high', name: 'High', color: '#ef4444', value: 3 },
];

const mockInternalTicket = {
  id: 'ticket-123',
  workspaceId: 'ws-test',
  title: 'Login Bug',
  description: 'Users cannot log in with SSO provider.',
  source: 'INTERNAL',
  internal: true,
  status: mockStatuses[0],
  priority: mockPriorities[1],
  satisfaction: 85,
  comments: [
    { id: 'c1', author: 'Alice', content: 'Reproduced on Chrome', timestamp: '2026-04-20T10:00:00Z' },
    { id: 'c2', author: 'Bob', content: 'Fix deployed to staging', timestamp: '2026-04-21T14:30:00Z' },
  ],
  summary: 'SSO login failure affecting multiple users.',
};

const mockExternalTicket = {
  id: 'integration-456:PROJ-789',
  workspaceId: 'ws-test',
  title: 'External API Timeout',
  description: 'Third-party API calls timing out under load.',
  source: 'JIRA',
  internal: false,
  integrationId: 'integration-456',
  externalTicketId: 'PROJ-789',
  status: mockStatuses[1],
  priority: mockPriorities[0],
  satisfaction: 42,
  comments: [
    { id: 'ec1', author: 'Jira User', content: 'Investigating timeout root cause', timestamp: null },
  ],
  summary: null,
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const TicketListPlaceholder: React.FC = () => <div data-testid="ticket-list">Ticket List</div>;
const TicketEditPlaceholder: React.FC = () => <div data-testid="ticket-edit">Ticket Edit</div>;

const renderDetailPage = (ticketId = 'ticket-123', workspaceId = 'ws-test') => {
  return render(
    <MemoryRouter initialEntries={[`/workspaces/${workspaceId}/tickets/${ticketId}`]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/tickets/:ticketId" element={<TicketDetailPage />} />
        <Route path="/workspaces/:workspaceId/tickets/:ticketId/edit" element={<TicketEditPlaceholder />} />
        <Route path="/workspaces/:workspaceId/tickets" element={<TicketListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('TicketDetailPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Scenario 1: Navigate to ticket detail page', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockInternalTicket);
    });

    it('calls_getTicketById_with_correct_ticketId', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(ticketService.getTicketById).toHaveBeenCalledWith('ticket-123');
      });
    });

    it('displays_ticket_title', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText('Login Bug')).toBeInTheDocument();
      });
    });

    it('displays_ticket_description', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText(/Users cannot log in with SSO/)).toBeInTheDocument();
      });
    });

    it('displays_ticket_status', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText('Open')).toBeInTheDocument();
      });
    });

    it('displays_ticket_priority', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText('High')).toBeInTheDocument();
      });
    });

    it('displays_comments_in_chronological_order', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText('Reproduced on Chrome')).toBeInTheDocument();
        expect(screen.getByText('Fix deployed to staging')).toBeInTheDocument();
      });
      const comments = screen.getAllByTestId('comment-item');
      expect(comments).toHaveLength(2);
    });

    it('displays_satisfaction_score', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText(/85/)).toBeInTheDocument();
      });
    });

    it('displays_source_as_internal', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText(/Internal/i)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 2: Direct URL access to ticket detail', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockInternalTicket);
    });

    it('renders_at_correct_route_path', () => {
      renderDetailPage();
      expect(screen.getByTestId('location-display')).toHaveTextContent('/workspaces/ws-test/tickets/ticket-123');
    });

    it('displays_loading_state_initially', () => {
      renderDetailPage();
      expect(screen.getByText(/Back to Tickets/i)).toBeInTheDocument();
    });

    it('displays_back_to_tickets_link_with_correct_href', async () => {
      renderDetailPage();
      await waitFor(() => {
        const backLink = screen.getByText(/Back to Tickets/i);
        expect(backLink.closest('a')).toHaveAttribute('href', '/workspaces/ws-test/tickets');
      });
    });
  });

  describe('Scenario 3: Navigate from detail to edit', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockInternalTicket);
    });

    it('renders_edit_button', async () => {
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByRole('link', { name: /edit/i })).toBeInTheDocument();
      });
    });

    it('edit_button_links_to_edit_route', async () => {
      renderDetailPage();
      await waitFor(() => {
        const editLink = screen.getByRole('link', { name: /edit/i });
        expect(editLink).toHaveAttribute('href', '/workspaces/ws-test/tickets/ticket-123/edit');
      });
    });

    it('clicking_edit_navigates_to_edit_page', async () => {
      const user = userEvent.setup();
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByRole('link', { name: /edit/i })).toBeInTheDocument();
      });
      await user.click(screen.getByRole('link', { name: /edit/i }));
      await waitFor(() => {
        expect(screen.getByTestId('ticket-edit')).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 4: Ticket not found', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockRejectedValue(new Error('Not found'));
    });

    it('displays_error_state_when_ticket_not_found', async () => {
      renderDetailPage('nonexistent-id');
      await waitFor(() => {
        expect(screen.getByText(/Ticket Not Found/i)).toBeInTheDocument();
      });
    });

    it('displays_return_to_tickets_list_link', async () => {
      renderDetailPage('nonexistent-id');
      await waitFor(() => {
        const returnLink = screen.getByText(/Return to Tickets List/i);
        expect(returnLink.closest('a')).toHaveAttribute('href', '/workspaces/ws-test/tickets');
      });
    });

    it('clicking_return_link_navigates_to_tickets_list', async () => {
      const user = userEvent.setup();
      renderDetailPage('nonexistent-id');
      await waitFor(() => {
        expect(screen.getByText(/Return to Tickets List/i)).toBeInTheDocument();
      });
      await user.click(screen.getByText(/Return to Tickets List/i));
      await waitFor(() => {
        expect(screen.getByTestId('ticket-list')).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 5: External ticket detail shows merged data', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockExternalTicket);
    });

    it('displays_external_provider_badge', async () => {
      renderDetailPage('integration-456:PROJ-789');
      await waitFor(() => {
        expect(screen.getByText(/Jira/i)).toBeInTheDocument();
      });
    });

    it('displays_external_ticket_key', async () => {
      renderDetailPage('integration-456:PROJ-789');
      await waitFor(() => {
        expect(screen.getByText(/PROJ-789/)).toBeInTheDocument();
      });
    });

    it('displays_external_ticket_title', async () => {
      renderDetailPage('integration-456:PROJ-789');
      await waitFor(() => {
        expect(screen.getByText('External API Timeout')).toBeInTheDocument();
      });
    });

    it('displays_satisfaction_score_for_external_ticket', async () => {
      renderDetailPage('integration-456:PROJ-789');
      await waitFor(() => {
        expect(screen.getByText(/42/)).toBeInTheDocument();
      });
    });

    it('displays_external_source_label', async () => {
      renderDetailPage('integration-456:PROJ-789');
      await waitFor(() => {
        expect(screen.getByText(/Jira/i)).toBeInTheDocument();
      });
    });
  });

  describe('Edge cases', () => {
    it('displays_loading_spinner_while_fetching', () => {
      vi.mocked(ticketService.getTicketById).mockReturnValue(new Promise(() => {}));
      renderDetailPage();
      expect(document.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('handles_ticket_with_no_comments', async () => {
      const ticketNoComments = { ...mockInternalTicket, comments: [] };
      vi.mocked(ticketService.getTicketById).mockResolvedValue(ticketNoComments);
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText('Login Bug')).toBeInTheDocument();
      });
      expect(screen.queryAllByTestId('comment-item')).toHaveLength(0);
    });

    it('handles_ticket_with_null_satisfaction', async () => {
      const ticketNoSat = { ...mockInternalTicket, satisfaction: null as any };
      vi.mocked(ticketService.getTicketById).mockResolvedValue(ticketNoSat);
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText('Login Bug')).toBeInTheDocument();
      });
    });

    it('handles_ticket_with_null_status', async () => {
      const ticketNoStatus = { ...mockInternalTicket, status: null };
      vi.mocked(ticketService.getTicketById).mockResolvedValue(ticketNoStatus);
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText('Login Bug')).toBeInTheDocument();
      });
    });

    it('handles_ticket_with_null_priority', async () => {
      const ticketNoPriority = { ...mockInternalTicket, priority: null };
      vi.mocked(ticketService.getTicketById).mockResolvedValue(ticketNoPriority);
      renderDetailPage();
      await waitFor(() => {
        expect(screen.getByText('Login Bug')).toBeInTheDocument();
      });
    });
  });
});
