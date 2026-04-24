
import React, { useState, useEffect } from 'react';
import { Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import CreateWorkspacePage from './components/pages/CreateWorkspacePage';
import EditWorkspacePage from './components/pages/EditWorkspacePage';
import AgentCreatePage from './components/pages/AgentCreatePage';
import AgentEditPage from './components/pages/AgentEditPage';
import DeployBuiltInAgentPage from './components/pages/DeployBuiltInAgentPage';
import IntegrationCreatePage from './components/pages/IntegrationCreatePage';
import IntegrationEditPage from './components/pages/IntegrationEditPage';
import TicketCreatePage from './components/pages/TicketCreatePage';
import TicketDetailPage from './components/pages/TicketDetailPage';
import TicketEditPage from './components/pages/TicketEditPage';
import ProfileEditPage from './components/pages/ProfileEditPage';
import Integrations from './components/Integrations';
import TicketList from './components/TicketList';
import AgentsList from './components/AgentsList';
import Login from './components/Login';
import WorkspaceModals from './components/WorkspaceModals/WorkspaceModals';
import Toast from './components/Toast';
import { Workspace } from './types';
import { getToken, logout } from './services/authService';
import AuthGuard from './components/AuthGuard';
import WorkspaceLayout from './components/WorkspaceLayout';
import PostLoginRedirect from './components/PostLoginRedirect';
import { Loader2 } from 'lucide-react';

const App: React.FC = () => {
  const navigate = useNavigate();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isAuthChecking, setIsAuthChecking] = useState(true);

  const [isDarkMode, setIsDarkMode] = useState(() => {
    if (typeof window !== 'undefined') {
        const saved = localStorage.getItem('nexus_theme');
        if (saved) return saved === 'dark';
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }
    return true;
  });

  useEffect(() => {
    const root = window.document.documentElement;
    if (isDarkMode) {
        root.classList.add('dark');
        localStorage.setItem('nexus_theme', 'dark');
    } else {
        root.classList.remove('dark');
        localStorage.setItem('nexus_theme', 'light');
    }
  }, [isDarkMode]);

  const toggleTheme = () => setIsDarkMode(!isDarkMode);

  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [workspaceInAction, setWorkspaceInAction] = useState<Workspace | null>(null);
  const [appToast, setAppToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);

  useEffect(() => {
    const token = getToken();
    if (token) {
      setIsAuthenticated(true);
    }
    setIsAuthChecking(false);
  }, []);

  const handleLogin = () => {
    setIsAuthenticated(true);
  };

  const handleLogout = () => {
    logout();
    setIsAuthenticated(false);
    navigate('/login');
  };

  if (isAuthChecking) {
     return (
       <div className="flex h-screen w-full items-center justify-center bg-background text-text">
         <Loader2 className="w-8 h-8 animate-spin text-primary" />
       </div>
     );
  }

  return (
    <>
      <Routes>
        <Route path="/login" element={
          isAuthenticated
            ? <Navigate to="/" replace />
            : <Login onLogin={handleLogin} isDarkMode={isDarkMode} toggleTheme={toggleTheme} />
        } />

        <Route path="/workspaces/new" element={
          <AuthGuard>
            <CreateWorkspacePage
              hasExistingWorkspaces={true}
              onWorkspaceCreated={(workspace) => {
                navigate(`/workspaces/${workspace.id}/tickets`);
              }}
            />
          </AuthGuard>
        } />

        <Route path="/workspaces/:workspaceId" element={
          <AuthGuard>
            <WorkspaceLayout
              isDarkMode={isDarkMode}
              toggleTheme={toggleTheme}
              onLogout={handleLogout}
              onDeleteWorkspace={(ws) => {
                setWorkspaceInAction(ws);
                setIsDeleteModalOpen(true);
              }}
            />
          </AuthGuard>
        }>
          <Route path="tickets" element={<TicketList />} />
          <Route path="tickets/new" element={<TicketCreatePage />} />
          <Route path="tickets/:ticketId" element={<TicketDetailPage />} />
          <Route path="tickets/:ticketId/edit" element={<TicketEditPage />} />
          <Route path="agents/new" element={<AgentCreatePage />} />
          <Route path="agents/:agentId/edit" element={<AgentEditPage />} />
          <Route path="agents/deploy/:templateId" element={<DeployBuiltInAgentPage />} />
          <Route path="agents" element={<AgentsList />} />
          <Route path="integrations/new" element={<IntegrationCreatePage />} />
          <Route path="integrations/:integrationId/edit" element={<IntegrationEditPage />} />
          <Route path="integrations" element={<Integrations />} />
          <Route path="edit" element={<EditWorkspacePage />} />
          <Route path="profile" element={<ProfileEditPage />} />
          <Route path="*" element={<Navigate to="tickets" replace />} />
          <Route index element={<Navigate to="tickets" replace />} />
        </Route>

        <Route path="/" element={
          isAuthenticated ? (
            <AuthGuard><PostLoginRedirect /></AuthGuard>
          ) : (
            <Navigate to="/login" replace />
          )
        } />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>

      <WorkspaceModals
        isDeleteModalOpen={isDeleteModalOpen}
        workspaceInAction={workspaceInAction}
        onDeleteModalClose={() => {
          setIsDeleteModalOpen(false);
          setWorkspaceInAction(null);
        }}
        onWorkspaceDeleted={(id) => {
          setIsDeleteModalOpen(false);
          setWorkspaceInAction(null);
          navigate('/workspaces/new');
        }}
        onToast={(message, type) => {
          setAppToast({ message, type });
          setTimeout(() => setAppToast(null), 5000);
        }}
      />

      {appToast && (
        <Toast message={appToast.message} type={appToast.type} onClose={() => setAppToast(null)} />
      )}
    </>
  );
};

export default App;
