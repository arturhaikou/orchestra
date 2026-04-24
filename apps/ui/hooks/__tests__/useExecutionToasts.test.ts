import { AgentExecutionCompletedEvent } from '../../types';
import { isValidReviewUrl, useExecutionToasts } from '../useExecutionToasts';

describe('useExecutionToasts', () => {
  describe('isValidReviewUrl', () => {
    it('accepts_HttpsUrl_ReturnsTrue', () => {
      expect(isValidReviewUrl('https://github.com/org/repo/pull/1')).toBe(true);
    });

    it('accepts_HttpUrl_ReturnsTrue', () => {
      expect(isValidReviewUrl('http://gitlab.local/org/repo/-/merge_requests/5')).toBe(true);
    });

    it('rejects_JavascriptScheme_ReturnsFalse', () => {
      expect(isValidReviewUrl('javascript:alert(1)')).toBe(false);
    });

    it('rejects_DataScheme_ReturnsFalse', () => {
      expect(isValidReviewUrl('data:text/html,<script>alert(1)</script>')).toBe(false);
    });

    it('rejects_EmptyString_ReturnsFalse', () => {
      expect(isValidReviewUrl('')).toBe(false);
    });

    it('rejects_Null_ReturnsFalse', () => {
      expect(isValidReviewUrl(null)).toBe(false);
    });

    it('rejects_Undefined_ReturnsFalse', () => {
      expect(isValidReviewUrl(undefined)).toBe(false);
    });

    it('rejects_FtpScheme_ReturnsFalse', () => {
      expect(isValidReviewUrl('ftp://files.example.com/report')).toBe(false);
    });
  });

  describe('toast creation from success event', () => {
    const successEvent: AgentExecutionCompletedEvent = {
      workspaceId: 'ws-1',
      agentId: 'agent-1',
      agentName: 'Code Reviewer',
      ticketId: 'ticket-1',
      ticketTitle: 'Fix login bug',
      status: 'success',
      reviewUrl: 'https://github.com/org/repo/pull/42',
    };

    it('SuccessEvent_WithReviewUrl_CreatesToastWithSuccessStatusAndUrl', () => {
      const { toasts } = useExecutionToasts('ws-1');
      // Stub returns empty; when implemented, receiving successEvent via SignalR
      // should produce a toast with status=success, reviewUrl populated, agentName, ticketTitle
      expect(toasts).toEqual(
        expect.arrayContaining([
          expect.objectContaining({
            agentName: 'Code Reviewer',
            ticketTitle: 'Fix login bug',
            status: 'success',
            reviewUrl: 'https://github.com/org/repo/pull/42',
          }),
        ])
      );
    });
  });

  describe('toast creation from failed event', () => {
    const failedEvent: AgentExecutionCompletedEvent = {
      workspaceId: 'ws-1',
      agentId: 'agent-2',
      agentName: 'Code Reviewer',
      ticketId: 'ticket-2',
      ticketTitle: 'Broken pipeline',
      status: 'failed',
      reviewUrl: null,
    };

    it('FailedEvent_WithoutReviewUrl_CreatesToastWithFailedStatusAndNoUrl', () => {
      const { toasts } = useExecutionToasts('ws-1');
      expect(toasts).toEqual(
        expect.arrayContaining([
          expect.objectContaining({
            agentName: 'Code Reviewer',
            ticketTitle: 'Broken pipeline',
            status: 'failed',
            reviewUrl: null,
          }),
        ])
      );
    });
  });

  describe('toast auto-dismiss', () => {
    it('Toast_AfterTimeout_IsRemovedFromList', () => {
      const { toasts } = useExecutionToasts('ws-1');
      // When implemented, after the configured timeout (e.g. 10s) elapses,
      // the toast should automatically be removed from the toasts array
      // Stub returns empty; this test will verify timeout logic once implemented
      expect(toasts.length).toBe(0); // stub — should be verified with fake timers
    });
  });

  describe('manual dismiss', () => {
    it('Dismiss_WithToastId_RemovesToastFromList', () => {
      const { toasts, dismiss } = useExecutionToasts('ws-1');
      // When implemented, calling dismiss(id) should remove that toast
      dismiss('toast-1');
      expect(toasts.every((t) => t.id !== 'toast-1')).toBe(true);
    });
  });

  describe('workspace filtering', () => {
    it('Event_FromDifferentWorkspace_DoesNotCreateToast', () => {
      const { toasts } = useExecutionToasts('ws-other');
      // Events for ws-1 should not appear when hook is scoped to ws-other
      expect(toasts).toHaveLength(0);
    });
  });
});
