import { renderHook, act, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
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

const mockNavigate = vi.fn();
vi.mock('react-router-dom', () => ({
  useNavigate: () => mockNavigate,
}));

// Import after mocks
import { useTicketEditForm } from '../useTicketEditForm';

const mockStatuses = [
  { id: 'status-open', name: 'Open', color: '#22c55e' },
];

const mockPriorities = [
  { id: 'priority-medium', name: 'Medium', color: '#eab308', value: 2 },
];

const mockTicket = {
  id: 'ticket-123',
  workspaceId: 'ws-test',
  title: 'Login Bug',
  description: 'SSO issue',
  source: 'INTERNAL',
  internal: true,
  status: mockStatuses[0],
  priority: mockPriorities[0],
  satisfaction: 100,
  comments: [],
};

describe('useTicketEditForm', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('loading', () => {
    it('starts_in_loading_state', () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'ticket-123'));
      expect(result.current.isLoading).toBe(true);
    });

    it('loads_ticket_data_into_form_state', async () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'ticket-123'));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.formState.title).toBe('Login Bug');
      expect(result.current.formState.description).toBe('SSO issue');
      expect(result.current.formState.statusId).toBe('status-open');
      expect(result.current.formState.priorityId).toBe('priority-medium');
    });

    it('sets_load_error_when_ticket_not_found', async () => {
      vi.mocked(ticketService.getTicketById).mockRejectedValue(new Error('Ticket not found'));
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'nonexistent'));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.loadError).toBe('Ticket not found');
    });

    it('does_not_load_when_workspaceId_is_undefined', () => {
      const { result } = renderHook(() => useTicketEditForm(undefined, 'ticket-123'));
      expect(ticketService.getTicketById).not.toHaveBeenCalled();
    });

    it('does_not_load_when_ticketId_is_undefined', () => {
      const { result } = renderHook(() => useTicketEditForm('ws-test', undefined));
      expect(ticketService.getTicketById).not.toHaveBeenCalled();
    });
  });

  describe('cancel', () => {
    it('navigates_to_tickets_list_on_cancel', async () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'ticket-123'));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      act(() => {
        result.current.handleCancel();
      });

      expect(mockNavigate).toHaveBeenCalledWith('/workspaces/ws-test/tickets');
    });
  });

  describe('save', () => {
    it('calls_updateTicket_on_valid_save', async () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);
      vi.mocked(ticketService.updateTicket).mockResolvedValue(mockTicket);

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'ticket-123'));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
      await act(async () => {
        await result.current.handleSave(mockEvent);
      });

      expect(ticketService.updateTicket).toHaveBeenCalledWith('ticket-123', expect.objectContaining({
        title: 'Login Bug',
        description: 'SSO issue',
      }));
      expect(mockNavigate).toHaveBeenCalledWith('/workspaces/ws-test/tickets');
    });

    it('sets_save_error_on_api_failure', async () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);
      vi.mocked(ticketService.updateTicket).mockRejectedValue(new Error('Server error'));

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'ticket-123'));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
      await act(async () => {
        await result.current.handleSave(mockEvent);
      });

      expect(result.current.saveError).toBe('Server error');
      expect(mockNavigate).not.toHaveBeenCalled();
    });
  });

  describe('validation', () => {
    it('sets_validation_error_when_title_is_empty', async () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue({ ...mockTicket, title: '' });
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'ticket-123'));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
      await act(async () => {
        await result.current.handleSave(mockEvent);
      });

      expect(result.current.validationErrors.title).toBeDefined();
      expect(ticketService.updateTicket).not.toHaveBeenCalled();
    });

    it('sets_validation_error_when_title_exceeds_500_characters', async () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue({ ...mockTicket, title: 'x'.repeat(501) });
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'ticket-123'));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
      await act(async () => {
        await result.current.handleSave(mockEvent);
      });

      expect(result.current.validationErrors.title).toBeDefined();
      expect(ticketService.updateTicket).not.toHaveBeenCalled();
    });

    it('sets_validation_error_when_description_exceeds_10000_characters', async () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue({ ...mockTicket, description: 'x'.repeat(10001) });
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const { result } = renderHook(() => useTicketEditForm('ws-test', 'ticket-123'));

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      const mockEvent = { preventDefault: vi.fn() } as unknown as React.FormEvent;
      await act(async () => {
        await result.current.handleSave(mockEvent);
      });

      expect(result.current.validationErrors.description).toBeDefined();
      expect(ticketService.updateTicket).not.toHaveBeenCalled();
    });
  });

  describe('ticketsListPath', () => {
    it('returns_correct_path_for_workspace', () => {
      vi.mocked(ticketService.getTicketById).mockResolvedValue(mockTicket);
      vi.mocked(ticketService.getTicketStatuses).mockResolvedValue(mockStatuses);
      vi.mocked(ticketService.getTicketPriorities).mockResolvedValue(mockPriorities);

      const { result } = renderHook(() => useTicketEditForm('ws-abc', 'ticket-123'));
      expect(result.current.ticketsListPath).toBe('/workspaces/ws-abc/tickets');
    });
  });
});
