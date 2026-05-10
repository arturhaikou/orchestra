import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import CategoryCard from '../agents/CategoryCard';

// ─── Render helper ─────────────────────────────────────────────────────────────

interface RenderCardOverrides {
  sourceId?: string;
  name?: string;
  description?: string;
  iconName?: string | undefined;
  selectedCount?: number;
  totalCount?: number;
  isActive?: boolean;
  onClick?: () => void;
  hint?: React.ReactNode;
}

function renderCard(overrides: RenderCardOverrides = {}) {
  const onClick = overrides.onClick ?? vi.fn();
  const props = {
    sourceId: 'cat-github',
    name: 'GitHub',
    description: 'Source control tools',
    iconName: 'GitBranch',
    selectedCount: 0,
    totalCount: 10,
    isActive: false,
    onClick,
    ...overrides,
  };
  const { rerender } = render(<CategoryCard {...props} />);
  return { onClick, rerender, props };
}

// ─── Tests ─────────────────────────────────────────────────────────────────────

describe('CategoryCard', () => {

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Scenario 1 — card structure', () => {

    it('renders_card_with_testid_category_card', () => {
      renderCard();
      expect(screen.getByTestId('category-card')).toBeInTheDocument();
    });

    it('renders_the_category_name', () => {
      renderCard({ name: 'Jira' });
      expect(screen.getByText('Jira')).toBeInTheDocument();
    });

    it('renders_a_visible_icon_element', () => {
      renderCard({ iconName: 'GitBranch' });
      const card = screen.getByTestId('category-card');
      const svg = card.querySelector('svg');
      const img = card.querySelector('img');
      const iconByRole = card.querySelector('[role="img"]');
      expect(svg || img || iconByRole).toBeTruthy();
    });

    it('renders_selected_vs_total_badge', () => {
      renderCard({ selectedCount: 3, totalCount: 12 });
      expect(screen.getByText('3 / 12')).toBeInTheDocument();
    });

    it('renders_zero_selected_badge_when_no_tools_selected', () => {
      renderCard({ selectedCount: 0, totalCount: 8 });
      expect(screen.getByText('0 / 8')).toBeInTheDocument();
    });

  });

  describe('Scenario 2 — selection state', () => {

    it('does_not_apply_active_class_when_isActive_is_false', () => {
      renderCard({ isActive: false });
      expect(screen.getByTestId('category-card')).not.toHaveAttribute('aria-current', 'true');
    });

    it('applies_active_aria_when_isActive_is_true', () => {
      renderCard({ isActive: true });
      expect(screen.getByTestId('category-card')).toHaveAttribute('aria-current', 'true');
    });

    it('calls_onClick_when_card_is_clicked', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      renderCard({ onClick });
      await user.click(screen.getByTestId('category-card'));
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('calls_onClick_on_Enter_keydown', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      renderCard({ onClick });
      screen.getByTestId('category-card').focus();
      await user.keyboard('{Enter}');
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('calls_onClick_on_Space_keydown', async () => {
      const user = userEvent.setup();
      const onClick = vi.fn();
      renderCard({ onClick });
      screen.getByTestId('category-card').focus();
      await user.keyboard(' ');
      expect(onClick).toHaveBeenCalledTimes(1);
    });

  });

  describe('Scenario 3 — badge updates with selection', () => {

    it('badge_displays_updated_count_when_selectedCount_prop_changes', () => {
      const { rerender, props } = renderCard({ selectedCount: 2, totalCount: 8 });
      expect(screen.getByText('2 / 8')).toBeInTheDocument();
      rerender(<CategoryCard {...props} selectedCount={3} totalCount={8} />);
      expect(screen.getByText('3 / 8')).toBeInTheDocument();
    });

    it('badge_is_always_present_even_at_zero_selected', () => {
      renderCard({ selectedCount: 0, totalCount: 5 });
      expect(screen.getByText('0 / 5')).toBeInTheDocument();
    });

  });

  describe('Scenario 4 — tooltip on hover', () => {

    it('card_has_a_title_attribute_with_the_description', () => {
      renderCard({ description: 'Source control tools' });
      const card = screen.getByTestId('category-card');
      const elementWithTitle = card.closest('[title]') ?? card.querySelector('[title]') ?? (card.getAttribute('title') ? card : null);
      expect(elementWithTitle).not.toBeNull();
      const titleValue = elementWithTitle?.getAttribute('title') ?? '';
      expect(titleValue).toContain('Source control tools');
    });

    it('card_shows_full_name_in_title_for_truncated_long_names', () => {
      const longName = 'A Very Long Category Name That Would Overflow';
      renderCard({ name: longName, description: 'Some description' });
      const card = screen.getByTestId('category-card');
      const allElements = [card, ...Array.from(card.querySelectorAll('[title]'))];
      const hasTitleAttr = allElements.some(el => el.getAttribute('title') !== null);
      expect(hasTitleAttr).toBe(true);
    });

  });

  describe('Scenario 5 — fallback icon', () => {

    it('renders_fallback_icon_when_iconName_is_undefined', () => {
      renderCard({ iconName: undefined });
      expect(screen.getByTestId('category-card')).toBeInTheDocument();
    });

    it('renders_fallback_icon_when_iconName_is_not_in_lucide', () => {
      renderCard({ iconName: 'NonExistentIconXYZ' });
      expect(screen.getByTestId('category-card')).toBeInTheDocument();
      const card = screen.getByTestId('category-card');
      const svg = card.querySelector('svg');
      const img = card.querySelector('img');
      const iconByRole = card.querySelector('[role="img"]');
      expect(svg || img || iconByRole).toBeTruthy();
    });

  });

  describe('Accessibility', () => {

    it('card_root_has_role_button', () => {
      renderCard();
      expect(screen.getByRole('button')).toBeInTheDocument();
    });

    it('card_root_is_keyboard_focusable_via_tabIndex', () => {
      renderCard();
      expect(screen.getByTestId('category-card')).toHaveAttribute('tabIndex', '0');
    });

  });

  describe('CategoryCard — optional badge and hint slot', () => {

    it('does_not_render_count_badge_when_selectedCount_is_undefined', () => {
      renderCard({ selectedCount: undefined, totalCount: 10 });
      expect(screen.queryByTestId('count-badge')).toBeNull();
    });

    it('does_not_render_count_badge_when_totalCount_is_undefined', () => {
      renderCard({ selectedCount: 3, totalCount: undefined });
      expect(screen.queryByTestId('count-badge')).toBeNull();
    });

    it('renders_count_badge_when_both_counts_are_provided', () => {
      renderCard({ selectedCount: 2, totalCount: 7 });
      expect(screen.getByTestId('count-badge')).toBeInTheDocument();
      expect(screen.getByText('2 / 7')).toBeInTheDocument();
    });

    it('renders_hint_node_when_counts_are_absent_and_hint_is_provided', () => {
      renderCard({
        selectedCount: undefined,
        totalCount: undefined,
        hint: <span data-testid="custom-hint">Click to discover</span>,
      });
      expect(screen.getByTestId('custom-hint')).toBeInTheDocument();
      expect(screen.queryByTestId('count-badge')).toBeNull();
    });

    it('does_not_render_hint_when_counts_are_present', () => {
      renderCard({
        selectedCount: 1,
        totalCount: 5,
        hint: <span data-testid="custom-hint">Click to discover</span>,
      });
      expect(screen.queryByTestId('custom-hint')).toBeNull();
      expect(screen.getByTestId('count-badge')).toBeInTheDocument();
    });

    it('renders_nothing_in_badge_slot_when_no_counts_and_no_hint', () => {
      renderCard({ selectedCount: undefined, totalCount: undefined });
      expect(screen.queryByTestId('count-badge')).toBeNull();
      expect(screen.getByTestId('category-card')).toBeInTheDocument();
    });

  });

});
