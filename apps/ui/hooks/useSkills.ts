import { useState, useEffect, useCallback } from 'react';
import { Skill } from '../types';
import { getSkills, deleteSkill } from '../services/skillService';

interface UseSkillsResult {
  skills: Skill[];
  isLoading: boolean;
  hasError: boolean;
  retry: () => void;
  removeSkill: (id: string) => void;
  deleteSkillById: (skillId: string) => Promise<void>;
}

export const useSkills = (workspaceId: string | undefined): UseSkillsResult => {
  const [skills, setSkills] = useState<Skill[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);

  const fetchSkills = useCallback(async () => {
    if (!workspaceId) return;
    setIsLoading(true);
    setHasError(false);
    try {
      const data = await getSkills(workspaceId);
      setSkills(data);
    } catch {
      setHasError(true);
    } finally {
      setIsLoading(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    fetchSkills();
  }, [fetchSkills]);

  const removeSkill = useCallback((id: string) => {
    setSkills(prev => prev.filter(s => s.id !== id));
  }, []);

  const deleteSkillById = useCallback(
    async (skillId: string) => {
      await deleteSkill(workspaceId!, skillId);
      removeSkill(skillId);
    },
    [workspaceId, removeSkill],
  );

  return { skills, isLoading, hasError, retry: fetchSkills, removeSkill, deleteSkillById };
};
