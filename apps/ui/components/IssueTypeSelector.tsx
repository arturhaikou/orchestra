import React from 'react';

interface IssueTypeSelectorProps {
  value: string | null;
  onChange: (issueType: string) => void;
  disabled?: boolean;
}

const ISSUE_TYPES = [
  { value: 'Task', label: 'Task', description: 'A task that needs to be done' },
  { value: 'Story', label: 'Story', description: 'A user story or feature' },
  { value: 'Bug', label: 'Bug', description: 'A problem that needs to be fixed' },
  { value: 'Epic', label: 'Epic', description: 'A large body of work' }
];

export const IssueTypeSelector: React.FC<IssueTypeSelectorProps> = ({
  value,
  onChange,
  disabled = false
}) => {
  const handleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    onChange(e.target.value);
  };

  return (
    <select
      value={value || ''}
      onChange={handleChange}
      disabled={disabled}
      className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-md text-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed"
    >
      <option value="">Select issue type...</option>
      {ISSUE_TYPES.map(type => (
        <option key={type.value} value={type.value} title={type.description}>
          {type.label}
        </option>
      ))}
    </select>
  );
};
