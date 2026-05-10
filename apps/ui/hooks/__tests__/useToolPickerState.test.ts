import { renderHook, act } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { useToolPickerState } from '../useToolPickerState';

describe('useToolPickerState', () => {

  describe('initial state', () => {
    it('returns_empty_snapshot_on_init', () => {
      const { result } = renderHook(() => useToolPickerState());
      expect(result.current.state.snapshot).toEqual([]);
    });

    it('returns_empty_working_on_init', () => {
      const { result } = renderHook(() => useToolPickerState());
      expect(result.current.state.working).toEqual([]);
    });

    it('returns_null_activeSourceId_on_init', () => {
      const { result } = renderHook(() => useToolPickerState());
      expect(result.current.state.activeSourceId).toBeNull();
    });

    it('returns_empty_searchTerm_on_init', () => {
      const { result } = renderHook(() => useToolPickerState());
      expect(result.current.state.searchTerm).toBe('');
    });
  });

  describe('OPEN_MODAL', () => {
    it('copies_currentSelections_into_snapshot', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-1', 'action-2'] },
        });
      });

      expect(result.current.state.snapshot).toEqual(['action-1', 'action-2']);
    });

    it('copies_currentSelections_into_working', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-1'] },
        });
      });

      expect(result.current.state.working).toEqual(['action-1']);
    });

    it('resets_searchTerm_to_empty_on_open', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({ type: 'SET_SEARCH', payload: { searchTerm: 'github' } });
      });
      act(() => {
        result.current.dispatch({ type: 'OPEN_MODAL', payload: { currentSelections: [] } });
      });

      expect(result.current.state.searchTerm).toBe('');
    });

    it('resets_activeSourceId_to_null_on_open', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({ type: 'SET_ACTIVE_SOURCE', payload: { sourceId: 'src-1' } });
      });
      act(() => {
        result.current.dispatch({ type: 'OPEN_MODAL', payload: { currentSelections: [] } });
      });

      expect(result.current.state.activeSourceId).toBeNull();
    });
  });

  describe('TOGGLE_TOOL', () => {
    it('adds_actionId_to_working_when_not_present', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({ type: 'OPEN_MODAL', payload: { currentSelections: [] } });
      });
      act(() => {
        result.current.dispatch({ type: 'TOGGLE_TOOL', payload: { actionId: 'action-5' } });
      });

      expect(result.current.state.working).toContain('action-5');
    });

    it('removes_actionId_from_working_when_already_present', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-5'] },
        });
      });
      act(() => {
        result.current.dispatch({ type: 'TOGGLE_TOOL', payload: { actionId: 'action-5' } });
      });

      expect(result.current.state.working).not.toContain('action-5');
    });

    it('does_not_affect_other_working_ids_when_toggling', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-1', 'action-2'] },
        });
      });
      act(() => {
        result.current.dispatch({ type: 'TOGGLE_TOOL', payload: { actionId: 'action-1' } });
      });

      expect(result.current.state.working).toEqual(['action-2']);
    });
  });

  describe('COMMIT', () => {
    it('preserves_working_after_commit', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-1'] },
        });
      });
      act(() => {
        result.current.dispatch({ type: 'TOGGLE_TOOL', payload: { actionId: 'action-2' } });
      });
      act(() => {
        result.current.dispatch({ type: 'COMMIT' });
      });

      expect(result.current.state.working).toEqual(['action-1', 'action-2']);
    });
  });

  describe('DISCARD', () => {
    it('restores_working_to_snapshot_on_discard', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-1'] },
        });
      });
      act(() => {
        result.current.dispatch({ type: 'TOGGLE_TOOL', payload: { actionId: 'action-2' } });
      });
      act(() => {
        result.current.dispatch({ type: 'DISCARD' });
      });

      expect(result.current.state.working).toEqual(['action-1']);
    });

    it('does_not_modify_snapshot_on_discard', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-1'] },
        });
      });
      act(() => {
        result.current.dispatch({ type: 'TOGGLE_TOOL', payload: { actionId: 'action-2' } });
      });
      act(() => {
        result.current.dispatch({ type: 'DISCARD' });
      });

      expect(result.current.state.snapshot).toEqual(['action-1']);
    });
  });

  describe('SET_SEARCH', () => {
    it('updates_searchTerm_to_dispatched_value', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({ type: 'SET_SEARCH', payload: { searchTerm: 'jira' } });
      });

      expect(result.current.state.searchTerm).toBe('jira');
    });

    it('clears_searchTerm_when_empty_string_dispatched', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({ type: 'SET_SEARCH', payload: { searchTerm: 'jira' } });
      });
      act(() => {
        result.current.dispatch({ type: 'SET_SEARCH', payload: { searchTerm: '' } });
      });

      expect(result.current.state.searchTerm).toBe('');
    });
  });

  describe('SET_ACTIVE_SOURCE', () => {
    it('updates_activeSourceId_to_dispatched_source', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({ type: 'SET_ACTIVE_SOURCE', payload: { sourceId: 'mcp-server-1' } });
      });

      expect(result.current.state.activeSourceId).toBe('mcp-server-1');
    });

    it('switches_activeSourceId_when_different_source_dispatched', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({ type: 'SET_ACTIVE_SOURCE', payload: { sourceId: 'cat-1' } });
      });
      act(() => {
        result.current.dispatch({ type: 'SET_ACTIVE_SOURCE', payload: { sourceId: 'cat-2' } });
      });

      expect(result.current.state.activeSourceId).toBe('cat-2');
    });
  });

  describe('SELECT_ALL', () => {
    it('adds_all_provided_action_ids_to_working', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({ type: 'OPEN_MODAL', payload: { currentSelections: [] } });
      });
      act(() => {
        result.current.dispatch({
          type: 'SELECT_ALL',
          payload: { actionIds: ['action-a', 'action-b', 'action-c'] },
        });
      });

      expect(result.current.state.working).toEqual(
        expect.arrayContaining(['action-a', 'action-b', 'action-c'])
      );
    });

    it('deduplicates_already_present_ids_on_select_all', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-a'] },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'SELECT_ALL',
          payload: { actionIds: ['action-a', 'action-b'] },
        });
      });

      const working = result.current.state.working;
      expect(working.filter(id => id === 'action-a')).toHaveLength(1);
      expect(working).toContain('action-b');
    });
  });

  describe('DESELECT_ALL', () => {
    it('removes_all_provided_action_ids_from_working', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-a', 'action-b', 'action-c'] },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'DESELECT_ALL',
          payload: { actionIds: ['action-a', 'action-b'] },
        });
      });

      expect(result.current.state.working).toEqual(['action-c']);
    });

    it('leaves_other_working_ids_intact_on_deselect_all', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: ['action-x', 'action-y'] },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'DESELECT_ALL',
          payload: { actionIds: ['action-x'] },
        });
      });

      expect(result.current.state.working).toEqual(['action-y']);
    });
  });
});
