import '@testing-library/jest-dom';
import React from 'react';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import EditWorkspaceModal from '../EditWorkspaceModal';
import * as workspaceService from '../../../services/workspaceService';
import { Workspace } from '../../../types';

// Mock the workspace service
jest.mock('../../../services/workspaceService');

describe('EditWorkspaceModal - Stale Model Detection', () => {
  const mockWorkspace: Workspace = {
    id: 'workspace-1',
    name: 'Engineering',
    isAiSummarizationEnabled: true,
    isCustomerSatisfactionAnalysisEnabled: true,
    aiSummarizationModelId: 'old-model-x',
    customerSatisfactionAnalysisModelId: 'valid-model-y',
  };

  const mockOnClose = jest.fn();
  const mockOnSubmit = jest.fn();
  const mockAvailableModels = ['valid-model-y', 'valid-model-z', 'another-model'];

  beforeEach(() => {
    jest.clearAllMocks();
    (workspaceService.fetchWorkspaceModels as jest.Mock).mockResolvedValue(
      mockAvailableModels
    );
  });

  describe('Scenario 1: Stale model identified — warning shown', () => {
    it('should display warning for stale AI Summarization model', async () => {
      render(
        <EditWorkspaceModal
          isOpen={true}
          onClose={mockOnClose}
          onSubmit={mockOnSubmit}
          workspace={mockWorkspace}
          isProcessing={false}
        />
      );

      // Wait for models to load
      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalled();
      });

      // Check that the stale model warning is displayed
      const aiSummarySection = screen.getByText('Model for AI Summarization').closest('div');
      const warningText = within(aiSummarySection!).getByRole('alert');
      expect(warningText).toBeInTheDocument();
      expect(warningText).toHaveTextContent(
        'The previously selected model for AI Summarization is no longer available'
      );
    });

    it('should display warning for stale Customer Satisfaction model', async () => {
      const workspaceWithStaleCSA: Workspace = {
        ...mockWorkspace,
        aiSummarizationModelId: 'valid-model-y',
        customerSatisfactionAnalysisModelId: 'old-model-w',
      };

      render(
        <EditWorkspaceModal
          isOpen={true}
          onClose={mockOnClose}
          onSubmit={mockOnSubmit}
          workspace={workspaceWithStaleCSA}
          isProcessing={false}
        />
      );

      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalled();
      });

      // Check that the stale model warning is displayed for CSA
      const csaSection = screen.getByText('Model for Customer Satisfaction Analysis').closest('div');
      const warningText = within(csaSection!).getByRole('alert');
      expect(warningText).toBeInTheDocument();
      expect(warningText).toHaveTextContent(
        'The previously selected model for Customer Satisfaction Analysis is no longer available'
      );
    });

    it('should show unresolved/placeholder state for stale model selector', async () => {
      render(
        <EditWorkspaceModal
          isOpen={true}
          onClose={mockOnClose}
          onSubmit={mockOnSubmit}
          workspace={mockWorkspace}
          isProcessing={false}
        />
      );

      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalled();
      });

      // The selector should not have the old model pre-selected
      const selectors = screen.getAllByRole('combobox');
      const aiSummarySelector = selectors[0]; // First selector is for AI Summarization
      expect(aiSummarySelector).toHaveValue('');
    });
  });

  describe('Scenario 2: Valid saved model — no warning shown', () => {
    it('should not display warning when saved model is available', async () => {
      const workspaceWithValidModel: Workspace = {
        ...mockWorkspace,
        aiSummarizationModelId: 'valid-model-y',
        customerSatisfactionAnalysisModelId: 'valid-model-z',
      };

      render(
        <EditWorkspaceModal
          isOpen={true}
          onClose={mockOnClose}
          onSubmit={mockOnSubmit}
          workspace={workspaceWithValidModel}
          isProcessing={false}
        />
      );

      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalled();
      });

      // Should not find any alert role elements
      const alerts = screen.queryAllByRole('alert');
      expect(alerts).toHaveLength(0);
    });

    it('should pre-select valid saved model in selector', async () => {
      const workspaceWithValidModel: Workspace = {
        ...mockWorkspace,
        aiSummarizationModelId: 'valid-model-y',
      };

      render(
        <EditWorkspaceModal
          isOpen={true}
          onClose={mockOnClose}
          onSubmit={mockOnSubmit}
          workspace={workspaceWithValidModel}
          isProcessing={false}
        />
      );

      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalled();
      });

      const selectors = screen.getAllByRole('combobox');
      const aiSummarySelector = selectors[0];
      expect(aiSummarySelector).toHaveValue('valid-model-y');
    });
  });

  describe('Scenario 3: No saved model — no stale warning', () => {
    it('should not display stale warning when model ID is undefined', async () => {
      const workspaceWithoutModelId: Workspace = {
        ...mockWorkspace,
        aiSummarizationModelId: undefined,
      };

      render(
        <EditWorkspaceModal
          isOpen={true}
          onClose={mockOnClose}
          onSubmit={mockOnSubmit}
          workspace={workspaceWithoutModelId}
          isProcessing={false}
        />
      );

      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalled();
      });

      const alerts = screen.queryAllByRole('alert');
      expect(alerts).toHaveLength(0);
    });
  });

  describe('Scenario 4: Owner re-selects valid model after warning', () => {
    it('should dismiss warning when user selects a valid model', async () => {
      const user = userEvent.setup();

      render(
        <EditWorkspaceModal
          isOpen={true}
          onClose={mockOnClose}
          onSubmit={mockOnSubmit}
          workspace={mockWorkspace}
          isProcessing={false}
        />
      );

      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalled();
      });

      // Verify warning is shown initially
      const aiSummarySection = screen.getByText('Model for AI Summarization').closest('div');
      let warningText = within(aiSummarySection!).queryByRole('alert');
      expect(warningText).toBeInTheDocument();

      // User selects a valid model
      const selectors = screen.getAllByRole('combobox');
      const aiSummarySelector = selectors[0];
      await user.click(aiSummarySelector);
      await user.click(screen.getByText('valid-model-z'));

      // Warning should disappear
      await waitFor(() => {
        warningText = within(aiSummarySection!).queryByRole('alert');
        expect(warningText).not.toBeInTheDocument();
      });
    });
  });

  describe('Scenario 5: Owner saves without resolving stale model', () => {
    it('should include unresolved field in submission when user does not change selector', async () => {
      const user = userEvent.setup();

      render(
        <EditWorkspaceModal
          isOpen={true}
          onClose={mockOnClose}
          onSubmit={mockOnSubmit}
          workspace={mockWorkspace}
          isProcessing={false}
        />
      );

      await waitFor(() => {
        expect(workspaceService.fetchWorkspaceModels).toHaveBeenCalled();
      });

      // User saves without selecting a model for the stale field
      const saveButton = screen.getByRole('button', { name: /Save Changes/i });
      await user.click(saveButton);

      // Verify submission was called with the unresolved field being undefined
      expect(mockOnSubmit).toHaveBeenCalledWith(
        'Engineering',
        true,
        true,
        undefined, // aiSummarizationModelId should be undefined (unresolved)
        'valid-model-y' // customerSatisfactionAnalysisModelId is valid
      );
    });
  });
});
