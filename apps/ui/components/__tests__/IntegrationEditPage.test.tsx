import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import IntegrationEditPage from '../pages/IntegrationEditPage';
import * as integrationService from '../../services/integrationService';
import { IntegrationType } from '../../types';

vi.mock('../../services/integrationService', () => ({
  getIntegrations: vi.fn(),
  updateIntegration: vi.fn(),
  createIntegration: vi.fn(),
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
  username: 'user@example.com',
  filterQuery: 'project = PROD',
  vectorize: false,
};

const LocationDisplay: React.FC = () => {
  const location = useLocation();
  return <div data-testid="location-display">{location.pathname}</div>;
};

const IntegrationsListPlaceholder: React.FC = () => <div data-testid="integrations-list">Integrations List</div>;

const renderEditPage = (integrationId = 'int-1') => {
  return render(
    <MemoryRouter initialEntries={[`/workspaces/ws-test/integrations/${integrationId}/edit`]}>
      <Routes>
        <Route path="/workspaces/:workspaceId/integrations/:integrationId/edit" element={<IntegrationEditPage />} />
        <Route path="/workspaces/:workspaceId/integrations" element={<IntegrationsListPlaceholder />} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>
  );
};

describe('IntegrationEditPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Scenario 1: Navigate to integration edit page', () => {
    beforeEach(() => {
      vi.mocked(integrationService.getIntegrations).mockResolvedValue([mockIntegration]);
    });

    it('renders_loading_state_initially', () => {
      renderEditPage();
      expect(screen.getByText(/back to integrations/i)).toBeInTheDocument();
    });

    it('renders_edit_integration_heading_after_load', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(screen.getByText(/edit integration/i)).toBeInTheDocument();
      });
    });

    it('renders_form_with_current_integration_name', async () => {
      renderEditPage();
      await waitFor(() => {
        const nameInput = screen.getByLabelText(/display name/i);
        expect(nameInput).toHaveValue('Jira Production');
      });
    });

    it('renders_provider_as_read_only', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(screen.getByText(/jira/i)).toBeInTheDocument();
        expect(screen.getByText(/provider cannot be changed/i)).toBeInTheDocument();
      });
    });

    it('renders_base_url_with_current_value', async () => {
      renderEditPage();
      await waitFor(() => {
        const urlInput = screen.getByLabelText(/base url/i);
        expect(urlInput).toHaveValue('https://company.atlassian.net');
      });
    });

    it('renders_api_key_as_masked', async () => {
      renderEditPage();
      await waitFor(() => {
        const apiKeyInput = screen.getByLabelText(/api key/i);
        expect(apiKeyInput).toHaveValue('••••••••••••');
      });
    });

    it('renders_api_key_helper_text', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(screen.getByText(/leave unchanged to keep the existing key/i)).toBeInTheDocument();
      });
    });

    it('renders_filter_query_with_current_value', async () => {
      renderEditPage();
      await waitFor(() => {
        const filterInput = screen.getByLabelText(/jql query/i);
        expect(filterInput).toHaveValue('project = PROD');
      });
    });

    it('renders_save_and_cancel_buttons', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /save/i })).toBeInTheDocument();
        expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
      });
    });

    it('renders_back_to_integrations_link', async () => {
      renderEditPage();
      await waitFor(() => {
        expect(screen.getByText(/back to integrations/i)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 4: Integration not found', () => {
    beforeEach(() => {
      vi.mocked(integrationService.getIntegrations).mockResolvedValue([]);
    });

    it('renders_not_found_error_state_when_integration_missing', async () => {
      renderEditPage('nonexistent-id');
      await waitFor(() => {
        expect(screen.getByText(/integration not found/i)).toBeInTheDocument();
      });
    });

    it('renders_return_to_integrations_link_on_not_found', async () => {
      renderEditPage('nonexistent-id');
      await waitFor(() => {
        expect(screen.getByText(/return to integrations/i)).toBeInTheDocument();
      });
    });

    it('renders_descriptive_message_on_not_found', async () => {
      renderEditPage('nonexistent-id');
      await waitFor(() => {
        expect(screen.getByText(/doesn't exist or has been removed/i)).toBeInTheDocument();
      });
    });
  });

  describe('Scenario 2: Successfully update an integration', () => {
    beforeEach(() => {
      vi.mocked(integrationService.getIntegrations).mockResolvedValue([mockIntegration]);
      vi.mocked(integrationService.updateIntegration).mockResolvedValue({
        ...mockIntegration,
        name: 'Jira Production Updated',
      });
    });

    it('updates_integration_and_navigates_to_list_on_save', async () => {
      const user = (await import('@testing-library/user-event')).default;
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toHaveValue('Jira Production');
      });

      const nameInput = screen.getByLabelText(/display name/i);
      await user.clear(nameInput);
      await user.type(nameInput, 'Jira Production Updated');

      await user.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(integrationService.updateIntegration).toHaveBeenCalledWith(
          'int-1',
          expect.objectContaining({ name: 'Jira Production Updated' })
        );
      });

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations');
      });
    });

    it('sends_masked_api_key_sentinel_when_key_unchanged', async () => {
      const user = (await import('@testing-library/user-event')).default;
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toHaveValue('Jira Production');
      });

      await user.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(integrationService.updateIntegration).toHaveBeenCalledWith(
          'int-1',
          expect.objectContaining({ apiKey: '••••••••••••' })
        );
      });
    });
  });

  describe('Save error handling', () => {
    beforeEach(() => {
      vi.mocked(integrationService.getIntegrations).mockResolvedValue([mockIntegration]);
    });

    it('displays_error_toast_and_preserves_form_on_save_failure', async () => {
      vi.mocked(integrationService.updateIntegration).mockRejectedValue(new Error('Failed to update integration'));
      const user = (await import('@testing-library/user-event')).default;
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toHaveValue('Jira Production');
      });

      await user.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(screen.getByText(/failed to update integration/i)).toBeInTheDocument();
      });

      expect(screen.getByLabelText(/display name/i)).toHaveValue('Jira Production');
    });

    it('displays_duplicate_name_error_on_409_conflict', async () => {
      vi.mocked(integrationService.updateIntegration).mockRejectedValue(new Error('Integration name already exists'));
      const user = (await import('@testing-library/user-event')).default;
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toHaveValue('Jira Production');
      });

      await user.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(screen.getByText(/integration name already exists/i)).toBeInTheDocument();
      });
    });

    it('stays_on_edit_page_when_save_fails', async () => {
      vi.mocked(integrationService.updateIntegration).mockRejectedValue(new Error('Server error'));
      const user = (await import('@testing-library/user-event')).default;
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toHaveValue('Jira Production');
      });

      await user.click(screen.getByRole('button', { name: /save/i }));

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations/int-1/edit');
      });
    });
  });

  describe('Scenario 3: Cancel editing', () => {
    beforeEach(() => {
      vi.mocked(integrationService.getIntegrations).mockResolvedValue([mockIntegration]);
    });

    it('navigates_to_integrations_list_on_cancel_without_saving', async () => {
      const user = (await import('@testing-library/user-event')).default;
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByLabelText(/display name/i)).toHaveValue('Jira Production');
      });

      await user.click(screen.getByRole('button', { name: /cancel/i }));

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations');
      });

      expect(integrationService.updateIntegration).not.toHaveBeenCalled();
    });

    it('navigates_to_integrations_list_via_back_link', async () => {
      const user = (await import('@testing-library/user-event')).default;
      renderEditPage();

      await waitFor(() => {
        expect(screen.getByText(/back to integrations/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/back to integrations/i));

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations');
      });
    });
  });

  describe('Scenario 4: Not found navigation', () => {
    beforeEach(() => {
      vi.mocked(integrationService.getIntegrations).mockResolvedValue([]);
    });

    it('navigates_to_integrations_list_when_return_button_clicked', async () => {
      const user = (await import('@testing-library/user-event')).default;
      renderEditPage('nonexistent-id');

      await waitFor(() => {
        expect(screen.getByText(/return to integrations/i)).toBeInTheDocument();
      });

      await user.click(screen.getByText(/return to integrations/i));

      await waitFor(() => {
        expect(screen.getByTestId('location-display').textContent).toBe('/workspaces/ws-test/integrations');
      });
    });

    it('shows_not_found_when_api_returns_empty_list', async () => {
      renderEditPage('int-1');

      await waitFor(() => {
        expect(screen.getByText(/integration not found/i)).toBeInTheDocument();
      });
    });
  });
});
