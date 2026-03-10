import React from 'react';

interface ToggleSwitchProps {
  id: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  disabled?: boolean;
}

const ToggleSwitch: React.FC<ToggleSwitchProps> = ({
  id,
  checked,
  onChange,
  label,
  disabled = false,
}) => {
  return (
    <div className="flex items-center gap-3">
      <button
        type="button"
        id={id}
        onClick={() => !disabled && onChange(!checked)}
        disabled={disabled}
        className={`relative inline-flex h-6 w-11 items-center rounded-full transition-colors ${
          checked
            ? 'bg-primary shadow-lg shadow-primary/30'
            : 'bg-border'
        } ${disabled ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
      >
        <span
          className={`inline-block h-5 w-5 transform rounded-full bg-white transition-transform ${
            checked ? 'translate-x-5' : 'translate-x-0.5'
          }`}
        />
      </button>
      <label
        htmlFor={id}
        className={`text-sm ${
          disabled ? 'text-textMuted cursor-not-allowed' : 'text-text cursor-pointer'
        }`}
        onClick={() => !disabled && onChange(!checked)}
      >
        {label}
      </label>
    </div>
  );
};

export default ToggleSwitch;
