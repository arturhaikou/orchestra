import React from 'react';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import AuthGuard from '../AuthGuard';
import * as authService from '../../services/authService';

vi.mock('../../services/authService', () => ({
  getToken: vi.fn(),
}));

const renderWithRouter = (initialEntry: string) => {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route
          path="/workspaces/:workspaceId/*"
          element={
            <AuthGuard>
              <div data-testid="protected-content">Protected Content</div>
            </AuthGuard>
          }
        />
        <Route path="/login" element={<div data-testid="login-page">Login Page</div>} />
      </Routes>
    </MemoryRouter>
  );
};

const LoginStateCapture: React.FC<{ onState: (state: any) => void }> = ({ onState }) => {
  const location = useLocation();
  React.useEffect(() => {
    onState(location.state);
  }, [location.state, onState]);
  return <div data-testid="login-page">Login</div>;
};

describe('AuthGuard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders_children_when_authenticated', () => {
    vi.mocked(authService.getToken).mockReturnValue('valid-jwt-token');

    renderWithRouter('/workspaces/abc-123/tickets');

    expect(screen.getByTestId('protected-content')).toBeInTheDocument();
    expect(screen.queryByTestId('login-page')).not.toBeInTheDocument();
  });

  it('redirects_to_login_when_unauthenticated', () => {
    vi.mocked(authService.getToken).mockReturnValue(null);

    renderWithRouter('/workspaces/abc-123/agents');

    expect(screen.getByTestId('login-page')).toBeInTheDocument();
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
  });

  it('redirects_to_login_when_token_is_empty_string', () => {
    vi.mocked(authService.getToken).mockReturnValue('');

    renderWithRouter('/workspaces/abc-123/tickets');

    expect(screen.getByTestId('login-page')).toBeInTheDocument();
    expect(screen.queryByTestId('protected-content')).not.toBeInTheDocument();
  });

  it('preserves_original_path_in_login_redirect_state', () => {
    vi.mocked(authService.getToken).mockReturnValue(null);

    let loginLocationState: any;
    render(
      <MemoryRouter initialEntries={['/workspaces/abc-123/agents']}>
        <Routes>
          <Route
            path="/workspaces/:workspaceId/*"
            element={
              <AuthGuard>
                <div>Protected</div>
              </AuthGuard>
            }
          />
          <Route
            path="/login"
            element={
              <LoginStateCapture onState={(state: any) => { loginLocationState = state; }} />
            }
          />
        </Routes>
      </MemoryRouter>
    );

    expect(loginLocationState?.from).toBe('/workspaces/abc-123/agents');
  });
});
