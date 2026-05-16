import React from 'react';
import { BookOpen } from 'lucide-react';
import { Skill } from '../../types';

interface SkillsSectionProps {
  skills?: Skill[];
}

const SkillsSection: React.FC<SkillsSectionProps> = ({ skills }) => {
  if (!skills || skills.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2">
      <div className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-1.5">
        <BookOpen className="w-3 h-3" /> Skills
      </div>
      <div className="flex flex-wrap gap-1.5">
        {skills.map(skill => (
          <span key={skill.id} className="text-[10px] bg-emerald-500/10 border border-emerald-500/20 text-emerald-400 px-2 py-0.5 rounded flex items-center gap-1">
            <BookOpen className="w-2.5 h-2.5" /> {skill.name}
          </span>
        ))}
      </div>
    </div>
  );
};

export default SkillsSection;
