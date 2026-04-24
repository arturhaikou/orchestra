import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import Integrations from '../Integrations';
import * as integrationService from '../../services/integrationService';

vi.mock('../../services/integrationService', () => ({
  createIntegration: vi.fn(),
  getIntegrations: vi.fn(),
  updateIntegration: vi.fn(),
  deleteIntegration: vi.fn(),
  testIntegrationConnection: vi.fn(),
}));

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const IntegrationCreatePlaceholder: React.FC = () => <div data-testid="create-page">Create Page</div>;

const renderIntegrationsList = () => {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/integrations']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/integrations" element={<Integrations />} />
        <Route path="/workspaces/:workspaceId/integrations/new" element={<IntegrationCreatePlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('Integrations — Add Connection Navigation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(integrationService.getIntegrations).mockResolvedValue([]);
  });

  it('navigates_to_integrations_new_when_add_connection_is_clicked', async () => {
    renderIntegrationsList();

    await waitFor(() => {
      expect(screen.getByText(/add connection/i)).toBeInTheDocument();
    });

    await userEvent.click(screen.getByText(/add connection/i));

    await waitFor(() => {
      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations/new');
    });
  });

  it('does_not_open_modal_when_add_connection_is_clicked', async () => {
    renderIntegrationsList();

    await waitFor(() => {
      expect(screen.getByText(/add connection/i)).toBeInTheDocument();
    });

    await userEvent.click(screen.getByText(/add connection/i));

    await waitFor(() => {
      expect(screen.queryByText(/new integration/i)).not.toBeInTheDocument();
    });
  });
});
