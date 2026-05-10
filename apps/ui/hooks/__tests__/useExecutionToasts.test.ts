import { renderHook, act } from '@testing-library/react';
import { vi, beforeEach } from 'vitest';
import { AgentExecutionCompletedEvent } from '../../types';
import { isValidReviewUrl, useExecutionToasts } from '../useExecutionToasts';

let capturedExecutionHandler: ((event: AgentExecutionCompletedEvent) => void) | null = null;

vi.mock('../../services/signalRService', () => ({
  onAgentExecutionCompleted: vi.fn((handler: (event: AgentExecutionCompletedEvent) => void) => {
    capturedExecutionHandler = handler;
  }),
  offAgentExecutionCompleted: vi.fn(),
}));

vi.mock('../../services/agentService', () => ({ getAgents: vi.fn(() => Promise.resolve([])) }));

beforeEach(() => {
  capturedExecutionHandler = null;
  vi.clearAllMocks();
});

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
      const { result } = renderHook(() => useExecutionToasts('ws-1'));
      act(() => { capturedExecutionHandler?.(successEvent); });
      expect(result.current.toasts).toEqual(
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
      const { result } = renderHook(() => useExecutionToasts('ws-1'));
      act(() => { capturedExecutionHandler?.(failedEvent); });
      expect(result.current.toasts).toEqual(
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
      const { result } = renderHook(() => useExecutionToasts('ws-1'));
      expect(result.current.toasts.length).toBe(0);
    });
  });

  describe('manual dismiss', () => {
    it('Dismiss_WithToastId_RemovesToastFromList', () => {
      const { result } = renderHook(() => useExecutionToasts('ws-1'));
      act(() => {
        result.current.dismiss('toast-1');
      });
      expect(result.current.toasts.every((t) => t.id !== 'toast-1')).toBe(true);
    });
  });

  describe('workspace filtering', () => {
    it('Event_FromDifferentWorkspace_DoesNotCreateToast', () => {
      const { result } = renderHook(() => useExecutionToasts('ws-other'));
      const otherWorkspaceEvent: AgentExecutionCompletedEvent = {
        workspaceId: 'ws-1',
        agentId: 'agent-1',
        agentName: 'Code Reviewer',
        ticketId: 'ticket-1',
        ticketTitle: 'Fix login bug',
        status: 'success',
        reviewUrl: null,
      };
      act(() => { capturedExecutionHandler?.(otherWorkspaceEvent); });
      expect(result.current.toasts).toHaveLength(0);
    });
  });
});
