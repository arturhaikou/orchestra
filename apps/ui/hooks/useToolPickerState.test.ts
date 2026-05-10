import { renderHook, act } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { useToolPickerState } from './useToolPickerState';

describe('toolPickerReducer — MCP actions (FR-005)', () => {

  describe('SET_MCP_SNAPSHOT', () => {
    it('seeds_mcpSnapshot_and_mcpWorking_from_payload', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1', 'tool2'] } },
        });
      });

      expect(result.current.state.mcpSnapshot).toEqual({ 'server-abc': ['tool1', 'tool2'] });
      expect(result.current.state.mcpWorking).toEqual({ 'server-abc': ['tool1', 'tool2'] });
    });

    it('overwrites_previous_mcpSnapshot_and_mcpWorking', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-xyz': ['toolA'] } },
        });
      });

      expect(result.current.state.mcpSnapshot).toEqual({ 'server-xyz': ['toolA'] });
      expect(result.current.state.mcpWorking).toEqual({ 'server-xyz': ['toolA'] });
    });

    it('handles_multiple_servers_in_single_snapshot', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: {
            mcpSelections: {
              'server-abc': ['tool1', 'tool2'],
              'server-xyz': ['toolA', 'toolB', 'toolC'],
            },
          },
        });
      });

      expect(result.current.state.mcpSnapshot['server-abc']).toEqual(['tool1', 'tool2']);
      expect(result.current.state.mcpSnapshot['server-xyz']).toEqual(['toolA', 'toolB', 'toolC']);
      expect(result.current.state.mcpWorking['server-abc']).toEqual(['tool1', 'tool2']);
      expect(result.current.state.mcpWorking['server-xyz']).toEqual(['toolA', 'toolB', 'toolC']);
    });
  });

  describe('TOGGLE_MCP_TOOL', () => {
    it('adds_tool_name_to_mcpWorking_when_not_present_scenario_1', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool2' },
        });
      });

      expect(result.current.state.mcpWorking['server-abc']).toContain('tool1');
      expect(result.current.state.mcpWorking['server-abc']).toContain('tool2');
    });

    it('removes_tool_name_from_mcpWorking_when_already_present_scenario_3', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1', 'tool2', 'tool3'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool1' },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool2' },
        });
      });

      expect(result.current.state.mcpWorking['server-abc']).toEqual(['tool3']);
    });

    it('results_in_empty_array_when_last_tool_is_removed_scenario_4', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool1' },
        });
      });

      expect(result.current.state.mcpWorking['server-abc']).toEqual([]);
    });

    it('does_not_affect_other_servers_when_toggling', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: {
            mcpSelections: {
              'server-abc': ['tool1'],
              'server-xyz': ['toolA'],
            },
          },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool1' },
        });
      });

      expect(result.current.state.mcpWorking['server-abc']).toEqual([]);
      expect(result.current.state.mcpWorking['server-xyz']).toEqual(['toolA']);
    });

    it('does_not_modify_mcpSnapshot_when_toggling', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool1' },
        });
      });

      expect(result.current.state.mcpSnapshot['server-abc']).toEqual(['tool1']);
    });

    it('creates_server_entry_in_mcpWorking_when_server_not_previously_in_snapshot', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-new', toolName: 'tool1' },
        });
      });

      expect(result.current.state.mcpWorking['server-new']).toContain('tool1');
    });
  });

  describe('DISCARD — MCP state', () => {
    it('restores_mcpWorking_from_mcpSnapshot_on_discard_scenario_5', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool1' },
        });
      });
      act(() => {
        result.current.dispatch({ type: 'DISCARD' });
      });

      expect(result.current.state.mcpWorking).toEqual({ 'server-abc': ['tool1'] });
    });

    it('does_not_modify_mcpSnapshot_on_discard', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1', 'tool2'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool1' },
        });
      });
      act(() => {
        result.current.dispatch({ type: 'DISCARD' });
      });

      expect(result.current.state.mcpSnapshot).toEqual({ 'server-abc': ['tool1', 'tool2'] });
    });
  });

  describe('OPEN_MODAL — MCP initialization', () => {
    it('initializes_mcpSnapshot_as_empty_object_on_open', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: [], initialActiveSourceId: null },
        });
      });

      expect(result.current.state.mcpSnapshot).toEqual({});
    });

    it('initializes_mcpWorking_as_empty_object_on_open', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: [], initialActiveSourceId: null },
        });
      });

      expect(result.current.state.mcpWorking).toEqual({});
    });
  });

  describe('CLEAR_MCP_SERVER (FR-005 — Scenario 4)', () => {
    it('removes_server_entry_from_mcpWorking', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: {
            mcpSelections: {
              'server-abc': ['tool1', 'tool2'],
              'server-xyz': ['toolA'],
            },
          },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'CLEAR_MCP_SERVER',
          payload: { serverId: 'server-abc' },
        });
      });

      expect(result.current.state.mcpWorking['server-abc']).toBeUndefined();
      expect(result.current.state.mcpWorking['server-xyz']).toEqual(['toolA']);
    });

    it('clears_server_that_has_no_tools_without_error', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-xyz': ['toolA'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'CLEAR_MCP_SERVER',
          payload: { serverId: 'server-NONEXISTENT' },
        });
      });

      expect(result.current.state.mcpWorking).toEqual({ 'server-xyz': ['toolA'] });
    });

    it('removes_only_specified_server_leaving_others_intact', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: {
            mcpSelections: {
              'server-1': ['t1'],
              'server-2': ['t2'],
              'server-3': ['t3'],
            },
          },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'CLEAR_MCP_SERVER',
          payload: { serverId: 'server-2' },
        });
      });

      expect(result.current.state.mcpWorking['server-1']).toEqual(['t1']);
      expect(result.current.state.mcpWorking['server-2']).toBeUndefined();
      expect(result.current.state.mcpWorking['server-3']).toEqual(['t3']);
    });
  });

  describe('OPEN_MODAL — with initialMcpSelections (FR-005 — Scenarios 3 & 5)', () => {
    it('seeds_mcpWorking_from_initialMcpSelections_when_provided', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: {
            currentSelections: [],
            initialMcpSelections: { 'server-abc': ['tool1', 'tool2'] },
          },
        });
      });

      expect(result.current.state.mcpWorking).toEqual({ 'server-abc': ['tool1', 'tool2'] });
      expect(result.current.state.mcpSnapshot).toEqual({ 'server-abc': ['tool1', 'tool2'] });
    });

    it('resets_mcpWorking_to_empty_when_initialMcpSelections_not_provided', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'SET_MCP_SNAPSHOT',
          payload: { mcpSelections: { 'server-abc': ['tool1'] } },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: { currentSelections: [] },
        });
      });

      expect(result.current.state.mcpWorking).toEqual({});
      expect(result.current.state.mcpSnapshot).toEqual({});
    });

    it('sets_activeSourceId_from_initialActiveSourceId_alongside_initialMcpSelections', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: {
            currentSelections: [],
            initialActiveSourceId: 'server-abc',
            initialMcpSelections: { 'server-abc': ['tool1'] },
          },
        });
      });

      expect(result.current.state.activeSourceId).toBe('server-abc');
      expect(result.current.state.mcpWorking['server-abc']).toEqual(['tool1']);
    });

    it('supports_multi_server_initialMcpSelections', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: {
            currentSelections: [],
            initialMcpSelections: {
              'server-abc': ['toolA', 'toolB'],
              'server-xyz': ['toolX'],
            },
          },
        });
      });

      expect(result.current.state.mcpWorking['server-abc']).toContain('toolA');
      expect(result.current.state.mcpWorking['server-abc']).toContain('toolB');
      expect(result.current.state.mcpWorking['server-xyz']).toContain('toolX');
    });

    it('mcpSnapshot_mirrors_initialMcpSelections_after_open_modal', () => {
      const { result } = renderHook(() => useToolPickerState());

      const initialSelections = {
        'server-abc': ['tool1', 'tool2'],
        'server-xyz': ['toolA', 'toolB', 'toolC'],
      };

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: {
            currentSelections: [],
            initialMcpSelections: initialSelections,
          },
        });
      });

      expect(result.current.state.mcpSnapshot).toEqual(initialSelections);
      expect(result.current.state.mcpWorking).toEqual(initialSelections);
    });

    it('toggle_tool_after_open_modal_with_initialMcpSelections_adjusts_count', () => {
      const { result } = renderHook(() => useToolPickerState());

      act(() => {
        result.current.dispatch({
          type: 'OPEN_MODAL',
          payload: {
            currentSelections: [],
            initialMcpSelections: { 'server-abc': ['tool1', 'tool2'] },
          },
        });
      });
      act(() => {
        result.current.dispatch({
          type: 'TOGGLE_MCP_TOOL',
          payload: { serverId: 'server-abc', toolName: 'tool1' },
        });
      });

      expect(result.current.state.mcpWorking['server-abc']).toEqual(['tool2']);
      expect(result.current.state.mcpSnapshot['server-abc']).toEqual(['tool1', 'tool2']);
    });
  });
});