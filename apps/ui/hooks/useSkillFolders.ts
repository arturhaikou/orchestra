import { useState, useEffect, useCallback } from 'react';
import { SkillFolder } from '../types';
import { getSkillFolders, deleteSkillFolder } from '../services/skillFolderService';

interface UseSkillFoldersResult {
  skillFolders: SkillFolder[];
  isLoading: boolean;
  hasError: boolean;
  retry: () => void;
  removeSkillFolder: (id: string) => void;
  deleteSkillFolderById: (skillFolderId: string) => Promise<void>;
}

export const useSkillFolders = (workspaceId: string | undefined): UseSkillFoldersResult => {
  const [skillFolders, setSkillFolders] = useState<SkillFolder[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [hasError, setHasError] = useState(false);

  const fetchSkillFolders = useCallback(async () => {
    if (!workspaceId) return;
    setIsLoading(true);
    setHasError(false);
    try {
      const data = await getSkillFolders(workspaceId);
      setSkillFolders(data);
    } catch {
      setHasError(true);
    } finally {
      setIsLoading(false);
    }
  }, [workspaceId]);

  useEffect(() => {
    fetchSkillFolders();
  }, [fetchSkillFolders]);

  const removeSkillFolder = useCallback((id: string) => {
    setSkillFolders(prev => prev.filter(f => f.id !== id));
  }, []);

  const deleteSkillFolderById = useCallback(
    async (skillFolderId: string) => {
      await deleteSkillFolder(workspaceId!, skillFolderId);
      removeSkillFolder(skillFolderId);
    },
    [workspaceId, removeSkillFolder],
  );

  return { skillFolders, isLoading, hasError, retry: fetchSkillFolders, removeSkillFolder, deleteSkillFolderById };
};
