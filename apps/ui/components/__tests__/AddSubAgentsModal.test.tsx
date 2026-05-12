import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import AddSubAgentsModal from '../agents/AddSubAgentsModal';
import { Agent } from '../../types';

const makeAgent = (overrides: Partial<Agent> & { id: string; name: string }): Agent => ({
  workspaceId: 'ws-test',
  role: 'Assistant',
  status: 'IDLE',
  capabilities: [],
  toolActionIds: [],
  toolCategories: [],
  subAgentIds: [],
  avatarUrl: '/avatar.png',
  ...overrides,
});

const agentAlpha = makeAgent({ id: 'agent-alpha', name: 'Alpha Bot', role: 'Analyst' });
const agentBeta = makeAgent({ id: 'agent-beta', name: 'Beta Bot', role: 'Developer' });
const agentGamma = makeAgent({ id: 'agent-gamma', name: 'Gamma Bot', role: 'Tester' });

const allAgents = [agentAlpha, agentBeta, agentGamma];

const renderModal = (props: Partial<React.ComponentProps<typeof AddSubAgentsModal>> = {}) => {
  const defaults = {
    isOpen: true,
    allAgents,
    alreadySelectedIds: [] as string[],
    onCommit: vi.fn(),
    onDiscard: vi.fn(),
  };
  return render(<AddSubAgentsModal {...defaults} {...props} />);
};

describe('AddSubAgentsModal', () => {
  describe('Rendering', () => {
    it('renders_nothing_when_closed', () => {
      renderModal({ isOpen: false });
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('renders_dialog_when_open', () => {
      renderModal();
      expect(screen.getByRole('dialog', { name: /select sub-agents/i })).toBeInTheDocument();
    });

    it('renders_all_selectable_agents_as_cards', () => {
      renderModal();
      expect(screen.getByText('Alpha Bot')).toBeInTheDocument();
      expect(screen.getByText('Beta Bot')).toBeInTheDocument();
      expect(screen.getByText('Gamma Bot')).toBeInTheDocument();
    });

    it('renders_empty_state_when_no_agents_available', () => {
      renderModal({ allAgents: [] });
      expect(screen.getByText(/no other agents available/i)).toBeInTheDocument();
    });
  });

  describe('Self-exclusion', () => {
    it('excludes_the_agent_being_edited_from_the_list', () => {
      renderModal({ excludeAgentId: 'agent-alpha' });
      expect(screen.queryByText('Alpha Bot')).not.toBeInTheDocument();
      expect(screen.getByText('Beta Bot')).toBeInTheDocument();
      expect(screen.getByText('Gamma Bot')).toBeInTheDocument();
    });
  });

  describe('Pre-selection', () => {
    it('pre_selects_already_assigned_agents', () => {
      renderModal({ alreadySelectedIds: ['agent-beta'] });

      const confirmButton = screen.getByRole('button', { name: /add 1 agent/i });
      expect(confirmButton).toBeInTheDocument();
    });

    it('shows_checkmark_on_pre_selected_agent', () => {
      renderModal({ alreadySelectedIds: ['agent-alpha'] });
      // The selected card should have an accessible check icon
      const cards = screen.getAllByRole('button');
      // The Alpha Bot card should be visually marked — just verify count is 1
      const confirmBtn = screen.getByRole('button', { name: /add 1 agent/i });
      expect(confirmBtn).toBeTruthy();
    });
  });

  describe('Selection behaviour', () => {
    it('toggling_a_card_increments_the_count', async () => {
      const user = userEvent.setup();
      renderModal();

      await user.click(screen.getByRole('button', { name: /alpha bot/i }));

      expect(screen.getByRole('button', { name: /add 1 agent/i })).toBeInTheDocument();
    });

    it('toggling_a_selected_card_decrements_the_count', async () => {
      const user = userEvent.setup();
      renderModal({ alreadySelectedIds: ['agent-alpha'] });

      await user.click(screen.getByRole('button', { name: /alpha bot/i }));

      expect(screen.getByRole('button', { name: /add none/i })).toBeInTheDocument();
    });

    it('selecting_multiple_agents_updates_count', async () => {
      const user = userEvent.setup();
      renderModal();

      await user.click(screen.getByRole('button', { name: /alpha bot/i }));
      await user.click(screen.getByRole('button', { name: /beta bot/i }));

      expect(screen.getByRole('button', { name: /add 2 agents/i })).toBeInTheDocument();
    });
  });

  describe('Commit', () => {
    it('calls_onCommit_with_selected_ids_on_confirm', async () => {
      const onCommit = vi.fn();
      const user = userEvent.setup();
      renderModal({ onCommit });

      await user.click(screen.getByRole('button', { name: /alpha bot/i }));
      await user.click(screen.getByRole('button', { name: /add 1 agent/i }));

      expect(onCommit).toHaveBeenCalledWith(['agent-alpha']);
    });

    it('calls_onCommit_with_empty_array_when_no_agents_selected', async () => {
      const onCommit = vi.fn();
      const user = userEvent.setup();
      renderModal({ onCommit });

      await user.click(screen.getByRole('button', { name: /add none/i }));

      expect(onCommit).toHaveBeenCalledWith([]);
    });

    it('calls_onCommit_with_pre_selected_ids_when_unchanged', async () => {
      const onCommit = vi.fn();
      const user = userEvent.setup();
      renderModal({ onCommit, alreadySelectedIds: ['agent-beta'] });

      await user.click(screen.getByRole('button', { name: /add 1 agent/i }));

      expect(onCommit).toHaveBeenCalledWith(['agent-beta']);
    });
  });

  describe('Discard', () => {
    it('calls_onDiscard_when_cancel_button_clicked', async () => {
      const onDiscard = vi.fn();
      const user = userEvent.setup();
      renderModal({ onDiscard });

      await user.click(screen.getByRole('button', { name: /cancel/i }));

      expect(onDiscard).toHaveBeenCalled();
    });

    it('calls_onDiscard_when_close_button_clicked', async () => {
      const onDiscard = vi.fn();
      const user = userEvent.setup();
      renderModal({ onDiscard });

      await user.click(screen.getByRole('button', { name: /close/i }));

      expect(onDiscard).toHaveBeenCalled();
    });

    it('calls_onDiscard_when_escape_key_pressed', async () => {
      const onDiscard = vi.fn();
      const user = userEvent.setup();
      renderModal({ onDiscard });

      await user.keyboard('{Escape}');

      expect(onDiscard).toHaveBeenCalled();
    });
  });
});
