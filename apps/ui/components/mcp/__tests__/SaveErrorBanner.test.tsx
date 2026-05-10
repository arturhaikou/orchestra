import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SaveErrorBanner } from '../SaveErrorBanner';
import type { SaveMcpServerError } from '../../../types';

const NETWORK_ERROR: SaveMcpServerError = {
  code: 'NETWORK',
  message: 'Failed to save. Please check your connection and try again.',
};

const DUPLICATE_ERROR: SaveMcpServerError = {
  code: 'DUPLICATE_NAME',
  message: 'A server with this name already exists. Please choose a different name.',
};

describe('SaveErrorBanner', () => {
  it('WhenError_IsNull_RendersNothing', () => {
    const { container } = render(
      <SaveErrorBanner error={null} onDismiss={vi.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('WhenNetworkError_RendersErrorMessage', () => {
    render(<SaveErrorBanner error={NETWORK_ERROR} onDismiss={vi.fn()} />);
    expect(
      screen.getByText('Failed to save. Please check your connection and try again.')
    ).toBeInTheDocument();
  });

  it('WhenDuplicateNameError_RendersCorrectMessage', () => {
    render(<SaveErrorBanner error={DUPLICATE_ERROR} onDismiss={vi.fn()} />);
    expect(
      screen.getByText('A server with this name already exists. Please choose a different name.')
    ).toBeInTheDocument();
  });

  it('WhenDismissButtonClicked_CallsOnDismiss', () => {
    const onDismiss = vi.fn();
    render(<SaveErrorBanner error={NETWORK_ERROR} onDismiss={onDismiss} />);

    fireEvent.click(screen.getByRole('button', { name: /dismiss/i }));

    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it('Banner_HasAlertRole_ForScreenReaderAccessibility', () => {
    render(<SaveErrorBanner error={NETWORK_ERROR} onDismiss={vi.fn()} />);
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});
