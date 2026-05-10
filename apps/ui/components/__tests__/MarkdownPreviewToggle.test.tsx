import React from 'react';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import MarkdownPreviewToggle from '../agents/MarkdownPreviewToggle';

// ─── Mock Setup ─────────────────────────────────────────────────────────────

vi.mock('../../utils/markdownRenderer', () => ({
  renderMarkdown: vi.fn((input: string) => `<p>${input}</p>`),
}));

// ─── Imports for mocking ────────────────────────────────────────────────────

import * as markdownRendererModule from '../../utils/markdownRenderer';
const mockRenderMarkdown = vi.mocked(markdownRendererModule.renderMarkdown);

// ─── Render Helper ──────────────────────────────────────────────────────────

interface RenderToggleOverrides {
  value?: string;
  onChange?: (value: string) => void;
  onFocus?: () => void;
  id?: string;
  rows?: number;
  placeholder?: string;
  disabled?: boolean;
}

function renderToggle(overrides: RenderToggleOverrides = {}) {
  const onChange = overrides.onChange ?? vi.fn();
  const onFocus = overrides.onFocus ?? vi.fn();
  const props = {
    value: 'some text',
    onChange,
    onFocus,
    ...overrides,
  };
  render(<MarkdownPreviewToggle {...props} />);
  return { onChange, onFocus, props };
}

// ─── Tests ──────────────────────────────────────────────────────────────────

