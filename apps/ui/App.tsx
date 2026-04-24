
import React, { useState, useEffect } from 'react';
import { Routes, Route, useNavigate } from 'react-router-dom';
import CreateWorkspacePage from './components/pages/CreateWorkspacePage';
import EditWorkspacePage from './components/pages/EditWorkspacePage';
import Sidebar from './components/Sidebar';
import Header from './components/Header';
import Dashboard from './components/Dashboard';
import Integrations from './components/Integrations';
import TicketList from './components/TicketList';
import AgentsList from './components/AgentsList';
import JobsList from './components/JobsList';
import WorkflowBuilder from './components/WorkflowBuilder';
import Login from './components/Login';
import ToggleSwitch from './components/ToggleSwitch';
import WorkspaceModals from './components/WorkspaceModals/WorkspaceModals';
import { Workspace, User } from './types';
import { getWorkspaces, deleteWorkspace } from './services/workspaceService';
import { getToken, logout, getUser, updateUser, changePassword } from './services/authService';
import ExecutionToastContainer from './components/ExecutionToastContainer';
import { connect as signalRConnect, disconnect as signalRDisconnect, switchWorkspace as signalRSwitchWorkspace } from './services/signalRService';
import { validatePassword } from './utils/passwordValidator';
import { X, Loader2, AlertTriangle, Pencil, Save, Trash2, Mail, User as UserIcon, Lock, ShieldCheck, Eye, EyeOff } from 'lucide-react';

