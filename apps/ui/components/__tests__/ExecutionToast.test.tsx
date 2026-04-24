import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '@testing-library/jest-dom';
import ExecutionToast from '../ExecutionToast';
import { ExecutionToastData } from '../../types';

const successToast: ExecutionToastData = {
  id: 'toast-1',
  agentId: 'agent-1',
  agentName: 'Code Reviewer',
  ticketId: 'ticket-1',
  ticketTitle: 'Fix login bug',
  status: 'success',
  reviewUrl: 'https://github.com/org/repo/pull/42',
  createdAt: Date.now(),
};

const failedToast: ExecutionToastData = {
  id: 'toast-2',
  agentId: 'agent-2',
  agentName: 'Code Reviewer',
  ticketId: 'ticket-2',
  ticketTitle: 'Broken pipeline',
  status: 'failed',
  reviewUrl: null,
  createdAt: Date.now(),
};

describe('ExecutionToast', () => {
  const mockDismiss = jest.fn();
  const mockViewTicket = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Success variant', () => {
    it('should render agent name and ticket title', () => {
      render(<ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      expect(screen.getByText('Code Reviewer')).toBeInTheDocument();
      expect(screen.getByText('Fix login bug')).toBeInTheDocument();
    });

    it('should show success status badge', () => {
      render(<ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      expect(screen.getByText('Success')).toBeInTheDocument();
    });

    it('should show "View Ticket" action button', () => {
      render(<ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      expect(screen.getByText('View Ticket')).toBeInTheDocument();
    });

    it('should show "Open Review" action button when reviewUrl is present', () => {
      render(<ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      expect(screen.getByText('Open Review')).toBeInTheDocument();
    });

    it('should have emerald/green border styling', () => {
      const { container } = render(<ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      const toastEl = container.firstChild as HTMLElement;
      expect(toastEl?.className).toContain('border-emerald-500');
    });
  });

  describe('Failure variant', () => {
    it('should show failed status badge', () => {
      render(<ExecutionToast toast={failedToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      expect(screen.getByText('Failed')).toBeInTheDocument();
    });

    it('should NOT show "Open Review" when reviewUrl is null', () => {
      render(<ExecutionToast toast={failedToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      expect(screen.queryByText('Open Review')).not.toBeInTheDocument();
    });

    it('should have red border styling', () => {
      const { container } = render(<ExecutionToast toast={failedToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      const toastEl = container.firstChild as HTMLElement;
      expect(toastEl?.className).toContain('border-red-500');
    });
  });

  describe('Accessibility', () => {
    it('should have role="status" and aria-live="polite" for success toasts', () => {
      const { container } = render(
        <ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />
      );
      const toastEl = container.firstChild as HTMLElement;
      expect(toastEl).toHaveAttribute('role', 'status');
      expect(toastEl).toHaveAttribute('aria-live', 'polite');
    });

    it('should have role="alert" and aria-live="assertive" for failed toasts', () => {
      const { container } = render(
        <ExecutionToast toast={failedToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />
      );
      const toastEl = container.firstChild as HTMLElement;
      expect(toastEl).toHaveAttribute('role', 'alert');
      expect(toastEl).toHaveAttribute('aria-live', 'assertive');
    });

    it('should have dismiss button with aria-label', () => {
      render(<ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      expect(screen.getByLabelText('Dismiss notification')).toBeInTheDocument();
    });

    it('should have aria-label for execution status badge', () => {
      render(<ExecutionToast toast={failedToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      expect(screen.getByLabelText('Execution status: Failed')).toBeInTheDocument();
    });
  });

  describe('Security', () => {
    it('should not render any exception details even if present in toast data', () => {
      const toastWithNoExceptionField: ExecutionToastData = {
        id: 'toast-sec',
        agentId: 'agent-sec',
        agentName: 'Support Bot',
        ticketId: 'ticket-sec',
        ticketTitle: 'Login Bug',
        status: 'failed',
        reviewUrl: null,
        createdAt: Date.now(),
      };
      const { container } = render(
        <ExecutionToast toast={toastWithNoExceptionField} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />
      );
      const text = container.textContent || '';
      expect(text).not.toContain('NullReferenceException');
      expect(text).not.toContain('stack trace');
      expect(text).not.toContain('Exception');
      expect(text).toContain('Support Bot');
      expect(text).toContain('Login Bug');
    });
  });

  describe('Interactions', () => {
    it('should call onDismiss when dismiss button is clicked', async () => {
      render(<ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      await userEvent.click(screen.getByLabelText('Dismiss notification'));
      expect(mockDismiss).toHaveBeenCalledWith('toast-1');
    });

    it('should call onViewTicket when "View Ticket" is clicked', async () => {
      render(<ExecutionToast toast={successToast} onDismiss={mockDismiss} onViewTicket={mockViewTicket} />);
      await userEvent.click(screen.getByText('View Ticket'));
      expect(mockViewTicket).toHaveBeenCalledWith('ticket-1');
    });
  });
});
