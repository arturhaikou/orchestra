import React, { useEffect, useState } from 'react';
import { Outlet, useParams, useNavigate } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import Sidebar from './Sidebar';
import Header from './Header';
import ExecutionToastContainer from './ExecutionToastContainer';
import { Workspace } from '../types';
import { getWorkspaces } from '../services/workspaceService';
import { connect as signalRConnect, disconnect as signalRDisconnect, switchWorkspace as signalRSwitchWorkspace } from '../services/signalRService';

interface WorkspaceLayoutProps {
  isDarkMode: boolean;
  toggleTheme: () => void;
  onLogout: () => void;
  onDeleteWorkspace: (ws: Workspace) => void;
}

const WorkspaceLayout: React.FC<WorkspaceLayoutProps> = ({
  isDarkMode,
  toggleTheme,
  onLogout,
  onDeleteWorkspace,
}) => {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const navigate = useNavigate();
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [accessDenied, setAccessDenied] = useState(false);
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  useEffect(() => {
    const fetchWorkspaces = async () => {
      setIsLoading(true);
      try {
        const data = await getWorkspaces();
        setWorkspaces(data);
        if (workspaceId && !data.find(w => w.id === workspaceId)) {
          setAccessDenied(true);
        }
      } catch (error) {
        console.error('Failed to load workspaces', error);
      } finally {
        setIsLoading(false);
      }
    };
    fetchWorkspaces();
  }, [workspaceId]);

  useEffect(() => {
    if (!workspaceId || accessDenied) return;

    signalRConnect(workspaceId).catch(err =>
      console.error('Failed to establish SignalR connection:', err)
    );

    return () => {
      signalRDisconnect(workspaceId).catch(err =>
        console.error('Failed to disconnect SignalR:', err)
      );
    };
  }, [workspaceId, accessDenied]);

  const handleSwitchWorkspace = (newId: string) => {
    if (workspaceId && newId !== workspaceId) {
      signalRSwitchWorkspace(workspaceId, newId).catch(err =>
        console.error('Failed to switch SignalR workspace:', err)
      );
    }
    localStorage.setItem('nexus_active_workspace', newId);
    navigate(`/workspaces/${newId}/tickets`);
  };

  if (isLoading) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-background text-text">
        <div className="flex flex-col items-center gap-4 p-4 text-center">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
          <p className="text-sm text-textMuted font-mono">Loading workspace...</p>
        </div>
      </div>
    );
  }

  if (accessDenied) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-background text-text">
        <div className="flex flex-col items-center gap-4 p-4 text-center">
          <p className="text-lg font-bold text-text">Access Denied</p>
          <p className="text-sm text-textMuted">You are not a member of this workspace.</p>
          <button
            onClick={() => navigate('/workspaces/new')}
            className="px-4 py-2 bg-primary text-white rounded-md hover:bg-primaryHover transition-colors"
          >
            Go to My Workspaces
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen bg-background text-text overflow-hidden selection:bg-primary/30">
      <Sidebar
        workspaces={workspaces}
        activeWorkspaceId={workspaceId || ''}
        onSwitchWorkspace={handleSwitchWorkspace}
        onDeleteWorkspace={onDeleteWorkspace}
        onLogout={onLogout}
        isOpen={isSidebarOpen}
        onClose={() => setIsSidebarOpen(false)}
      />

      <main className="flex-1 flex flex-col h-full overflow-hidden relative">
        <Header
          isDarkMode={isDarkMode}
          toggleTheme={toggleTheme}
          toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)}
          workspaceId={workspaceId || ''}
        />

        <div className="flex-1 overflow-auto p-4 md:p-6 scroll-smooth relative">
          <div className="pointer-events-none absolute inset-0 overflow-hidden">
            <div className="absolute -top-40 -right-32 h-96 w-96 rounded-full bg-primary/5 blur-[120px]" />
            <div className="absolute -bottom-40 -left-32 h-96 w-96 rounded-full bg-purple-500/5 blur-[120px]" />
          </div>
          <div className="max-w-7xl mx-auto h-full relative">
            <Outlet />
          </div>
        </div>
      </main>

      <ExecutionToastContainer />
    </div>
  );
};

export default WorkspaceLayout;
