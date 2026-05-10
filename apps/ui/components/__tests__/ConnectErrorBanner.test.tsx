import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';
import { ConnectErrorBanner } from '../mcp/ConnectErrorBanner';

describe('ConnectErrorBanner', () => {
  it('Scenario3_Timeout_ShowsTimeoutMessage', () => {
    render(<ConnectErrorBanner errorCode="CONNECTION_TIMEOUT" onDismiss={vi.fn()} />);
    expect(screen.getByText(/did not respond within 30 seconds/i)).toBeInTheDocument();
  });

  it('Scenario4_AuthFailed_ShowsAuthMessage', () => {
    render(<ConnectErrorBanner errorCode="AUTH_FAILED" onDismiss={vi.fn()} />);
    expect(screen.getByText(/check your api key/i)).toBeInTheDocument();
  });

  it('Unreachable_ShowsUnreachableMessage', () => {
    render(<ConnectErrorBanner errorCode="UNREACHABLE" onDismiss={vi.fn()} />);
    expect(screen.getByText(/check the url or command/i)).toBeInTheDocument();
  });

  it('Unknown_ShowsGenericMessage', () => {
    render(<ConnectErrorBanner errorCode="UNKNOWN" onDismiss={vi.fn()} />);
    expect(screen.getByText(/unexpected error/i)).toBeInTheDocument();
  });

  it('HasDismissButton', () => {
    render(<ConnectErrorBanner errorCode="UNKNOWN" onDismiss={vi.fn()} />);
    expect(screen.getByRole('button', { name: /dismiss/i })).toBeInTheDocument();
  });

  it('OnDismiss_IsCalledOnDismissClick', async () => {
    const onDismiss = vi.fn();
    render(<ConnectErrorBanner errorCode="UNKNOWN" onDismiss={onDismiss} />);
    await userEvent.click(screen.getByRole('button', { name: /dismiss/i }));
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it('HasAlertRole', () => {
    render(<ConnectErrorBanner errorCode="UNKNOWN" onDismiss={vi.fn()} />);
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });
});