describe('MarkdownPreviewToggle', () => {

  beforeEach(() => {
    vi.clearAllMocks();
    mockRenderMarkdown.mockImplementation((input: string) => `<p>${input}</p>`);
  });

  describe('Scenario 1 — Toggle control is visible', () => {

    it('renders_an_Edit_button_with_pencil_icon', () => {
      renderToggle();
      const editButtons = screen.queryAllByRole('button');
      const editButton = editButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('write') || 
        btn.textContent?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('write') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('pencil')
      );
      expect(editButton).toBeTruthy();
    });

    it('renders_a_Preview_button_with_eye_icon', () => {
      renderToggle();
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      expect(previewButton).toBeTruthy();
    });

  });

  describe('Scenario 2 — Edit mode is the default', () => {

    it('shows_the_textarea_in_edit_mode_by_default', () => {
      renderToggle({ value: 'Hello world' });
      const textarea = screen.getByRole('textbox');
      expect(textarea).toBeInTheDocument();
      expect(textarea).toHaveValue('Hello world');
    });

    it('does_not_show_the_preview_pane_by_default', () => {
      renderToggle({ value: 'Hello world' });
      const previewPane = screen.queryByTestId('preview-pane');
      expect(previewPane).not.toBeInTheDocument();
    });

    it('Edit_button_is_marked_as_active_by_default', () => {
      renderToggle({ value: 'some text' });
      const editButtons = screen.queryAllByRole('button');
      const editButton = editButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('write') || 
        btn.textContent?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('write') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('pencil')
      );
      if (editButton) {
        expect(editButton).toHaveClass(/bg-primary|text-primary|active/);
      }
    });

    it('Preview_button_is_not_marked_as_active_by_default', () => {
      renderToggle({ value: 'some text' });
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      if (previewButton) {
        expect(previewButton).not.toHaveClass(/bg-primary|text-primary|active/);
      }
    });

  });

  describe('Scenario 3 — Switching to Preview renders markdown', () => {

    it('hides_the_textarea_and_shows_the_preview_pane_when_Preview_is_clicked', async () => {
      const user = userEvent.setup();
      renderToggle({ value: '# Heading' });
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
      }
      
      const textarea = screen.queryByRole('textbox');
      expect(textarea).not.toBeInTheDocument();
      
      const previewPane = screen.queryByTestId('preview-pane');
      expect(previewPane).toBeInTheDocument();
    });

    it('calls_renderMarkdown_with_the_current_value_when_entering_Preview_mode', async () => {
      const user = userEvent.setup();
      renderToggle({ value: '**bold**' });
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
        expect(mockRenderMarkdown).toHaveBeenCalledWith('**bold**');
      }
    });

    it('renders_the_HTML_returned_by_renderMarkdown_in_the_preview_pane', async () => {
      const user = userEvent.setup();
      mockRenderMarkdown.mockReturnValue('<p><strong>bold</strong></p>');
      renderToggle({ value: '**bold**' });
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
        const previewPane = screen.queryByTestId('preview-pane');
        expect(previewPane?.innerHTML).toContain('<strong>bold</strong>');
      }
    });

    it('Preview_button_is_marked_as_active_after_switching_to_preview', async () => {
      const user = userEvent.setup();
      renderToggle({ value: 'text' });
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
        expect(previewButton).toHaveClass(/bg-primary|text-primary|active/);
      }
    });

    it('Edit_button_is_not_marked_as_active_after_switching_to_preview', async () => {
      const user = userEvent.setup();
      renderToggle({ value: 'text' });
      
      const buttons = screen.queryAllByRole('button');
      const previewButton = buttons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      const editButton = buttons.find(btn => 
        btn.textContent?.toLowerCase().includes('write') || 
        btn.textContent?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('write') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('pencil')
      );
      
      if (previewButton) {
        await user.click(previewButton);
        if (editButton) {
          expect(editButton).not.toHaveClass(/bg-primary|text-primary|active/);
        }
      }
    });

  });

  describe('Scenario 4 — Switching back to Edit preserves content', () => {

    it('returns_to_textarea_edit_mode_when_Edit_is_clicked_from_preview', async () => {
      const user = userEvent.setup();
      renderToggle({ value: '# Heading' });
      
      const buttons = screen.queryAllByRole('button');
      const previewButton = buttons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      const editButton = buttons.find(btn => 
        btn.textContent?.toLowerCase().includes('write') || 
        btn.textContent?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('write') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('pencil')
      );
      
      if (previewButton && editButton) {
        await user.click(previewButton);
        await user.click(editButton);
        
        const textarea = screen.getByRole('textbox');
        expect(textarea).toBeInTheDocument();
        expect(textarea).toHaveValue('# Heading');
      }
    });

    it('does_not_invoke_onChange_when_switching_between_modes', async () => {
      const user = userEvent.setup();
      const mockOnChange = vi.fn();
      renderToggle({ value: 'some text', onChange: mockOnChange });
      
      const buttons = screen.queryAllByRole('button');
      const previewButton = buttons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      const editButton = buttons.find(btn => 
        btn.textContent?.toLowerCase().includes('write') || 
        btn.textContent?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('write') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('pencil')
      );
      
      if (previewButton && editButton) {
        await user.click(previewButton);
        await user.click(editButton);
        expect(mockOnChange).not.toHaveBeenCalled();
      }
    });

  });

  describe('Scenario 5 — Empty state in Preview mode', () => {

    it('shows_the_empty_state_placeholder_when_value_is_empty_and_Preview_is_clicked', async () => {
      const user = userEvent.setup();
      renderToggle({ value: '' });
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
        expect(screen.getByText('Nothing to preview yet.')).toBeInTheDocument();
      }
    });

    it('does_NOT_call_renderMarkdown_when_value_is_empty_string_on_preview', async () => {
      const user = userEvent.setup();
      renderToggle({ value: '' });
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
        expect(mockRenderMarkdown).not.toHaveBeenCalled();
      }
    });

  });

  describe('Scenario 6 — Component is navigation-passive', () => {

    it('does_not_register_beforeunload_handler_on_component_mount', () => {
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener');
      renderToggle();
      
      const beforeUnloadCalls = addEventListenerSpy.mock.calls.filter(
        call => call[0] === 'beforeunload'
      );
      expect(beforeUnloadCalls.length).toBe(0);
      
      addEventListenerSpy.mockRestore();
    });

    it('does_not_register_beforeunload_handler_when_switching_to_preview_mode', async () => {
      const user = userEvent.setup();
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener');
      renderToggle({ value: 'text' });
      
      vi.clearAllMocks();
      mockRenderMarkdown.mockClear();
      addEventListenerSpy.mockClear();
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
      }
      
      const beforeUnloadCalls = addEventListenerSpy.mock.calls.filter(
        call => call[0] === 'beforeunload'
      );
      expect(beforeUnloadCalls.length).toBe(0);
      
      addEventListenerSpy.mockRestore();
    });

  });

  describe('Additional: prop forwarding', () => {

    it('forwards_the_id_prop_to_the_textarea', () => {
      renderToggle({ id: 'custom-instructions' });
      const textarea = screen.getByRole('textbox');
      expect(textarea).toHaveAttribute('id', 'custom-instructions');
    });

    it('forwards_the_placeholder_prop_to_the_textarea', () => {
      renderToggle({ placeholder: 'Enter instructions...' });
      const textarea = screen.getByRole('textbox');
      expect(textarea).toHaveAttribute('placeholder', 'Enter instructions...');
    });

    it('invokes_onChange_with_the_new_value_when_the_user_types_in_the_textarea', async () => {
      const user = userEvent.setup();
      const mockOnChange = vi.fn();
      renderToggle({ value: '', onChange: mockOnChange });
      
      const textarea = screen.getByRole('textbox');
      await user.type(textarea, 'Hello');
      
      expect(mockOnChange).toHaveBeenCalled();
      const allCalls = mockOnChange.mock.calls.map(call => call[0]);
      expect(allCalls.join('')).toBe('Hello');
    });

    it('invokes_onFocus_when_the_textarea_receives_focus', async () => {
      const user = userEvent.setup();
      const mockOnFocus = vi.fn();
      renderToggle({ value: '', onFocus: mockOnFocus });
      
      const textarea = screen.getByRole('textbox');
      await user.click(textarea);
      
      expect(mockOnFocus).toHaveBeenCalledTimes(1);
    });

    it('respects_the_disabled_prop', () => {
      renderToggle({ disabled: true });
      const textarea = screen.getByRole('textbox') as HTMLTextAreaElement;
      expect(textarea.disabled).toBe(true);
    });

    it('respects_the_rows_prop', () => {
      renderToggle({ rows: 10 });
      const textarea = screen.getByRole('textbox') as HTMLTextAreaElement;
      expect(textarea.rows).toBe(10);
    });

  });

  describe('Edge cases', () => {

    it('handles_null_onChange_gracefully', () => {
      expect(() => {
        renderToggle({ onChange: undefined as any });
      }).not.toThrow();
    });

    it('handles_switching_modes_rapidly', async () => {
      const user = userEvent.setup();
      renderToggle({ value: 'test content' });
      
      const buttons = screen.queryAllByRole('button');
      const previewButton = buttons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      const editButton = buttons.find(btn => 
        btn.textContent?.toLowerCase().includes('write') || 
        btn.textContent?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('edit') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('write') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('pencil')
      );
      
      if (previewButton && editButton) {
        await user.click(previewButton);
        await user.click(editButton);
        await user.click(previewButton);
        
        const previewPane = screen.queryByTestId('preview-pane');
        expect(previewPane).toBeInTheDocument();
      }
    });

    it('handles_very_long_markdown_content', async () => {
      const user = userEvent.setup();
      const longContent = '# ' + 'A'.repeat(1000);
      renderToggle({ value: longContent });
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
        expect(mockRenderMarkdown).toHaveBeenCalledWith(longContent);
      }
    });

    it('handles_special_characters_in_markdown_content', async () => {
      const user = userEvent.setup();
      const specialContent = '# <div>XSS</div> **bold** `code`';
      renderToggle({ value: specialContent });
      
      const previewButtons = screen.queryAllByRole('button');
      const previewButton = previewButtons.find(btn => 
        btn.textContent?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('preview') ||
        btn.getAttribute('aria-label')?.toLowerCase().includes('eye')
      );
      
      if (previewButton) {
        await user.click(previewButton);
        expect(mockRenderMarkdown).toHaveBeenCalledWith(specialContent);
      }
    });

  });

});
