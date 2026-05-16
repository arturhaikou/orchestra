import { useState, useEffect, useCallback } from 'react';
import { Skill } from '../types';
import { getSkill } from '../services/skillService';

interface UseLoadSkillResult {
  skill: Skill | null;
  isLoading: boolean;
  loadError: string | null;
  retry: () => void;
}

export const useLoadSkill = (
  workspaceId: string | undefined,
  skillId: string | undefined
): UseLoadSkillResult => {
  const [skill, setSkill] = useState<Skill | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const fetchSkill = useCallback(async () => {
    if (!workspaceId || !skillId) return;
    setIsLoading(true);
    setLoadError(null);
    try {
      const data = await getSkill(workspaceId, skillId);
      setSkill(data);
    } catch (err) {
      setLoadError(err instanceof Error ? err.message : 'Failed to load skill');
    } finally {
      setIsLoading(false);
    }
  }, [workspaceId, skillId]);

  useEffect(() => {
    fetchSkill();
  }, [fetchSkill]);

  return { skill, isLoading, loadError, retry: fetchSkill };
};
