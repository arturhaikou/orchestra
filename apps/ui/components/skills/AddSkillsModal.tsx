import React, { useEffect, useState, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { X, Check, BookOpen } from 'lucide-react';
import { Skill } from '../../types';

export interface AddSkillsModalProps {
  isOpen: boolean;
  allSkills: Skill[];
  alreadySelectedIds: string[];
  onCommit: (selectedIds: string[]) => void;
  onDiscard: () => void;
}

const SkillPickerCard: React.FC<{
  skill: Skill;
  selected: boolean;
  onToggle: (id: string) => void;
}> = ({ skill, selected, onToggle }) => (
  <button
    type="button"
    onClick={() => onToggle(skill.id)}
    className={`relative w-full text-left p-4 rounded-lg border transition-all duration-150
      ${selected
        ? 'border-primary bg-primary/10 shadow-sm shadow-primary/20'
        : 'border-border bg-surfaceHighlight hover:border-primary/50 hover:bg-surface'
      }`}
  >
    {selected && (
      <span className="absolute top-2 right-2 flex items-center justify-center w-5 h-5 rounded-full bg-primary">
        <Check className="w-3 h-3 text-white" />
      </span>
    )}
    <div className="flex items-center gap-3 pr-6">
      <div className="w-9 h-9 rounded-lg bg-primary/10 flex items-center justify-center flex-shrink-0">
        <BookOpen className="w-4 h-4 text-primary" />
      </div>
      <div className="min-w-0">
        <p className="text-sm font-semibold text-text truncate">{skill.name}</p>
        <p className="text-xs text-textMuted truncate mt-0.5">{skill.description}</p>
      </div>
    </div>
  </button>
);

const AddSkillsModal: React.FC<AddSkillsModalProps> = ({
  isOpen,
  allSkills,
  alreadySelectedIds,
  onCommit,
  onDiscard,
}) => {
  const [pendingIds, setPendingIds] = useState<Set<string>>(new Set());

  useEffect(() => {
    if (isOpen) {
      setPendingIds(new Set(alreadySelectedIds));
    }
  }, [isOpen, alreadySelectedIds]);

  useEffect(() => {
    if (!isOpen) return;
    document.body.style.overflow = 'hidden';
    return () => { document.body.style.overflow = ''; };
  }, [isOpen]);

  const handleEscape = useCallback(
    (e: KeyboardEvent) => { if (e.key === 'Escape') onDiscard(); },
    [onDiscard]
  );

  useEffect(() => {
    if (!isOpen) return;
    window.addEventListener('keydown', handleEscape);
    return () => window.removeEventListener('keydown', handleEscape);
  }, [isOpen, handleEscape]);

  const toggleSkill = (id: string) => {
    setPendingIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleCommit = () => onCommit(Array.from(pendingIds));

  const selectedCount = pendingIds.size;

  if (!isOpen) return null;

  return createPortal(
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4"
      role="dialog"
      aria-modal="true"
      aria-label="Select skills"
    >
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={onDiscard}
      />

      {/* Modal panel */}
      <div className="relative z-10 w-full max-w-2xl bg-surface border border-border rounded-xl shadow-2xl flex flex-col max-h-[80vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-border flex-shrink-0">
          <div className="flex items-center gap-2">
            <BookOpen className="w-5 h-5 text-primary" />
            <h2 className="text-lg font-semibold text-text">Select Skills</h2>
          </div>
          <button
            type="button"
            onClick={onDiscard}
            className="text-textMuted hover:text-text transition-colors p-1 rounded hover:bg-surfaceHighlight"
            aria-label="Close"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-6">
          {allSkills.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-12 text-textMuted">
              <BookOpen className="w-10 h-10 mb-3 opacity-40" />
              <p className="text-sm italic">No skills available in this workspace.</p>
              <p className="text-xs mt-1 opacity-70">Create some skills first, then assign them to agents.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              {allSkills.map(skill => (
                <SkillPickerCard
                  key={skill.id}
                  skill={skill}
                  selected={pendingIds.has(skill.id)}
                  onToggle={toggleSkill}
                />
              ))}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-border flex-shrink-0">
          <button
            type="button"
            onClick={onDiscard}
            className="px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleCommit}
            className="px-5 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors flex items-center gap-2 shadow-lg shadow-primary/20 hover:shadow-[0_0_20px_rgba(99,102,241,0.2)]"
          >
            <Check className="w-4 h-4" />
            {selectedCount === 0 ? 'Add None' : `Add ${selectedCount} Skill${selectedCount !== 1 ? 's' : ''}`}
          </button>
        </div>
      </div>
    </div>,
    document.body
  );
};

export default AddSkillsModal;
