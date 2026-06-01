/**
 * Filesystem browser service for the folder picker.
 * Communicates with the backend FileSystemController to list directories.
 */

export const fileSystemService = {
  /**
   * Gets the filesystem roots (drives on Windows, / on Unix).
   */
  async getRoots(): Promise<string[]> {
    try {
      const response = await fetch('/v1/filesystem/roots');
      if (!response.ok) {
        let errorMsg = `HTTP ${response.status}`;
        try {
          const error = await response.json();
          errorMsg = error.error || errorMsg;
        } catch {
          // Response is not JSON (e.g., HTML error page)
        }
        throw new Error(`Failed to retrieve filesystem roots: ${errorMsg}`);
      }
      return response.json();
    } catch (err) {
      throw new Error(
        err instanceof Error ? err.message : 'Failed to retrieve filesystem roots.'
      );
    }
  },

  /**
   * Gets the immediate subdirectories of the given absolute path.
   * @param path Absolute path to list children for
   */
  async getChildren(path: string): Promise<string[]> {
    try {
      const encodedPath = encodeURIComponent(path);
      const response = await fetch(`/v1/filesystem/children?path=${encodedPath}`);
      if (!response.ok) {
        let errorMsg = `HTTP ${response.status}`;
        try {
          const error = await response.json();
          if (response.status === 404) {
            errorMsg = `Directory not found: ${path}`;
          } else if (response.status === 403) {
            errorMsg = `Access denied: ${path}`;
          } else {
            errorMsg = error.error || errorMsg;
          }
        } catch {
          // Response is not JSON
          if (response.status === 404) {
            errorMsg = `Directory not found: ${path}`;
          } else if (response.status === 403) {
            errorMsg = `Access denied: ${path}`;
          }
        }
        throw new Error(errorMsg);
      }
      return response.json();
    } catch (err) {
      throw new Error(
        err instanceof Error ? err.message : 'Failed to list directory.'
      );
    }
  },
};
