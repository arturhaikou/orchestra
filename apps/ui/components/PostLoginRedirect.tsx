import React, { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { getWorkspaces } from '../services/workspaceService';
import { logout } from '../services/authService';

const PostLoginRedirect: React.FC = () => {
  const [redirectTo, setRedirectTo] = useState<string | null>(null);

  useEffect(() => {
    const resolve = async () => {
      try {
        const workspaces = await getWorkspaces();
        if (workspaces.length > 0) {
          const savedWsId = localStorage.getItem('nexus_active_workspace');
          const workspace = workspaces.find(w => w.id === savedWsId) || workspaces[0];
          setRedirectTo(`/workspaces/${workspace.id}/tickets`);
        } else {
          setRedirectTo('/workspaces/new');
        }
      } catch (error) {
        if (error instanceof Error && error.message === 'UNAUTHORIZED') {
          logout();
          setRedirectTo('/login');
        } else {
          setRedirectTo('/workspaces/new');
        }
      }
    };
    resolve();
  }, []);

  if (redirectTo) {
    return <Navigate to={redirectTo} replace />;
  }

  return (
    <div className="flex h-screen w-full items-center justify-center bg-background text-text">
      <Loader2 className="w-8 h-8 animate-spin text-primary" />
    </div>
  );
};

export default PostLoginRedirect;
