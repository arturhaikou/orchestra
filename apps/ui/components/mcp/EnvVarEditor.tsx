import React from 'react';
import { Plus } from 'lucide-react';
import { EnvVar, EnvVarValueEditState, EnvVarEditStateMap } from '../../types';
import EnvVarRow from './EnvVarRow';

interface EnvVarEditorProps {
  envVars: EnvVar[];
  envKeyErrors: Record<number, string>;
  envKeyTouched: Record<number, boolean>;
  envTotalSizeError?: string;
  isEditMode: boolean;
  envVarEditStateMap?: EnvVarEditStateMap;
  isDisabled?: boolean;
  onChange: (envVars: EnvVar[]) => void;
  onKeyBlur: (index: number) => void;
  onValueEditStateChange?: (rowIndex: number, state: EnvVarValueEditState) => void;
}

const EnvVarEditor: React.FC<EnvVarEditorProps> = ({
  envVars, envKeyErrors, envKeyTouched, envTotalSizeError,
  isEditMode, envVarEditStateMap, isDisabled, onChange, onKeyBlur,
  onValueEditStateChange,
}) => (
  <div>
    <label className="block text-sm font-medium text-textMuted mb-1.5">
      Environment Variables
    </label>
    <div className="space-y-2">
      {envVars.length === 0 && (
        <p className="text-xs text-textMuted italic">No environment variables yet.</p>
      )}
      {envVars.map((ev, i) => (
        <EnvVarRow
          key={i}
          index={i}
          envKey={ev.key}
          envValue={ev.value}
          keyError={envKeyErrors[i]}
          isKeyTouched={!!envKeyTouched[i]}
          valueEditState={resolveEditState(i, isEditMode, envVarEditStateMap)}
          isDisabled={isDisabled}
          onChange={(idx, patch) => onChange(updateEnvVar(envVars, idx, patch))}
          onKeyBlur={onKeyBlur}
          onRemove={idx => onChange(envVars.filter((_, j) => j !== idx))}
          onValueEditStateChange={onValueEditStateChange ?? (() => {})}
        />
      ))}
      <button
        type="button"
        disabled={isDisabled}
        onClick={() => onChange([...envVars, { key: '', value: '' }])}
        className="text-xs text-primary hover:text-primary/80 transition-colors
                   disabled:opacity-45 disabled:cursor-not-allowed flex items-center gap-1"
      >
        <Plus size={12} /> Add Variable
      </button>
      {envTotalSizeError && (
        <p className="mt-1 text-xs text-amber-400">{envTotalSizeError}</p>
      )}
    </div>
  </div>
);

function resolveEditState(
  index: number,
  isEditMode: boolean,
  map?: EnvVarEditStateMap
): EnvVarValueEditState {
  if (!isEditMode) return 'touched';
  return map?.[index] ?? 'touched';
}

function updateEnvVar(
  envVars: EnvVar[],
  index: number,
  patch: Partial<EnvVar>
): EnvVar[] {
  return envVars.map((ev, i) => (i === index ? { ...ev, ...patch } : ev));
}

export default EnvVarEditor;
