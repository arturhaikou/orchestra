import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import TicketCreatePage from '../pages/TicketCreatePage';
import * as ticketService from '../../services/ticketService';

vi.mock('../../services/ticketService', () => ({
  createTicket: vi.fn(),
  getTicketStatuses: vi.fn(),
  getTicketPriorities: vi.fn(),
  getTickets: vi.fn(),
  updateTicket: vi.fn(),
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

const mockCreatedTicket = {
  id: 'ticket-new-1',
  workspaceId: 'ws-test',
  title: 'Test Ticket',
  description: 'Test description for the ticket',
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

const renderTicketCreatePage = () => {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/tickets/new']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/tickets/new" element={<TicketCreatePage />} />
        <Route path="/workspaces/:workspaceId/tickets" element={<TicketListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('TicketCreatePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
    vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);
    vi.mocked(ticketService.createTicket).mockResolvedValue(mockCreatedTicket);
  });

  describe('Scenario 1: Navigate to ticket creation page', () => {
    it('renders_ticket_creation_form_with_required_fields', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/create new ticket/i)).toBeInTheDocument();
      });

      expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/description/i)).toBeInTheDocument();
    });

    it('renders_status_and_priority_dropdowns', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/status/i)).toBeInTheDocument();
        expect(screen.getByText(/priority/i)).toBeInTheDocument();
      });
    });

    it('renders_save_and_cancel_buttons', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      });
    });

    it('renders_back_to_tickets_link', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/back to tickets/i)).toBeInTheDocument();
      });
    });

    it('loads_statuses_and_priorities_on_mount', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(ticketService.getTicketStatuses).toHaveBeenCalled();
        expect(ticketService.getTicketPriorities).toHaveBeenCalled();
      });
    });

    it('renders_character_counters_for_title_and_description', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/0\s*\/\s*500/)).toBeInTheDocument();
        expect(screen.getByText(/0\s*\/\s*10,?000/)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 2: Successfully create a ticket', () => {
    it('creates_ticket_and_navigates_to_list_on_save', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/title/i), 'Test Ticket');
      await userEvent.type(screen.getByLabelText(/description/i), 'Test description for the ticket');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(ticketService.createTicket).toHaveBeenCalledWith(
          'ws-test',
          expect.objectContaining({
            title: 'Test Ticket',
            description: 'Test description for the ticket',
          })
        );
      });

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/tickets');
      });
    });
  });

  describe('Scenario 3: Cancel ticket creation', () => {
    it('navigates_to_tickets_list_on_cancel_without_creating', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      });

      await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

      expect(ticketService.createTicket).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/tickets');
      });
    });

    it('navigates_to_tickets_list_via_back_link', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByText(/back to tickets/i)).toBeInTheDocument();
      });

      await userEvent.click(screen.getByText(/back to tickets/i));

      expect(ticketService.createTicket).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/tickets');
      });
    });
  });

  describe('Scenario 4: Title exceeds maximum length', () => {
    it('shows_validation_error_when_title_exceeds_500_characters', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      const longTitle = 'a'.repeat(501);
      const { fireEvent } = await import('@testing-library/react');
      fireEvent.change(screen.getByLabelText(/title/i), { target: { value: longTitle } });
      fireEvent.change(screen.getByLabelText(/description/i), { target: { value: 'Valid description' } });

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      expect(ticketService.createTicket).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/title.*must not exceed 500 characters/i)).toBeInTheDocument();
      });

      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/tickets/new');
    });

    it('shows_validation_error_when_title_is_empty', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/description/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/description/i), 'Valid description');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      expect(ticketService.createTicket).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/title is required/i)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 5: Description at maximum boundary', () => {
    it('creates_ticket_successfully_with_description_at_10000_characters', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      const maxDescription = 'a'.repeat(10000);
      const { fireEvent } = await import('@testing-library/react');
      fireEvent.change(screen.getByLabelText(/title/i), { target: { value: 'Valid Title' } });
      fireEvent.change(screen.getByLabelText(/description/i), { target: { value: maxDescription } });

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(ticketService.createTicket).toHaveBeenCalledWith(
          'ws-test',
          expect.objectContaining({
            title: 'Valid Title',
            description: maxDescription,
          })
        );
      });
    });
  });

  describe('Edge cases', () => {
    it('shows_validation_error_when_description_is_empty', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/title/i), 'Valid Title');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      expect(ticketService.createTicket).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/description is required/i)).toBeInTheDocument();
      });
    });

    it('shows_validation_error_when_description_exceeds_10000_characters', async () => {
      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      const { fireEvent } = await import('@testing-library/react');
      fireEvent.change(screen.getByLabelText(/title/i), { target: { value: 'Valid Title' } });
      fireEvent.change(screen.getByLabelText(/description/i), { target: { value: 'a'.repeat(10001) } });

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      expect(ticketService.createTicket).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/description.*must not exceed 10,?000 characters/i)).toBeInTheDocument();
      });
    });

    it('shows_error_toast_when_api_returns_error', async () => {
      vi.mocked(ticketService.createTicket).mockRejectedValue(new Error('Server error'));

      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/title/i), 'Test Ticket');
      await userEvent.type(screen.getByLabelText(/description/i), 'Test description');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(screen.getByText(/server error/i)).toBeInTheDocument();
      });

      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/tickets/new');
    });

    it('preserves_form_data_after_api_error', async () => {
      vi.mocked(ticketService.createTicket).mockRejectedValue(new Error('Network error'));

      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/title/i), 'My Ticket');
      await userEvent.type(screen.getByLabelText(/description/i), 'My description');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(screen.getByText(/network error/i)).toBeInTheDocument();
      });

      expect(screen.getByLabelText(/title/i)).toHaveValue('My Ticket');
      expect(screen.getByLabelText(/description/i)).toHaveValue('My description');
    });

    it('disables_save_button_while_saving', async () => {
      vi.mocked(ticketService.createTicket).mockImplementation(
        () => new Promise(() => {})
      );

      renderTicketCreatePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
      });

      await userEvent.type(screen.getByLabelText(/title/i), 'Test Ticket');
      await userEvent.type(screen.getByLabelText(/description/i), 'Test description');

      await userEvent.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(screen.getByText(/saving/i)).toBeInTheDocument();
      });
    });

    it('renders_form_on_direct_navigation_to_tickets_new', () => {
      renderTicketCreatePage();

      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/tickets/new');
    });
  });
});
