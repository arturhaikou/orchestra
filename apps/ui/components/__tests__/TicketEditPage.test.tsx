import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
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
  { id: 'status-in-progress', name: 'In Progress', color: '#eab308' },
];

const mockPriorities = [
  { id: 'priority-low', name: 'Low', color: '#6b7280', value: 1 },
  { id: 'priority-medium', name: 'Medium', color: '#eab308', value: 2 },
  { id: 'priority-high', name: 'High', color: '#ef4444', value: 3 },
];

const mockTicket = {
  id: 'ticket-123',
  workspaceId: 'ws-test',
  title: 'Login Bug',
  description: 'Users cannot log in with SSO',
  source: 'INTERNAL',
  internal: true,
  status: mockStatuses[0],
  priority: mockPriorities[1],
  satisfaction: 100,
  comments: [],
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const TicketListPlaceholder: React.FC = () => <div data-testid="ticket-list">Ticket List</div>;

const renderEditPage = (ticketId = 'ticket-123') => {
  return render(
    <MemoryRouter initialEntries={[`/workspaces/ws-test/tickets/${ticketId}/edit`]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/tickets/:ticketId/edit" element={<TicketEditPage />} />
        <Route path="/workspaces/:workspaceId/tickets" element={<TicketListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('TicketEditPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Scenario 1: Navigate to ticket edit page', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);
    });

    it('renders_loading_state_initially', () => {
      renderEditPage();
      expect(screen.getByText(/back to tickets/i)).toBeInTheDocument();
    });

    it('renders_edit_ticket_heading_after_load', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(screen.getByText(/edit ticket/i)).toBeInTheDocument();
      });
    });

    it('renders_form_with_current_ticket_title', async () => {
      renderEditPage();
      await waitFor(() => {
        const titleInput = screen.getByLabelText(/title/i);
        expect(titleInput).toHaveValue('Login Bug');
      });
    });

    it('renders_form_with_current_ticket_description', async () => {
      renderEditPage();
      await waitFor(() => {
        const descInput = screen.getByLabelText(/description/i);
        expect(descInput).toHaveValue('Users cannot log in with SSO');
      });
    });

    it('renders_character_counters', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(screen.getByText(/9 \/ 500/)).toBeInTheDocument();
      });
    });

    it('renders_save_and_cancel_buttons', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(screen.getByText(/save changes/i)).toBeInTheDocument();
        expect(screen.getByText(/cancel/i)).toBeInTheDocument();
      });
    });

    it('loads_ticket_and_lookups_in_parallel', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(ticketService.getTicketById).toHaveBeenCalledWith('ticket-123');
        expect(ticketService.getTicketStatuses).toHaveBeenCalled();
        expect(ticketService.getTicketPriorities).toHaveBeenCalled();
      });
    });
  });

  describe('Scenario 2: Successfully update a ticket', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);
      vi.mocked(ticketService.updateTicket).mockResolvedValue({ ...mockTicket, description: 'Updated description' });
    });

    it('navigates_to_tickets_list_on_successful_save', async () => {
      const user = userEvent.setup();
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/description/i)).toBeInTheDocument();
      });

      const descInput = screen.getByLabelText(/description/i);
      await user.clear(descInput);
      await user.type(descInput, 'Updated description');

      const saveButton = screen.getByText(/save changes/i);
      await user.click(saveButton);

      await waitFor(() => {
        expect(ticketService.updateTicket).toHaveBeenCalled();
      });

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent('/workspaces/ws-test/tickets');
      });
    });

    it('shows_saving_state_during_submission', async () => {
      let resolveUpdate: (value: any) => void;
      vi.mocked(ticketService.updateTicket).mockImplementation(
        () => new Promise((resolve) => { resolveUpdate = resolve; })
      );

      const user = userEvent.setup();
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      const saveButton = screen.getByText(/save changes/i);
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/saving/i)).toBeInTheDocument();
      });

      resolveUpdate!({ ...mockTicket });
    });

    it('shows_error_toast_when_save_fails', async () => {
      vi.mocked(ticketService.updateTicket).mockRejectedValue(new Error('Server error'));

      const user = userEvent.setup();
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      const saveButton = screen.getByText(/save changes/i);
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/server error/i)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 3: Cancel editing', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);
    });

    it('navigates_to_tickets_list_on_cancel', async () => {
      const user = userEvent.setup();
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByText(/cancel/i)).toBeInTheDocument();
      });

      const cancelButton = screen.getByText(/cancel/i);
      await user.click(cancelButton);

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent('/workspaces/ws-test/tickets');
      });
    });

    it('does_not_call_update_api_on_cancel', async () => {
      const user = userEvent.setup();
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByText(/cancel/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/cancel/i));

      expect(ticketService.updateTicket).not.toHaveBeenCalled();
    });
  });

  describe('Scenario 4: Ticket not found', () => {
    it('renders_error_state_when_ticket_not_found', async () => {
      vi.mocked(ticketService.getTicketById).mockRejectedValue(new Error('Ticket not found'));
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      renderEditPage('nonexistent-id');

      await waitFor(() => {
        expect(screen.getByText(/ticket not found/i)).toBeInTheDocument();
      });
    });

    it('renders_return_to_tickets_link', async () => {
      vi.mocked(ticketService.getTicketById).mockRejectedValue(new Error('Ticket not found'));
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      renderEditPage('nonexistent-id');

      await waitFor(() => {
        expect(screen.getByText(/return to tickets/i)).toBeInTheDocument();
      });
    });

    it('navigates_to_tickets_list_from_error_state', async () => {
      vi.mocked(ticketService.getTicketById).mockRejectedValue(new Error('Ticket not found'));
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const user = userEvent.setup();
      renderEditPage('nonexistent-id');

      await waitFor(() => {
        expect(screen.getByText(/return to tickets/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/return to tickets/i));

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent('/workspaces/ws-test/tickets');
      });
    });
  });

  describe('Client-side validation', () => {
    beforeEach(() => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);
    });

    it('shows_error_when_title_is_empty', async () => {
      const user = userEvent.setup();
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      const titleInput = screen.getByLabelText(/title/i);
      await user.clear(titleInput);

      const saveButton = screen.getByText(/save changes/i);
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/title is required/i)).toBeInTheDocument();
      });

      expect(ticketService.updateTicket).not.toHaveBeenCalled();
    });

    it('shows_error_when_title_exceeds_500_characters', async () => {
      const user = userEvent.setup();
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      const titleInput = screen.getByLabelText(/title/i);
      await user.clear(titleInput);
      await user.type(titleInput, 'x'.repeat(501));

      const saveButton = screen.getByText(/save changes/i);
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/must not exceed 500/i)).toBeInTheDocument();
      });

      expect(ticketService.updateTicket).not.toHaveBeenCalled();
    });
  });
});
