import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import IntegrationCreatePage from '../pages/IntegrationCreatePage';
import * as integrationService from '../../services/integrationService';
import { IntegrationType } from '../../types';

vi.mock('../../services/integrationService', () => ({
  createIntegration: vi.fn(),
  getIntegrations: vi.fn(),
  updateIntegration: vi.fn(),
  deleteIntegration: vi.fn(),
  testIntegrationConnection: vi.fn(),
}));

const mockCreatedIntegration = {
  id: 'integration-1',
  workspaceId: 'ws-test',
  name: 'My Jira',
  types: [IntegrationType.TRACKER],
  provider: 'jira',
  icon: 'jira',
  connected: true,
  lastSync: '',
  url: 'https://test.atlassian.net',
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const IntegrationsListPlaceholder: React.FC = () => <div data-testid="integrations-list">Integrations List</div>;

const renderIntegrationCreatePage = () => {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/integrations/new']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/integrations/new" element={<IntegrationCreatePage />} />
        <Route path="/workspaces/:workspaceId/integrations" element={<IntegrationsListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('IntegrationCreatePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(integrationService.createIntegration).mockResolvedValue(mockCreatedIntegration);
    vi.mocked(integrationService.testIntegrationConnection).mockResolvedValue(undefined);
  });

  describe('Scenario 1: Navigate to integration creation page', () => {
    it('renders_integration_creation_form_with_required_fields', () => {
      renderIntegrationCreatePage();

      expect(screen.getByText(/new integration/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/api key/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/base url/i)).toBeInTheDocument();
    });

    it('renders_provider_selection_dropdown', () => {
      renderIntegrationCreatePage();

      const providerSelect = screen.getByDisplayValue('Jira');
      expect(providerSelect).toBeInTheDocument();
    });

    it('renders_save_and_cancel_buttons', () => {
      renderIntegrationCreatePage();

      expect(screen.getByRole('button', { name: /save connection/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    });

    it('renders_back_to_integrations_link', () => {
      renderIntegrationCreatePage();

      expect(screen.getByText(/back to integrations/i)).toBeInTheDocument();
    });
  });

  describe('Scenario 2: Successfully create an integration', () => {
    it('creates_integration_and_navigates_to_list_on_save', async () => {
      renderIntegrationCreatePage();

      await userEvent.type(screen.getByLabelText(/display name/i), 'My Jira');
      await userEvent.type(screen.getByLabelText(/base url/i), 'https://test.atlassian.net');
      await userEvent.type(screen.getByLabelText(/api key/i), 'secret-key-123');

      const filterInput = screen.getByLabelText(/jql query/i);
      await userEvent.type(filterInput, 'project = "TEST"');

      await userEvent.click(screen.getByRole('button', { name: /save connection/i }));

      await waitFor(() => {
        expect(integrationService.createIntegration).toHaveBeenCalledWith(
          expect.objectContaining({
            workspaceId: 'ws-test',
            name: 'My Jira',
            provider: 'jira',
            url: 'https://test.atlassian.net',
            apiKey: 'secret-key-123',
          })
        );
      });

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations');
      });
    });
  });

  describe('Scenario 3: Cancel integration creation', () => {
    it('navigates_to_integrations_list_on_cancel_without_creating', async () => {
      renderIntegrationCreatePage();

      await userEvent.click(screen.getByRole('button', { name: /cancel/i }));

      expect(integrationService.createIntegration).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations');
      });
    });

    it('navigates_to_integrations_list_via_back_link', async () => {
      renderIntegrationCreatePage();

      await userEvent.click(screen.getByText(/back to integrations/i));

      expect(integrationService.createIntegration).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations');
      });
    });
  });

  describe('Scenario 4: Validation error on credentials', () => {
    it('shows_validation_error_when_api_key_is_empty', async () => {
      renderIntegrationCreatePage();

      await userEvent.type(screen.getByLabelText(/display name/i), 'My Jira');
      await userEvent.type(screen.getByLabelText(/base url/i), 'https://test.atlassian.net');

      const filterInput = screen.getByLabelText(/jql query/i);
      await userEvent.type(filterInput, 'project = "TEST"');

      await userEvent.click(screen.getByRole('button', { name: /save connection/i }));

      expect(integrationService.createIntegration).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/api key is required/i)).toBeInTheDocument();
      });

      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations/new');
    });

    it('shows_validation_error_when_name_is_empty', async () => {
      renderIntegrationCreatePage();

      await userEvent.type(screen.getByLabelText(/base url/i), 'https://test.atlassian.net');
      await userEvent.type(screen.getByLabelText(/api key/i), 'secret-key');

      const filterInput = screen.getByLabelText(/jql query/i);
      await userEvent.type(filterInput, 'project = "TEST"');

      await userEvent.click(screen.getByRole('button', { name: /save connection/i }));

      expect(integrationService.createIntegration).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/integration name is required/i)).toBeInTheDocument();
      });
    });

    it('shows_validation_error_when_name_is_too_short', async () => {
      renderIntegrationCreatePage();

      await userEvent.type(screen.getByLabelText(/display name/i), 'A');
      await userEvent.type(screen.getByLabelText(/base url/i), 'https://test.atlassian.net');
      await userEvent.type(screen.getByLabelText(/api key/i), 'secret-key');

      const filterInput = screen.getByLabelText(/jql query/i);
      await userEvent.type(filterInput, 'project = "TEST"');

      await userEvent.click(screen.getByRole('button', { name: /save connection/i }));

      expect(integrationService.createIntegration).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/between 2 and 100 characters/i)).toBeInTheDocument();
      });
    });

    it('shows_validation_error_when_url_is_empty', async () => {
      renderIntegrationCreatePage();

      await userEvent.type(screen.getByLabelText(/display name/i), 'My Jira');
      await userEvent.type(screen.getByLabelText(/api key/i), 'secret-key');

      const filterInput = screen.getByLabelText(/jql query/i);
      await userEvent.type(filterInput, 'project = "TEST"');

      await userEvent.click(screen.getByRole('button', { name: /save connection/i }));

      expect(integrationService.createIntegration).not.toHaveBeenCalled();

      await waitFor(() => {
        expect(screen.getByText(/base url is required/i)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 5: Direct URL access', () => {
    it('renders_form_on_direct_navigation_to_integrations_new', () => {
      renderIntegrationCreatePage();

      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations/new');
      expect(screen.getByText(/new integration/i)).toBeInTheDocument();
    });
  });

  describe('Edge cases', () => {
    it('shows_error_toast_when_api_returns_error', async () => {
      vi.mocked(integrationService.createIntegration).mockRejectedValue(new Error('Integration name already exists'));

      renderIntegrationCreatePage();

      await userEvent.type(screen.getByLabelText(/display name/i), 'My Jira');
      await userEvent.type(screen.getByLabelText(/base url/i), 'https://test.atlassian.net');
      await userEvent.type(screen.getByLabelText(/api key/i), 'secret-key');

      const filterInput = screen.getByLabelText(/jql query/i);
      await userEvent.type(filterInput, 'project = "TEST"');

      await userEvent.click(screen.getByRole('button', { name: /save connection/i }));

      await waitFor(() => {
        expect(screen.getByText(/integration name already exists/i)).toBeInTheDocument();
      });

      expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations/new');
    });

    it('disables_form_fields_while_saving', async () => {
      vi.mocked(integrationService.createIntegration).mockImplementation(
        () => new Promise(() => {})
      );

      renderIntegrationCreatePage();

      await userEvent.type(screen.getByLabelText(/display name/i), 'My Jira');
      await userEvent.type(screen.getByLabelText(/base url/i), 'https://test.atlassian.net');
      await userEvent.type(screen.getByLabelText(/api key/i), 'secret-key');

      const filterInput = screen.getByLabelText(/jql query/i);
      await userEvent.type(filterInput, 'project = "TEST"');

      await userEvent.click(screen.getByRole('button', { name: /save connection/i }));

      await waitFor(() => {
        expect(screen.getByText(/saving/i)).toBeInTheDocument();
      });
    });

    it('preserves_form_data_after_api_error', async () => {
      vi.mocked(integrationService.createIntegration).mockRejectedValue(new Error('Server error'));

      renderIntegrationCreatePage();

      await userEvent.type(screen.getByLabelText(/display name/i), 'My Jira');
      await userEvent.type(screen.getByLabelText(/base url/i), 'https://test.atlassian.net');
      await userEvent.type(screen.getByLabelText(/api key/i), 'secret-key');

      const filterInput = screen.getByLabelText(/jql query/i);
      await userEvent.type(filterInput, 'project = "TEST"');

      await userEvent.click(screen.getByRole('button', { name: /save connection/i }));

      await waitFor(() => {
        expect(screen.getByText(/server error/i)).toBeInTheDocument();
      });

      expect(screen.getByLabelText(/display name/i)).toHaveValue('My Jira');
      expect(screen.getByLabelText(/base url/i)).toHaveValue('https://test.atlassian.net');
    });
  });
});
