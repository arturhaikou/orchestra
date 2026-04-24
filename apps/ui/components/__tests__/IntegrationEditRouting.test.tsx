import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import Integrations from '../Integrations';
import * as integrationService from '../../services/integrationService';
import { IntegrationType } from '../../types';

vi.mock('../../services/integrationService', () => ({
  getIntegrations: vi.fn(),
  createIntegration: vi.fn(),
  updateIntegration: vi.fn(),
  deleteIntegration: vi.fn(),
  testIntegrationConnection: vi.fn(),
}));

const mockIntegration = {
  id: 'int-1',
  workspaceId: 'ws-test',
  name: 'Jira Production',
  types: [IntegrationType.TRACKER],
  icon: 'jira',
  provider: 'jira',
  connected: true,
  lastSync: '2 hours ago',
  url: 'https://company.atlassian.net',
  filterQuery: 'project = PROD',
  vectorize: false,
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const IntegrationEditPlaceholder: React.FC = () => <div data-testid="edit-page">Edit Page</div>;

const renderIntegrationsList = () => {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/integrations']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/integrations" element={<Integrations />} />
        <Route path="/workspaces/:workspaceId/integrations/:integrationId/edit" element={<IntegrationEditPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('Integrations — Edit Navigation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(integrationService.getIntegrations).mockResolvedValue([mockIntegration]);
  });

  it('navigates_to_edit_page_when_configure_button_clicked', async () => {
    renderIntegrationsList();

    await waitFor(() => {
      expect(screen.getByText('Jira Production')).toBeInTheDocument();
    });

    await userEvent.click(screen.getByText(/configure/i));

    await waitFor(() => {
      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations/int-1/edit');
    });
  });

  it('does_not_open_edit_modal_when_configure_is_clicked', async () => {
    renderIntegrationsList();

    await waitFor(() => {
      expect(screen.getByText('Jira Production')).toBeInTheDocument();
    });

    await userEvent.click(screen.getByText(/configure/i));

    await waitFor(() => {
      expect(screen.queryByText(/edit integration/i)).not.toBeInTheDocument();
    });
  });
});
