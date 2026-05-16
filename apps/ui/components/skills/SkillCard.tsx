import React from 'react';
import { useNavigate } from 'react-router-dom';
import { BookOpen, Pencil, Trash2 } from 'lucide-react';
import { Skill } from '../../types';

interface SkillCardProps {
  skill: Skill;
  workspaceId: string;
  onDelete: (skill: Skill) => void;
}

const SkillCard: React.FC<SkillCardProps> = ({ skill, workspaceId, onDelete }) => {
  const navigate = useNavigate();

  const formattedDate = skill.updatedAt
    ? new Date(skill.updatedAt).toLocaleDateString(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
      })
    : null;

  return (
    <div className="bg-surface border border-border rounded-xl p-5 flex flex-col gap-4 hover:border-primary/40 transition-colors group">
      {/* Header */}
      <div className="flex items-start gap-3">
        <div className="w-9 h-9 rounded-lg bg-primary/10 flex items-center justify-center flex-shrink-0 mt-0.5">
          <BookOpen className="w-4 h-4 text-primary" />
        </div>
        <div className="flex-1 min-w-0">
          <h3 className="text-sm font-semibold text-text truncate">{skill.name}</h3>
          {formattedDate && (
            <p className="text-[10px] text-textMuted mt-0.5">Updated {formattedDate}</p>
          )}
        </div>
      </div>

      {/* Description */}
      <p className="text-xs text-textMuted line-clamp-3 leading-relaxed">
        {skill.description || <span className="italic">No description provided.</span>}
      </p>

      {/* Actions */}
      <div className="flex items-center gap-2 pt-1 border-t border-border mt-auto">
        <button
          onClick={() => navigate(`/workspaces/${workspaceId}/skills/${skill.id}/edit`)}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-textMuted hover:text-primary hover:bg-primary/10 rounded-md transition-colors"
          aria-label={`Edit ${skill.name}`}
        >
          <Pencil className="w-3.5 h-3.5" />
          Edit
        </button>
        <button
          onClick={() => onDelete(skill)}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-textMuted hover:text-red-400 hover:bg-red-500/10 rounded-md transition-colors ml-auto"
          aria-label={`Delete ${skill.name}`}
        >
          <Trash2 className="w-3.5 h-3.5" />
          Delete
        </button>
      </div>
    </div>
  );
};

export default SkillCard;
