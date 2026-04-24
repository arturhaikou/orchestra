import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import ProfileEditPage from '../pages/ProfileEditPage';
import * as authService from '../../services/authService';

vi.mock('../../services/authService', () => ({
  getUser: vi.fn(),
  updateUser: vi.fn(),
  changePassword: vi.fn(),
  getToken: vi.fn(),
  login: vi.fn(),
  register: vi.fn(),
  logout: vi.fn(),
}));

const mockUser = {
  id: 'user-123',
  name: 'Jane Doe',
  email: 'jane@example.com',
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const TicketsListPlaceholder: React.FC = () => <div data-testid="tickets-list">Tickets List</div>;

const renderProfilePage = () => {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/profile']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
        <Route path="/workspaces/:workspaceId/tickets" element={<TicketsListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('ProfileEditPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(authService.getUser).mockReturnValue(mockUser);
    vi.mocked(authService.updateUser).mockResolvedValue(mockUser);
  });

  describe('Scenario 1: Navigate to profile page', () => {
    it('displays_profile_form_with_current_name', async () => {
      renderProfilePage();
      await waitFor(() => {
        const nameInput = screen.getByLabelText(/display name/i);
        expect(nameInput).toHaveValue('Jane Doe');
      });
    });

    it('displays_profile_form_with_current_email', async () => {
      renderProfilePage();
      await waitFor(() => {
        const emailInput = screen.getByLabelText(/email address/i);
        expect(emailInput).toHaveValue('jane@example.com');
      });
    });

    it('displays_empty_password_fields', async () => {
      renderProfilePage();
      await waitFor(() => {
        const currentPw = screen.getByLabelText(/current password/i);
        const newPw = screen.getByLabelText(/new password/i);
        const confirmPw = screen.getByLabelText(/confirm new/i);
        expect(currentPw).toHaveValue('');
        expect(newPw).toHaveValue('');
        expect(confirmPw).toHaveValue('');
      });
    });
  });

  describe('Scenario 2: Successfully update profile', () => {
    it('calls_updateUser_and_navigates_to_tickets_on_save', async () => {
      const user = userEvent.setup();
      const updatedUser = { ...mockUser, name: 'Jane Smith' };
      vi.mocked(authService.updateUser).mockResolvedValue(updatedUser);

      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
      });

      const nameInput = screen.getByLabelText(/display name/i);
      await user.clear(nameInput);
      await user.type(nameInput, 'Jane Smith');

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(authService.updateUser).toHaveBeenCalledWith({
          name: 'Jane Smith',
          email: 'jane@example.com',
        });
      });

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent('/workspaces/ws-test/tickets');
      });
    });

    it('disables_form_during_save', async () => {
      const user = userEvent.setup();
      let resolveUpdate: (value: any) => void;
      vi.mocked(authService.updateUser).mockImplementation(
        () => new Promise((resolve) => { resolveUpdate = resolve; })
      );

      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
      });

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/saving/i)).toBeInTheDocument();
      });

      resolveUpdate!(mockUser);
    });
  });

  describe('Scenario 3: Cancel profile editing', () => {
    it('navigates_back_on_cancel', async () => {
      const user = userEvent.setup();

      render(
        <MemoryRouter initialEntries={['/workspaces/ws-test/tickets', '/workspaces/ws-test/profile']}>
          <Routes>
            <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
            <Route path="/workspaces/:workspaceId/tickets" element={<TicketsListPlaceholder />} />
          </Routes>
          <LocationDisplay />
        </MemoryRouter>
      );

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      });

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      await waitFor(() => {
        expect(screen.getByTestId('location-display')).toHaveTextContent('/workspaces/ws-test/tickets');
      });
    });
  });

  describe('Scenario 4: Password change validation', () => {
    it('shows_error_when_new_password_without_current_password', async () => {
      const user = userEvent.setup();
      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/new password/i)).toBeInTheDocument();
      });

      const newPwInput = screen.getByLabelText(/new password/i);
      const confirmPwInput = screen.getByLabelText(/confirm new/i);
      await user.type(newPwInput, 'NewPass123!');
      await user.type(confirmPwInput, 'NewPass123!');

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/current password is required/i)).toBeInTheDocument();
      });

      expect(authService.updateUser).not.toHaveBeenCalled();
    });

    it('shows_error_when_passwords_do_not_match', async () => {
      const user = userEvent.setup();
      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
      });

      await user.type(screen.getByLabelText(/current password/i), 'OldPass123!');
      await user.type(screen.getByLabelText(/new password/i), 'NewPass123!');
      await user.type(screen.getByLabelText(/confirm new/i), 'DifferentPass123!');

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/new passwords do not match/i)).toBeInTheDocument();
      });

      expect(authService.updateUser).not.toHaveBeenCalled();
    });

    it('shows_error_for_weak_password', async () => {
      const user = userEvent.setup();
      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
      });

      await user.type(screen.getByLabelText(/current password/i), 'OldPass123!');
      await user.type(screen.getByLabelText(/new password/i), 'weak');
      await user.type(screen.getByLabelText(/confirm new/i), 'weak');

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/password must be at least 8 characters/i)).toBeInTheDocument();
      });

      expect(authService.updateUser).not.toHaveBeenCalled();
    });

    it('calls_changePassword_when_password_fields_valid', async () => {
      const user = userEvent.setup();
      vi.mocked(authService.changePassword).mockResolvedValue(undefined);

      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
      });

      await user.type(screen.getByLabelText(/current password/i), 'OldPass123!');
      await user.type(screen.getByLabelText(/new password/i), 'NewStrongPass1!');
      await user.type(screen.getByLabelText(/confirm new/i), 'NewStrongPass1!');

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(authService.updateUser).toHaveBeenCalled();
      });

      await waitFor(() => {
        expect(authService.changePassword).toHaveBeenCalledWith('OldPass123!', 'NewStrongPass1!');
      });
    });
  });

  describe('Edge cases', () => {
    it('shows_validation_error_when_name_is_empty', async () => {
      const user = userEvent.setup();
      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
      });

      const nameInput = screen.getByLabelText(/display name/i);
      await user.clear(nameInput);

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/name is required/i)).toBeInTheDocument();
      });

      expect(authService.updateUser).not.toHaveBeenCalled();
    });

    it('shows_validation_error_when_email_is_empty', async () => {
      const user = userEvent.setup();
      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/email address/i)).toBeInTheDocument();
      });

      const emailInput = screen.getByLabelText(/email address/i);
      await user.clear(emailInput);

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/email is required/i)).toBeInTheDocument();
      });

      expect(authService.updateUser).not.toHaveBeenCalled();
    });

    it('shows_api_error_on_update_failure', async () => {
      const user = userEvent.setup();
      vi.mocked(authService.updateUser).mockRejectedValue(new Error('Update failed'));

      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
      });

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/update failed/i)).toBeInTheDocument();
      });
    });

    it('shows_api_error_on_password_change_failure', async () => {
      const user = userEvent.setup();
      vi.mocked(authService.updateUser).mockResolvedValue(mockUser);
      vi.mocked(authService.changePassword).mockRejectedValue(new Error('Invalid credentials'));

      renderProfilePage();

      await waitFor(() => {
        expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
      });

      await user.type(screen.getByLabelText(/current password/i), 'WrongPass123!');
      await user.type(screen.getByLabelText(/new password/i), 'NewStrongPass1!');
      await user.type(screen.getByLabelText(/confirm new/i), 'NewStrongPass1!');

      const saveButton = screen.getByRole('button', { name: /save profile/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/invalid credentials/i)).toBeInTheDocument();
      });
    });
  });
});
