import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import McpTransportForm from '../mcp/McpTransportForm';

vi.mock('../../services/integrationService', () => ({
  createHttpMcpIntegration: vi.fn(),
  createStdioMcpIntegration: vi.fn(),
  getIntegrations: vi.fn(),
}));

const mockOnSuccess = vi.fn();
const mockOnCancel = vi.fn();

describe('McpTransportForm — transport switch (Scenario 3)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('switching_transport_to_stdio_clears_http_fields', async () => {
    const user = userEvent.setup();
    render(
      <McpTransportForm
        workspaceId="ws-test"
        onSuccess={mockOnSuccess}
        onCancel={mockOnCancel}
      />
    );

    await user.type(screen.getByLabelText(/endpoint url/i), 'https://api.example.com');
    await user.type(screen.getByLabelText(/api key/i), 'secret123');

    await user.click(screen.getByRole('button', { name: /stdio/i }));

    expect(screen.queryByLabelText(/endpoint url/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/api key/i)).not.toBeInTheDocument();
  });

  it('switching_transport_to_stdio_shows_stdio_fields', async () => {
    const user = userEvent.setup();
    render(
      <McpTransportForm
        workspaceId="ws-test"
        onSuccess={mockOnSuccess}
        onCancel={mockOnCancel}
      />
    );

    await user.click(screen.getByRole('button', { name: /stdio/i }));

    expect(screen.getByLabelText(/command/i)).toBeInTheDocument();
  });
});
