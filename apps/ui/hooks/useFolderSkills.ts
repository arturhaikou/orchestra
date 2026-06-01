import { useState, useCallback } from 'react';
import { DiscoveredSkill } from '../types';
import { getSkillsInFolder } from '../services/skillFolderService';

interface UseFolderSkillsResult {
  skills: DiscoveredSkill[];
  isLoading: boolean;
  hasError: boolean;
  loadSkills: (workspaceId: string, skillFolderId: string) => Promise<void>;
}

export const useFolderSkills = (): UseFolderSkillsResult => {
  const [skills, setSkills] = useState<DiscoveredSkill[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);

  const loadSkills = useCallback(async (workspaceId: string, skillFolderId: string) => {
    setIsLoading(true);
    setHasError(false);
    try {
      const data = await getSkillsInFolder(workspaceId, skillFolderId);
      setSkills(data);
    } catch {
      setHasError(true);
    } finally {
      setIsLoading(false);
    }
  }, []);

  return { skills, isLoading, hasError, loadSkills };
};
