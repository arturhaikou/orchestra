import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import AgentToolSummaryCard, { AgentToolSummaryCardProps } from '../agents/AgentToolSummaryCard';

// ─── Shared test data ──────────────────────────────────────────────────────────

const defaultProps: AgentToolSummaryCardProps = {
  sourceId: 'native-tickets',
  sourceName: 'Ticket Tracker',
  selectedCount: 2,
  totalCount: 4,
  onRemove: vi.fn(),
  onOpen: vi.fn(),
};

function renderCard(overrides: Partial<AgentToolSummaryCardProps> = {}) {
  const onRemove = vi.fn();
  const onOpen = vi.fn();
  render(
    <AgentToolSummaryCard
      {...defaultProps}
      onRemove={onRemove}
      onOpen={onOpen}
      {...overrides}
    />
  );
  return { onRemove, onOpen };
}

// ─── Tests ─────────────────────────────────────────────────────────────────────

describe('AgentToolSummaryCard', () => {
  beforeEach(() => vi.clearAllMocks());

  describe('Card anatomy', () => {
    it('renders_the_source_name', () => {
      renderCard();
      expect(screen.getByText('Ticket Tracker')).toBeInTheDocument();
    });

    it('renders_the_X_of_Y_selection_count_badge', () => {
      renderCard({ selectedCount: 2, totalCount: 4 });
      expect(screen.getByTestId('selection-count')).toHaveTextContent('2');
      expect(screen.getByTestId('selection-count')).toHaveTextContent('4');
    });

    it('shows_connection_status_badge_for_mcp_cards', () => {
      renderCard({ connectionStatus: 'Connected' });
      expect(screen.getByText(/connected/i)).toBeInTheDocument();
    });

    it('does_not_show_connection_status_badge_for_native_cards', () => {
      renderCard({ connectionStatus: undefined });
      expect(screen.queryByText(/connected/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/failed/i)).not.toBeInTheDocument();
    });

    it('shows_failed_status_badge_when_connectionStatus_is_ConnectionFailed', () => {
      renderCard({ connectionStatus: 'ConnectionFailed' });
      expect(screen.getByText(/failed/i)).toBeInTheDocument();
    });
  });

  describe('Interactions', () => {
    it('calls_onOpen_when_card_body_is_clicked', async () => {
      const user = userEvent.setup();
      const { onOpen } = renderCard();
      await user.click(screen.getByTestId('tool-summary-card'));
      expect(onOpen).toHaveBeenCalledOnce();
    });

    it('calls_onRemove_when_remove_button_is_clicked', async () => {
      const user = userEvent.setup();
      const { onRemove } = renderCard();
      await user.click(screen.getByTestId('remove-source-button'));
      expect(onRemove).toHaveBeenCalledOnce();
    });

    it('does_not_call_onOpen_when_remove_button_is_clicked', async () => {
      const user = userEvent.setup();
      const { onOpen } = renderCard();
      await user.click(screen.getByTestId('remove-source-button'));
      expect(onOpen).not.toHaveBeenCalled();
    });
  });

  describe('Visual state on removal', () => {
    it('applies_fade_out_class_immediately_after_remove_click', async () => {
      const user = userEvent.setup();
      renderCard();
      const card = screen.getByTestId('tool-summary-card');
      await user.click(screen.getByTestId('remove-source-button'));
      expect(card.className).toMatch(/opacity-0|removing/);
    });
  });

  describe('FR-006 visual enhancements', () => {
    it('renders_a_lucide_icon_for_a_registered_source_id', () => {
      renderCard({ sourceId: 'cat-tracker' });
      const card = screen.getByTestId('tool-summary-card');
      const svgIcon = card.querySelector('svg');
      expect(svgIcon).toBeInTheDocument();
    });

    it('renders_a_fallback_icon_for_an_unregistered_source_id', () => {
      renderCard({ sourceId: 'unknown-custom-source' });
      const card = screen.getByTestId('tool-summary-card');
      const svgIcon = card.querySelector('svg');
      expect(svgIcon).toBeInTheDocument();
    });

    it('card_root_has_hover_tailwind_variant_class', () => {
      renderCard();
      const card = screen.getByTestId('tool-summary-card');
      expect(card.className).toMatch(/hover:/);
    });

    it('card_root_has_active_tailwind_variant_class', () => {
      renderCard();
      const card = screen.getByTestId('tool-summary-card');
      expect(card.className).toMatch(/active:/);
    });
  });
});
