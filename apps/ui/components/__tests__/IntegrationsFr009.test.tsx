import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import Integrations from '../Integrations';
import * as integrationService from '../../services/integrationService';

vi.mock('../../services/integrationService', () => ({
  getIntegrations: vi.fn(),
  deleteIntegration: vi.fn(),
  syncIntegrationTools: vi.fn(),
  getDeletionImpact: vi.fn(),
}));

const renderIntegrations = () =>
  render(
    <MemoryRouter initialEntries={['/workspaces/ws-test/integrations']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/integrations" element={<Integrations />} />
      </Routes>
    </MemoryRouter>
  );

describe('Integrations — FR-009 Design Tools removal (Scenario 5)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(integrationService.getIntegrations).mockResolvedValue([]);
  });

  it('does_not_render_design_tools_category_heading', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.queryByText('Design Tools')).not.toBeInTheDocument();
    });
  });

  it('add_mcp_button_label_is_add_mcp_server', async () => {
    renderIntegrations();

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /add mcp server/i })).toBeInTheDocument();
    });
  });
});
