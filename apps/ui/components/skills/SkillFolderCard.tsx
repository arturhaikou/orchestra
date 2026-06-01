import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Folder, Pencil, Trash2, BookOpen, ChevronDown, ChevronUp, AlertTriangle } from 'lucide-react';
import { SkillFolder } from '../../types';
import { useFolderSkills } from '../../hooks/useFolderSkills';

interface SkillFolderCardProps {
  skillFolder: SkillFolder;
  workspaceId: string;
  onDelete: (skillFolder: SkillFolder) => void;
}

const SkillFolderCard: React.FC<SkillFolderCardProps> = ({ skillFolder, workspaceId, onDelete }) => {
  const navigate = useNavigate();
  const { skills, isLoading, hasError, loadSkills } = useFolderSkills();
  const [expanded, setExpanded] = useState(false);

  const formattedDate = skillFolder.updatedAt
    ? new Date(skillFolder.updatedAt).toLocaleDateString(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
      })
    : null;

  const handleViewSkills = () => {
    if (!expanded) {
      loadSkills(workspaceId, skillFolder.id);
    }
    setExpanded(prev => !prev);
  };

  return (
    <div className="bg-surface border border-border rounded-xl p-5 flex flex-col gap-4 hover:border-primary/40 transition-colors group h-full">
      {/* Header */}
      <div className="flex items-start gap-3">
        <div className="w-9 h-9 rounded-lg bg-primary/10 flex items-center justify-center flex-shrink-0 mt-0.5">
          <Folder className="w-4 h-4 text-primary" />
        </div>
        <div className="flex-1 min-w-0">
          <h3 className="text-sm font-semibold text-text truncate">{skillFolder.name}</h3>
          {formattedDate && (
            <p className="text-[10px] text-textMuted mt-0.5">Updated {formattedDate}</p>
          )}
        </div>
      </div>

      {/* Folder path */}
      <p className="text-xs text-textMuted font-mono bg-background/50 px-2 py-1.5 rounded-md truncate" title={skillFolder.folderPath}>
        {skillFolder.folderPath}
      </p>

      {/* View Skills section */}
      {expanded && (
        <div className="border-t border-border pt-3 space-y-2">
          {isLoading && (
            <p className="text-xs text-textMuted italic">Loading skills...</p>
          )}
          {hasError && (
            <div className="flex items-center gap-1.5 text-xs text-red-400">
              <AlertTriangle className="w-3.5 h-3.5" />
              <span>Failed to load skills from folder.</span>
            </div>
          )}
          {!isLoading && !hasError && skills.length === 0 && (
            <p className="text-xs text-textMuted italic">No skills found in this folder.</p>
          )}
          {!isLoading && !hasError && skills.map((skill, idx) => (
            <div key={idx} className="flex items-start gap-2">
              <BookOpen className="w-3.5 h-3.5 text-primary mt-0.5 shrink-0" />
              <div className="min-w-0">
                <p className="text-xs font-medium text-text">{skill.name}</p>
                {skill.description && (
                  <p className="text-[10px] text-textMuted line-clamp-2">{skill.description}</p>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center gap-2 pt-1 border-t border-border mt-auto">
        <button
          onClick={handleViewSkills}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-textMuted hover:text-primary hover:bg-primary/10 rounded-md transition-colors"
          aria-label={expanded ? 'Hide skills' : 'View skills'}
        >
          {expanded ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
          {expanded ? 'Hide Skills' : 'View Skills'}
        </button>
        <button
          onClick={() => navigate(`/workspaces/${workspaceId}/skill-folders/${skillFolder.id}/edit`)}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-textMuted hover:text-primary hover:bg-primary/10 rounded-md transition-colors"
          aria-label={`Edit ${skillFolder.name}`}
        >
          <Pencil className="w-3.5 h-3.5" />
          Edit
        </button>
        <button
          onClick={() => onDelete(skillFolder)}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium text-textMuted hover:text-red-400 hover:bg-red-500/10 rounded-md transition-colors ml-auto"
          aria-label={`Delete ${skillFolder.name}`}
        >
          <Trash2 className="w-3.5 h-3.5" />
          Delete
        </button>
      </div>
    </div>
  );
};

export default SkillFolderCard;
