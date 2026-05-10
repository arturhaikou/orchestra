import { useReducer, Dispatch } from 'react';
import { ToolPickerState, ToolPickerAction } from '../types';

const initialState: ToolPickerState = {
  snapshot: [],
  working: [],
  activeSourceId: null,
  searchTerm: '',
  mcpSnapshot: {},
  mcpWorking: {},
};

function toolPickerReducer(
  state: ToolPickerState,
  action: ToolPickerAction
): ToolPickerState {
  switch (action.type) {
    case 'OPEN_MODAL': {
      const initialMcp = action.payload.initialMcpSelections ?? {};
      return {
        snapshot: [...action.payload.currentSelections],
        working: [...action.payload.currentSelections],
        activeSourceId: action.payload.initialActiveSourceId ?? null,
        searchTerm: '',
        mcpSnapshot: { ...initialMcp },
        mcpWorking: { ...initialMcp },
      };
    }

    case 'SET_ACTIVE_SOURCE':
      return { ...state, activeSourceId: action.payload.sourceId };

    case 'TOGGLE_TOOL': {
      const { actionId } = action.payload;
      const isSelected = state.working.includes(actionId);
      return {
        ...state,
        working: isSelected
          ? state.working.filter(id => id !== actionId)
          : [...state.working, actionId],
      };
    }

    case 'SELECT_ALL': {
      const merged = Array.from(new Set([...state.working, ...action.payload.actionIds]));
      return { ...state, working: merged };
    }

    case 'DESELECT_ALL': {
      const toRemove = new Set(action.payload.actionIds);
      return { ...state, working: state.working.filter(id => !toRemove.has(id)) };
    }

    case 'SET_SEARCH':
      return { ...state, searchTerm: action.payload.searchTerm };

    case 'COMMIT':
      return state;

    case 'SET_MCP_SNAPSHOT':
      return {
        ...state,
        mcpSnapshot: { ...action.payload.mcpSelections },
        mcpWorking: { ...action.payload.mcpSelections },
      };

    case 'TOGGLE_MCP_TOOL': {
      const { serverId, toolName } = action.payload;
      const currentTools = state.mcpWorking[serverId] ?? [];
      const isSelected = currentTools.includes(toolName);
      const updatedTools = isSelected
        ? currentTools.filter((t) => t !== toolName)
        : [...currentTools, toolName];
      return {
        ...state,
        mcpWorking: { ...state.mcpWorking, [serverId]: updatedTools },
      };
    }

    case 'DISCARD':
      return { ...state, working: [...state.snapshot], mcpWorking: { ...state.mcpSnapshot } };

    case 'CLEAR_MCP_SERVER': {
      const newMcpWorking = { ...state.mcpWorking };
      delete newMcpWorking[action.payload.serverId];
      return { ...state, mcpWorking: newMcpWorking };
    }

    default:
      return state;
  }
}

export interface UseToolPickerStateResult {
  state: ToolPickerState;
  dispatch: Dispatch<ToolPickerAction>;
}

export function useToolPickerState(): UseToolPickerStateResult {
  const [state, dispatch] = useReducer(toolPickerReducer, initialState);
  return { state, dispatch };
}
