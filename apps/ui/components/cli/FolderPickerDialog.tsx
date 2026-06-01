import React, { useState, useEffect } from 'react';
import { ChevronRight, FolderOpen, ArrowUp, Loader2, AlertTriangle } from 'lucide-react';
import { fileSystemService } from '../../services/fileSystemService';

interface FolderPickerDialogProps {
  initialPath?: string;
  onSelect: (fullPath: string) => void;
  onClose: () => void;
}

export const FolderPickerDialog: React.FC<FolderPickerDialogProps> = ({
  initialPath,
  onSelect,
  onClose,
}) => {
  const [currentPath, setCurrentPath] = useState<string | null>(null);
  const [children, setChildren] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Load roots or validate initial path on mount
  useEffect(() => {
    const loadInitial = async () => {
      try {
        setIsLoading(true);
        setError(null);

        // If an initial path was provided and looks valid, try to load its children
        if (initialPath && initialPath.trim()) {
          try {
            const subdirs = await fileSystemService.getChildren(initialPath);
            setCurrentPath(initialPath);
            setChildren(subdirs);
            return;
          } catch {
            // Initial path is invalid or inaccessible; fall through to roots
          }
        }

        // Load roots
        const roots = await fileSystemService.getRoots();
        setCurrentPath(null);
        setChildren(roots);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load filesystem.');
      } finally {
        setIsLoading(false);
      }
    };

    loadInitial();
  }, [initialPath]);

  const handleNavigateIn = async (path: string) => {
    try {
      setIsLoading(true);
      setError(null);
      const subdirs = await fileSystemService.getChildren(path);
      setCurrentPath(path);
      setChildren(subdirs);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load directory.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleNavigateUp = async () => {
    if (!currentPath) return;

    const parent = getParentPath(currentPath);
    if (parent === null) {
      // Go to roots
      try {
        setIsLoading(true);
        setError(null);
        const roots = await fileSystemService.getRoots();
        setCurrentPath(null);
        setChildren(roots);
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Failed to load filesystem.');
      } finally {
        setIsLoading(false);
      }
    } else {
      await handleNavigateIn(parent);
    }
  };

  const handleSelect = () => {
    if (currentPath !== null) {
      onSelect(currentPath);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-surface border border-border rounded-xl shadow-2xl flex flex-col w-full max-w-2xl max-h-96 overflow-hidden">
        {/* Header */}
        <div className="px-6 py-4 border-b border-border flex items-center justify-between">
          <h2 className="text-lg font-semibold text-text">Select Folder</h2>
          <button
            onClick={onClose}
            className="text-textMuted hover:text-text transition-colors"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {/* Breadcrumb Navigation */}
        {currentPath !== null && (
          <div className="px-6 py-3 bg-surfaceHighlight border-b border-border flex items-center gap-2 text-sm font-mono text-textMuted flex-wrap">
            <button
              onClick={() => setCurrentPath(null)}
              className="text-primary hover:underline cursor-pointer"
            >
              Root
            </button>
            {getPathSegments(currentPath).map((segment, idx, arr) => (
              <React.Fragment key={idx}>
                <ChevronRight size={14} />
                {idx === arr.length - 1 ? (
                  <span className="text-text">{segment}</span>
                ) : (
                  <button
                    onClick={() => handleNavigateIn(getPathUpToSegment(currentPath, idx))}
                    className="text-primary hover:underline cursor-pointer"
                  >
                    {segment}
                  </button>
                )}
              </React.Fragment>
            ))}
          </div>
        )}

        {/* Directory List */}
        <div className="flex-1 overflow-y-auto px-6 py-4">
          {isLoading ? (
            <div className="flex items-center justify-center h-full">
              <Loader2 className="w-6 h-6 text-primary animate-spin" />
            </div>
          ) : error ? (
            <div className="flex items-start gap-3 p-4 bg-red-500/10 border border-red-500/20 rounded-lg">
              <AlertTriangle size={16} className="text-red-400 flex-shrink-0 mt-0.5" />
              <p className="text-sm text-red-400">{error}</p>
            </div>
          ) : children.length === 0 ? (
            <p className="text-sm text-textMuted text-center py-8">No subdirectories found.</p>
          ) : (
            <div className="space-y-1">
              {children.map(childPath => (
                <button
                  key={childPath}
                  onClick={() => handleNavigateIn(childPath)}
                  className="w-full text-left flex items-center gap-3 px-3 py-2.5 rounded-lg hover:bg-surfaceHighlight transition-colors text-sm text-text"
                >
                  <FolderOpen size={16} className="text-primary flex-shrink-0" />
                  <span className="truncate font-mono text-xs">{getDisplayName(childPath)}</span>
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-border flex items-center justify-between gap-3 bg-surfaceHighlight">
          {currentPath !== null && (
            <button
              onClick={handleNavigateUp}
              className="flex items-center gap-2 px-3 py-2 text-sm text-textMuted hover:text-text border border-border rounded-lg hover:bg-surface transition-colors"
              disabled={isLoading}
            >
              <ArrowUp size={14} />
              Up
            </button>
          )}
          <div className="flex-1" />
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm text-textMuted hover:text-text border border-border rounded-lg hover:bg-surface transition-colors disabled:opacity-50"
            disabled={isLoading}
          >
            Cancel
          </button>
          <button
            onClick={handleSelect}
            disabled={currentPath === null || isLoading}
            className="flex items-center gap-2 px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primaryHover transition-colors disabled:opacity-50"
          >
            Select This Folder
          </button>
        </div>
      </div>
    </div>
  );
};

/**
 * Get path segments for breadcrumb display, excluding the root/drive.
 */
function getPathSegments(path: string): string[] {
  if (path === '/' || (path.length === 3 && path[1] === ':' && path[2] === '\\')) {
    return [];
  }

  // Windows: C:\Users\name → ['Users', 'name']
  // Unix: /home/user → ['home', 'user']
  const separator = path.includes('\\') ? '\\' : '/';
  let parts = path.split(separator).filter(p => p.length > 0);

  // Remove drive letter on Windows (C: part)
  if (path[1] === ':') {
    parts = parts.slice(1);
  }

  return parts;
}

/**
 * Get the parent directory path.
 */
function getParentPath(path: string): string | null {
  const separator = path.includes('\\') ? '\\' : '/';
  const parts = path.split(separator).filter(p => p.length > 0);

  if (parts.length === 0) return null;
  if (parts.length === 1 && path[1] === ':') return null; // Windows root

  parts.pop();

  if (parts.length === 0) {
    // Unix root or Windows root
    return path[1] === ':' ? `${path[0]}:\\` : '/';
  }

  if (path.startsWith('/')) {
    return '/' + parts.join('/');
  } else {
    return parts.join('\\') + '\\';
  }
}

/**
 * Get the path up to and including the segment at the given index.
 */
function getPathUpToSegment(path: string, segmentIndex: number): string {
  const separator = path.includes('\\') ? '\\' : '/';
  const parts = path.split(separator).filter(p => p.length > 0);

  if (path[1] === ':') {
    // Windows path: first part is drive, segments start from index 1
    const sliced = [parts[0], ...parts.slice(1, segmentIndex + 2)];
    return sliced.join('\\') + '\\';
  } else {
    // Unix path
    const sliced = parts.slice(0, segmentIndex + 1);
    return '/' + sliced.join('/');
  }
}

/**
 * Get just the folder name from a full path.
 */
function getDisplayName(path: string): string {
  const separator = path.includes('\\') ? '\\' : '/';
  const parts = path.split(separator).filter(p => p.length > 0);
  return parts[parts.length - 1] || path;
}
