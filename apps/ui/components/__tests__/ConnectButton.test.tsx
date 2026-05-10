import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';
import { ConnectButton } from '../mcp/ConnectButton';

describe('ConnectButton', () => {
  it('Idle_FormValid_IsNotDisabled', () => {
    render(<ConnectButton connectStatus="idle" isFormValid={true} onClick={vi.fn()} />);
    const btn = screen.getByRole('button');
    expect(btn).not.toBeDisabled();
  });

  it('Idle_FormInvalid_IsDisabled', () => {
    render(<ConnectButton connectStatus="idle" isFormValid={false} onClick={vi.fn()} />);
    expect(screen.getByRole('button')).toBeDisabled();
  });

  it('Loading_ShowsConnectingText', () => {
    render(<ConnectButton connectStatus="loading" isFormValid={true} onClick={vi.fn()} />);
    expect(screen.getByRole('button')).toBeDisabled();
    expect(screen.getByText(/connecting/i)).toBeInTheDocument();
  });

  it('Success_ShowsConnectedText_AndIsDisabled', () => {
    render(<ConnectButton connectStatus="success" isFormValid={true} onClick={vi.fn()} />);
    expect(screen.getByRole('button')).toBeDisabled();
    expect(screen.getByText(/connected/i)).toBeInTheDocument();
  });

  it('Error_FormValid_IsNotDisabled', () => {
    render(<ConnectButton connectStatus="error" isFormValid={true} onClick={vi.fn()} />);
    expect(screen.getByRole('button')).not.toBeDisabled();
  });

  it('Idle_FormValid_OnClick_IsCalled', async () => {
    const onClick = vi.fn();
    render(<ConnectButton connectStatus="idle" isFormValid={true} onClick={onClick} />);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });
});
