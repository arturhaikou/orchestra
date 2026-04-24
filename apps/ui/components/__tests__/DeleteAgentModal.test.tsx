import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import '@testing-library/jest-dom';
import AgentsList from '../AgentsList';

vi.mock('../../services/agentService', () => ({
  getAgents: vi.fn().mockResolvedValue([
    {
      id: 'agent-1',
      name: 'Test Agent',
      role: 'Tester',
      capabilities: [],
      toolActionIds: [],
      customInstructions: '',
      projectPrinciples: '',
      model: null,
      isBuiltIn: false,
    },
  ]),
  updateAgent: vi.fn(),
  deleteAgent: vi.fn(),
}));

vi.mock('../../services/toolService', () => ({
  getTools: vi.fn().mockResolvedValue([]),
}));

vi.mock('../../services/workspaceService', () => ({
  fetchWorkspaceModels: vi.fn().mockResolvedValue([]),
  deleteWorkspace: vi.fn(),
}));

const { deleteAgent } = await import('../../services/agentService');

function renderAgentsList() {
  return render(
    <MemoryRouter initialEntries={['/workspaces/ws-1/agents']}>
      <Routes>
        <Route path="/workspaces/:workspaceId/agents" element={<AgentsList />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('Delete Agent Modal - Save and Close Behavior', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should close modal and remove agent from list on successful delete', async () => {
    const user = userEvent.setup();
    (deleteAgent as ReturnType<typeof vi.fn>).mockResolvedValueOnce(undefined);
    renderAgentsList();

    await waitFor(() => {
      expect(screen.getByText('Test Agent')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    await waitFor(() => {
      expect(screen.getByText('Decommission Agent?')).toBeInTheDocument();
    });
    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(screen.queryByText('Decommission Agent?')).not.toBeInTheDocument();
      expect(screen.queryByText('Test Agent')).not.toBeInTheDocument();
    });
  });

  it('should display success toast after successful deletion', async () => {
    const user = userEvent.setup();
    (deleteAgent as ReturnType<typeof vi.fn>).mockResolvedValueOnce(undefined);
    renderAgentsList();

    await waitFor(() => {
      expect(screen.getByText('Test Agent')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    await waitFor(() => {
      expect(screen.getByText('Decommission Agent?')).toBeInTheDocument();
    });
    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText(/has been removed/i)).toBeInTheDocument();
    });
  });

  it('should keep modal open and show error on API failure', async () => {
    const user = userEvent.setup();
    (deleteAgent as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('Agent not found. It may have already been deleted.'));
    renderAgentsList();

    await waitFor(() => {
      expect(screen.getByText('Test Agent')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    await waitFor(() => {
      expect(screen.getByText('Decommission Agent?')).toBeInTheDocument();
    });
    const confirmButton = screen.getByRole('button', { name: /confirm/i });
    await user.click(confirmButton);

    await waitFor(() => {
      expect(screen.getByText('Decommission Agent?')).toBeInTheDocument();
      expect(screen.getByText(/Agent not found/i)).toBeInTheDocument();
    });

    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('should allow retry after error and close on subsequent success', async () => {
    const user = userEvent.setup();
    (deleteAgent as ReturnType<typeof vi.fn>)
      .mockRejectedValueOnce(new Error('Temporary error'))
      .mockResolvedValueOnce(undefined);
    renderAgentsList();

    await waitFor(() => {
      expect(screen.getByText('Test Agent')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    await waitFor(() => {
      expect(screen.getByText('Decommission Agent?')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /confirm/i }));
    await waitFor(() => {
      expect(screen.getByText(/Temporary error/i)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /retry/i }));
    await waitFor(() => {
      expect(screen.queryByText('Decommission Agent?')).not.toBeInTheDocument();
    });
  });

  it('should clear error and close modal on cancel after error', async () => {
    const user = userEvent.setup();
    (deleteAgent as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('Some error'));
    renderAgentsList();

    await waitFor(() => {
      expect(screen.getByText('Test Agent')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    await waitFor(() => {
      expect(screen.getByText('Decommission Agent?')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /confirm/i }));
    await waitFor(() => {
      expect(screen.getByText(/Some error/i)).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: /cancel/i }));
    await waitFor(() => {
      expect(screen.queryByText('Decommission Agent?')).not.toBeInTheDocument();
    });
  });
});
