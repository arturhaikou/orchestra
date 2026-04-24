import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '@testing-library/jest-dom';
import DeployMethodDialog from '../DeployMethodDialog';

const defaultProps = {
  isOpen: true,
  onClose: jest.fn(),
  onSelectScratch: jest.fn(),
  onSelectBuiltIn: jest.fn(),
};

function renderDialog(overrides: Partial<typeof defaultProps> = {}) {
  const props = { ...defaultProps, ...overrides };
  return render(<DeployMethodDialog {...props} />);
}

beforeEach(() => {
  jest.clearAllMocks();
});

describe('DeployMethodDialog', () => {
  describe('Scenario 1: Dialog appears on Deploy Agent click', () => {
    it('renders_WhenOpen_ShowsBothOptionsWithDescriptions', () => {
      renderDialog();

      expect(screen.getByText('Deploy Agent')).toBeInTheDocument();
      expect(screen.getByText('Create from Scratch')).toBeInTheDocument();
      expect(screen.getByText('Use Built-In Agent')).toBeInTheDocument();
      expect(
        screen.getByText('Configure a fully custom agent with your own settings.')
      ).toBeInTheDocument();
      expect(
        screen.getByText('Pick from a catalogue of pre-configured agent templates.')
      ).toBeInTheDocument();
    });

    it('renders_WhenClosed_ReturnsNull', () => {
      const { container } = renderDialog({ isOpen: false });
      expect(container.firstChild).toBeNull();
    });

    it('renders_WhenOpen_HasCorrectAriaAttributes', () => {
      renderDialog();

      const dialog = screen.getByRole('dialog');
      expect(dialog).toHaveAttribute('aria-modal', 'true');
      expect(dialog).toHaveAttribute('aria-labelledby', 'deploy-dialog-title');
    });
  });

  describe('Scenario 2: Create from Scratch opens existing modal', () => {
    it('onSelectScratch_WhenClicked_CallsCallback', async () => {
      const onSelectScratch = jest.fn();
      renderDialog({ onSelectScratch });

      await userEvent.click(screen.getByText('Create from Scratch'));

      expect(onSelectScratch).toHaveBeenCalledTimes(1);
    });
  });

  describe('Scenario 3: Use Built-In Agent navigates to catalogue', () => {
    it('onSelectBuiltIn_WhenClicked_CallsCallback', async () => {
      const onSelectBuiltIn = jest.fn();
      renderDialog({ onSelectBuiltIn });

      await userEvent.click(screen.getByText('Use Built-In Agent'));

      expect(onSelectBuiltIn).toHaveBeenCalledTimes(1);
    });
  });

  describe('Scenario 4: Dialog dismissed without selection', () => {
    it('onClose_WhenCloseButtonClicked_CallsCallback', async () => {
      const onClose = jest.fn();
      renderDialog({ onClose });

      await userEvent.click(screen.getByLabelText('Close'));

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('onClose_WhenCancelClicked_CallsCallback', async () => {
      const onClose = jest.fn();
      renderDialog({ onClose });

      await userEvent.click(screen.getByText('Cancel'));

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('onClose_WhenEscapePressed_CallsCallback', () => {
      const onClose = jest.fn();
      renderDialog({ onClose });

      fireEvent.keyDown(document, { key: 'Escape' });

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('onClose_WhenBackdropClicked_CallsCallback', async () => {
      const onClose = jest.fn();
      renderDialog({ onClose });

      const backdrop = screen.getByRole('dialog');
      await userEvent.click(backdrop);

      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('noStateChange_WhenDismissed_NoOtherCallbacksFired', () => {
      const onClose = jest.fn();
      const onSelectScratch = jest.fn();
      const onSelectBuiltIn = jest.fn();
      renderDialog({ onClose, onSelectScratch, onSelectBuiltIn });

      fireEvent.keyDown(document, { key: 'Escape' });

      expect(onClose).toHaveBeenCalledTimes(1);
      expect(onSelectScratch).not.toHaveBeenCalled();
      expect(onSelectBuiltIn).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('focusManagement_WhenOpened_FirstOptionReceivesFocus', () => {
      renderDialog();

      const firstOption = screen.getByText('Create from Scratch').closest('button');
      expect(document.activeElement).toBe(firstOption);
    });

    it('ariaDescribedby_OptionCards_HaveDescriptions', () => {
      renderDialog();

      const scratchButton = screen.getByText('Create from Scratch').closest('button');
      expect(scratchButton).toHaveAttribute('aria-describedby', 'scratch-description');

      const builtInButton = screen.getByText('Use Built-In Agent').closest('button');
      expect(builtInButton).toHaveAttribute('aria-describedby', 'builtin-description');
    });
  });
});