const App: React.FC = () => {
  const navigate = useNavigate();
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [isAuthChecking, setIsAuthChecking] = useState(true);
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  
  // Theme Management
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
  
  const [activeView, setActiveView] = useState(() => {
    const saved = localStorage.getItem('nexus_active_view');
    if (!saved || saved === 'dashboard' || saved === 'jobs') return 'tickets';
    return saved;
  });

  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [activeWorkspaceId, setActiveWorkspaceId] = useState<string>(() => {
    return localStorage.getItem('nexus_active_workspace') || '';
  });

  const [isLoading, setIsLoading] = useState(true);
  
  // Modal States
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [isProfileModalOpen, setIsProfileModalOpen] = useState(false);
  const [workspaceInAction, setWorkspaceInAction] = useState<Workspace | null>(null);
  
  const [profileInput, setProfileInput] = useState({ 
    name: '', 
    email: '', 
    currentPassword: '', 
    newPassword: '', 
    confirmPassword: '' 
  });
  const [showPasswords, setShowPasswords] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);

  // Persist View & Workspace changes
  useEffect(() => {
    localStorage.setItem('nexus_active_view', activeView);
  }, [activeView]);

  useEffect(() => {
    if (activeWorkspaceId) {
      localStorage.setItem('nexus_active_workspace', activeWorkspaceId);
    }
  }, [activeWorkspaceId]);

  useEffect(() => {
    if (!isAuthenticated || !activeWorkspaceId) return;

    let previousWorkspaceId: string | null = null;

    const connectToWorkspace = async () => {
      try {
        if (previousWorkspaceId && previousWorkspaceId !== activeWorkspaceId) {
          await signalRSwitchWorkspace(previousWorkspaceId, activeWorkspaceId);
        } else {
          await signalRConnect(activeWorkspaceId);
        }
        previousWorkspaceId = activeWorkspaceId;
      } catch (err) {
        console.error('Failed to establish SignalR connection:', err);
      }
    };

    connectToWorkspace();

    return () => {
      if (activeWorkspaceId) {
        signalRDisconnect(activeWorkspaceId).catch((err) =>
          console.error('Failed to disconnect SignalR:', err),
        );
      }
    };
  }, [isAuthenticated, activeWorkspaceId]);

  // Check authentication status on mount
  useEffect(() => {
    const token = getToken();
    if (token) {
      setIsAuthenticated(true);
    }
    setIsAuthChecking(false);
  }, []);

  useEffect(() => {
    if (!isAuthenticated) return;

    const fetchWorkspaces = async () => {
      setIsLoading(true);
      try {
        const data = await getWorkspaces();
        setWorkspaces(data);
        if (data.length > 0) {
          const savedWsId = localStorage.getItem('nexus_active_workspace');
          const workspaceExists = data.find(w => w.id === savedWsId);
          setActiveWorkspaceId(workspaceExists ? workspaceExists.id : data[0].id);
        } else {
          navigate('/workspaces/new');
        }
      } catch (error) {
        console.error("Failed to load workspaces", error);
      } finally {
        setIsLoading(false);
      }
    };
    fetchWorkspaces();
  }, [isAuthenticated]);

  const handleLogin = () => {
    setIsAuthenticated(true);
  };

  const handleLogout = () => {
    logout();
    setIsAuthenticated(false);
    setWorkspaces([]);
    setActiveWorkspaceId('');
    setActiveView('tickets');
    setIsSidebarOpen(false);
  };



  const handleOpenProfile = () => {
      const user = getUser();
      if (user) {
          setProfileInput({ 
            name: user.name, 
            email: user.email,
            currentPassword: '',
            newPassword: '',
            confirmPassword: ''
          });
          setProfileError(null);
          setIsProfileModalOpen(true);
      }
  };

  const handleSaveProfile = async (e: React.FormEvent) => {
      e.preventDefault();
      setProfileError(null);

      // Validate Personal Info
      if (!profileInput.name.trim() || !profileInput.email.trim()) {
          setProfileError("Name and Email are required.");
          return;
      }

      // Validate Password Change
      const isChangingPassword = profileInput.newPassword || profileInput.confirmPassword || profileInput.currentPassword;
      if (isChangingPassword) {
          if (!profileInput.currentPassword) {
              setProfileError("Current password is required to make security changes.");
              return;
          }
          if (profileInput.newPassword !== profileInput.confirmPassword) {
              setProfileError("New passwords do not match.");
              return;
          }
          
          // Use the password validator utility
          const passwordValidation = validatePassword(profileInput.newPassword);
          if (!passwordValidation.isValid) {
              setProfileError(passwordValidation.errors[0]); // Show first validation error
              return;
          }
      }

      setIsProcessing(true);
      try {
          // Update basic info
          await updateUser({ name: profileInput.name, email: profileInput.email });
          
          // Change password if requested
          if (isChangingPassword) {
              await changePassword(profileInput.currentPassword, profileInput.newPassword);
          }
          
          setIsProfileModalOpen(false);
      } catch (error: any) {
          setProfileError(error.message || "Failed to update profile.");
          console.error("Failed to update profile", error);
      } finally {
          setIsProcessing(false);
      }
  };

  const renderContent = () => {
    switch (activeView) {
      case 'integrations': return <Integrations workspaceId={activeWorkspaceId} />;
      case 'tickets': return <TicketList workspaceId={activeWorkspaceId} onNavigateToTickets={() => setActiveView('tickets')} />;
      case 'agents': return <AgentsList workspaceId={activeWorkspaceId} />;
      // case 'workflows': return <WorkflowBuilder workspaceId={activeWorkspaceId} isDarkMode={isDarkMode} />;
      default: return <TicketList workspaceId={activeWorkspaceId} onNavigateToTickets={() => setActiveView('tickets')} />;
    }
  };

  if (isAuthChecking) {
     return (
       <div className="flex h-screen w-full items-center justify-center bg-background text-text">
         <Loader2 className="w-8 h-8 animate-spin text-primary" />
       </div>
     );
  }

  if (!isAuthenticated) {
    return <Login onLogin={handleLogin} isDarkMode={isDarkMode} toggleTheme={toggleTheme} />;
  }

  if (isLoading && workspaces.length === 0) {
    return (
      <div className="flex h-screen w-full items-center justify-center bg-background text-text">
        <div className="flex flex-col items-center gap-4 p-4 text-center">
          <Loader2 className="w-8 h-8 animate-spin text-primary" />
          <p className="text-sm text-textMuted font-mono">Initializing Orchestra Environment...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen bg-background text-text overflow-hidden selection:bg-primary/30">
      
      <Sidebar 
        activeView={activeView} 
        setActiveView={setActiveView} 
        workspaces={workspaces}
        activeWorkspaceId={activeWorkspaceId}
        onSwitchWorkspace={setActiveWorkspaceId}
        onDeleteWorkspace={(ws) => {
          setWorkspaceInAction(ws);
          setIsDeleteModalOpen(true);
        }}
        onEditProfile={handleOpenProfile}
        onLogout={handleLogout}
        isOpen={isSidebarOpen}
        onClose={() => setIsSidebarOpen(false)}
      />

      <main className="flex-1 flex flex-col h-full overflow-hidden relative">
        <Header 
            activeView={activeView} 
            isDarkMode={isDarkMode} 
            toggleTheme={toggleTheme} 
            toggleSidebar={() => setIsSidebarOpen(!isSidebarOpen)}
            workspaceId={activeWorkspaceId}
        />

        <div className="flex-1 overflow-auto p-4 md:p-6 scroll-smooth">
           <div className="max-w-7xl mx-auto h-full">
            <Routes>
              <Route
                path="/workspaces/new"
                element={
                  <CreateWorkspacePage
                    hasExistingWorkspaces={workspaces.length > 0}
                    onWorkspaceCreated={(workspace) => {
                      setWorkspaces(prev => [...prev, workspace]);
                      setActiveWorkspaceId(workspace.id);
                    }}
                  />
                }
              />
              <Route
                path="/workspaces/:workspaceId/edit"
                element={
                  <EditWorkspacePage
                    workspaces={workspaces}
                    onWorkspaceUpdated={(updatedWorkspace) => {
                      setWorkspaces((prev) =>
                        prev.map((w) =>
                          w.id === updatedWorkspace.id ? updatedWorkspace : w
                        )
                      );
                    }}
                  />
                }
              />
              <Route path="*" element={renderContent()} />
            </Routes>
           </div>
        </div>
      </main>

      {activeWorkspaceId && (
        <ExecutionToastContainer workspaceId={activeWorkspaceId} activeView={activeView} />
      )}

      {/* Profile Settings Modal - Expanded */}
      {isProfileModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-lg rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[95vh]">
            <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50">
              <h3 className="text-lg font-bold text-text flex items-center gap-2">
                <UserIcon className="w-5 h-5 text-primary" /> Profile Settings
              </h3>
              <button onClick={() => setIsProfileModalOpen(false)} className="text-textMuted hover:text-text transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <form onSubmit={handleSaveProfile} className="p-6 space-y-6 overflow-y-auto custom-scrollbar">
              {profileError && (
                <div className="p-3 bg-red-500/10 border border-red-500/20 rounded text-red-500 text-xs font-medium flex items-center gap-2">
                  <AlertTriangle className="w-4 h-4 shrink-0" />
                  {profileError}
                </div>
              )}

              {/* Personal Info Section */}
              <div className="space-y-4">
                <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-2">
                  <UserIcon className="w-3.5 h-3.5" /> Personal Information
                </h4>
                <div className="grid grid-cols-1 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[10px] font-semibold text-textMuted uppercase">Display Name</label>
                    <div className="relative group">
                        <UserIcon className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                        <input 
                        type="text" 
                        value={profileInput.name}
                        onChange={(e) => setProfileInput({...profileInput, name: e.target.value})}
                        placeholder="John Doe"
                        className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all"
                        />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[10px] font-semibold text-textMuted uppercase">Email Address</label>
                    <div className="relative group">
                        <Mail className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                        <input 
                        type="email" 
                        value={profileInput.email}
                        onChange={(e) => setProfileInput({...profileInput, email: e.target.value})}
                        placeholder="john@example.com"
                        className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all"
                        />
                    </div>
                  </div>
                </div>
              </div>

              {/* Security Section */}
              <div className="space-y-4 pt-4 border-t border-border/50">
                <div className="flex items-center justify-between">
                  <h4 className="text-[10px] font-bold text-textMuted uppercase tracking-widest flex items-center gap-2">
                    <ShieldCheck className="w-3.5 h-3.5" /> Security & Password
                  </h4>
                  <button 
                    type="button"
                    onClick={() => setShowPasswords(!showPasswords)}
                    className="text-[10px] font-bold text-primary hover:text-primaryHover transition-colors flex items-center gap-1"
                  >
                    {showPasswords ? <EyeOff className="w-3 h-3" /> : <Eye className="w-3 h-3" />}
                    {showPasswords ? 'Hide Inputs' : 'Show Inputs'}
                  </button>
                </div>
                
                <div className="space-y-4">
                  <div className="space-y-1.5">
                    <label className="text-[10px] font-semibold text-textMuted uppercase">Current Password</label>
                    <div className="relative group">
                        <Lock className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                        <input 
                        type={showPasswords ? "text" : "password"}
                        value={profileInput.currentPassword}
                        onChange={(e) => setProfileInput({...profileInput, currentPassword: e.target.value})}
                        placeholder="Required for any changes"
                        className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all font-mono"
                        />
                    </div>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className="text-[10px] font-semibold text-textMuted uppercase">New Password</label>
                      <div className="relative group">
                          <Lock className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                          <input 
                          type={showPasswords ? "text" : "password"}
                          value={profileInput.newPassword}
                          onChange={(e) => setProfileInput({...profileInput, newPassword: e.target.value})}
                          placeholder="Min 8 chars, 1 uppercase, 1 digit, 1 special"
                          className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all font-mono"
                          />
                      </div>
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[10px] font-semibold text-textMuted uppercase">Confirm New</label>
                      <div className="relative group">
                          <Lock className="absolute left-3 top-2.5 w-4 h-4 text-textMuted group-focus-within:text-primary transition-colors" />
                          <input 
                          type={showPasswords ? "text" : "password"}
                          value={profileInput.confirmPassword}
                          onChange={(e) => setProfileInput({...profileInput, confirmPassword: e.target.value})}
                          placeholder="Repeat new password"
                          className="w-full bg-background border border-border rounded-md px-3 pl-10 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm transition-all font-mono"
                          />
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <div className="pt-4 flex flex-col sm:flex-row gap-3">
                 <button 
                   type="button" 
                   onClick={() => setIsProfileModalOpen(false)}
                   className="flex-1 px-4 py-2.5 border border-border rounded-md text-sm font-bold uppercase tracking-widest text-textMuted hover:text-text hover:bg-surfaceHighlight transition-colors"
                   disabled={isProcessing}
                 >
                   Cancel
                 </button>
                 <button 
                   type="submit" 
                   disabled={isProcessing}
                   className="flex-1 px-4 py-2.5 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-bold uppercase tracking-widest transition-all disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-primary/20"
                 >
                   {isProcessing ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />} 
                   {isProcessing ? 'Saving...' : 'Save Profile'}
                 </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Workspace Modals */}
      <WorkspaceModals
        isDeleteModalOpen={isDeleteModalOpen}
        workspaceInAction={workspaceInAction}
        onDeleteModalClose={() => {
          setIsDeleteModalOpen(false);
          setWorkspaceInAction(null);
        }}
        onWorkspaceDeleted={(id) => {
          const remaining = workspaces.filter(ws => ws.id !== id);
          setWorkspaces(remaining);
          if (activeWorkspaceId === id) {
            if (remaining.length > 0) {
              setActiveWorkspaceId(remaining[0].id);
            } else {
              setActiveWorkspaceId('');
              navigate('/workspaces/new');
            }
          }
        }}
      />

    </div>
  );
};

export default App;
