import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
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

describe('Profile Edit Routing', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(authService.getUser).mockReturnValue(mockUser);
  });

  it('renders_profile_edit_page_at_correct_route', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/profile']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/profile settings/i)).toBeInTheDocument();
    });
  });

  it('renders_within_workspace_route_scope', () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/profile']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByText(/profile settings/i)).toBeInTheDocument();
  });

  it('renders_personal_information_section', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/profile']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/personal information/i)).toBeInTheDocument();
    });
  });

  it('renders_name_field_prepopulated_with_current_user', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/profile']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      const nameInput = screen.getByLabelText(/display name/i);
      expect(nameInput).toHaveValue('Jane Doe');
    });
  });

  it('renders_email_field_prepopulated_with_current_user', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/profile']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      const emailInput = screen.getByLabelText(/email address/i);
      expect(emailInput).toHaveValue('jane@example.com');
    });
  });

  it('renders_save_and_cancel_buttons', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/profile']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /save profile/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    });
  });

  it('renders_password_section', async () => {
    render(
      <MemoryRouter initialEntries={['/workspaces/ws-test/profile']}>
        <Routes>
          <Route path="/workspaces/:workspaceId/profile" element={<ProfileEditPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText(/security & password/i)).toBeInTheDocument();
    });
  });
});
