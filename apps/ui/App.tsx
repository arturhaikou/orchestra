
import React, { useState, useEffect } from 'react';
import Sidebar from './components/Sidebar';
import Header from './components/Header';
import Dashboard from './components/Dashboard';
import Integrations from './components/Integrations';
import TicketList from './components/TicketList';
import AgentsList from './components/AgentsList';
import JobsList from './components/JobsList';
import WorkflowBuilder from './components/WorkflowBuilder';
import Login from './components/Login';
import { Workspace, User } from './types';
import { getWorkspaces, createWorkspace, updateWorkspace, deleteWorkspace } from './services/workspaceService';
import { getToken, logout, getUser, updateUser, changePassword } from './services/authService';
import { validatePassword } from './utils/passwordValidator';
import { X, Loader2, AlertTriangle, Pencil, Save, Trash2, Mail, User as UserIcon, Lock, ShieldCheck, Eye, EyeOff } from 'lucide-react';

const App: React.FC = () => {
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
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [isProfileModalOpen, setIsProfileModalOpen] = useState(false);
  const [workspaceInAction, setWorkspaceInAction] = useState<Workspace | null>(null);
  
  const [workspaceNameInput, setWorkspaceNameInput] = useState('');
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
          setIsCreateModalOpen(true);
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

  const handleCreateWorkspace = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!workspaceNameInput.trim()) return;

    setIsProcessing(true);
    try {
      const newWorkspace = await createWorkspace(workspaceNameInput);
      setWorkspaces([...workspaces, newWorkspace]);
      setActiveWorkspaceId(newWorkspace.id);
      setWorkspaceNameInput('');
      setIsCreateModalOpen(false);
    } catch (error) {
      console.error("Failed to create workspace", error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleUpdateWorkspace = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!workspaceInAction || !workspaceNameInput.trim()) return;

    setIsProcessing(true);
    try {
      const updated = await updateWorkspace(workspaceInAction.id, workspaceNameInput);
      setWorkspaces(prev => prev.map(ws => ws.id === workspaceInAction.id ? updated : ws));
      setIsEditModalOpen(false);
      setWorkspaceInAction(null);
      setWorkspaceNameInput('');
    } catch (error) {
      console.error("Failed to update workspace", error);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleDeleteWorkspace = async () => {
    if (!workspaceInAction) return;

    setIsProcessing(true);
    try {
      await deleteWorkspace(workspaceInAction.id);
      const remaining = workspaces.filter(ws => ws.id !== workspaceInAction.id);
      setWorkspaces(remaining);
      
      if (activeWorkspaceId === workspaceInAction.id) {
        if (remaining.length > 0) {
          setActiveWorkspaceId(remaining[0].id);
        } else {
          setActiveWorkspaceId('');
          setIsCreateModalOpen(true);
        }
      }
      
      setIsDeleteModalOpen(false);
      setWorkspaceInAction(null);
    } catch (error) {
      console.error("Failed to delete workspace", error);
    } finally {
      setIsProcessing(false);
    }
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
    if (workspaces.length === 0 && !isLoading) {
       return (
         <div className="flex h-full items-center justify-center text-textMuted p-6 text-center">
           <p>Please create a workspace to continue using Orchestra.</p>
         </div>
       );
    }

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
        onCreateWorkspace={() => {
          setWorkspaceNameInput('');
          setIsCreateModalOpen(true);
        }}
        onEditWorkspace={(ws) => {
          setWorkspaceInAction(ws);
          setWorkspaceNameInput(ws.name);
          setIsEditModalOpen(true);
        }}
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
        />

        <div className="flex-1 overflow-auto p-4 md:p-6 scroll-smooth">
           <div className="max-w-7xl mx-auto h-full">
            {renderContent()}
           </div>
        </div>
      </main>

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

      {/* Create Workspace Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl overflow-hidden">
            <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50">
              <h3 className="text-lg font-bold text-text">
                {workspaces.length === 0 ? 'Welcome' : 'New Workspace'}
              </h3>
              {workspaces.length > 0 && (
                <button onClick={() => setIsCreateModalOpen(false)} className="text-textMuted hover:text-text transition-colors">
                  <X className="w-5 h-5" />
                </button>
              )}
            </div>
            <form onSubmit={handleCreateWorkspace} className="p-6 space-y-4">
              <div className="space-y-1.5">
                <label className="text-[10px] font-semibold text-textMuted uppercase">Workspace Name</label>
                <input 
                  type="text" 
                  value={workspaceNameInput}
                  onChange={(e) => setWorkspaceNameInput(e.target.value)}
                  placeholder="e.g., Engineering Alpha"
                  className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                  autoFocus
                />
              </div>
              <div className="pt-2 flex gap-3">
                 {workspaces.length > 0 && (
                   <button 
                     type="button" 
                     onClick={() => setIsCreateModalOpen(false)}
                     className="flex-1 px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
                     disabled={isProcessing}
                   >
                     Cancel
                   </button>
                 )}
                 <button 
                   type="submit" 
                   disabled={!workspaceNameInput.trim() || isProcessing}
                   className="flex-1 px-4 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-primary/20"
                 >
                   {isProcessing ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Create Workspace'}
                 </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit Workspace Modal */}
      {isEditModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl overflow-hidden">
            <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50">
              <h3 className="text-lg font-bold text-text flex items-center gap-2">
                <Pencil className="w-4 h-4 text-primary" /> Edit
              </h3>
              <button onClick={() => setIsEditModalOpen(false)} className="text-textMuted hover:text-text transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>
            <form onSubmit={handleUpdateWorkspace} className="p-6 space-y-4">
              <div className="space-y-1.5">
                <label className="text-[10px] font-semibold text-textMuted uppercase">New Name</label>
                <input 
                  type="text" 
                  value={workspaceNameInput}
                  onChange={(e) => setWorkspaceNameInput(e.target.value)}
                  placeholder="e.g., Engineering Gamma"
                  className="w-full bg-background border border-border rounded-md px-3 py-2 text-sm text-text focus:outline-none focus:border-primary shadow-sm"
                  autoFocus
                />
              </div>
              <div className="pt-2 flex gap-3">
                <button 
                  type="button" 
                  onClick={() => setIsEditModalOpen(false)}
                  className="flex-1 px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
                  disabled={isProcessing}
                >
                  Cancel
                </button>
                <button 
                  type="submit" 
                  disabled={!workspaceNameInput.trim() || isProcessing}
                  className="flex-1 px-4 py-2 bg-primary hover:bg-primaryHover text-white rounded-md text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-primary/20"
                >
                  {isProcessing ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Delete Workspace Modal */}
      {isDeleteModalOpen && workspaceInAction && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
          <div className="bg-surface border border-border w-full max-w-md rounded-xl shadow-2xl overflow-hidden">
            <div className="px-6 py-4 border-b border-border flex justify-between items-center bg-surfaceHighlight/50">
              <h3 className="text-lg font-bold text-text flex items-center gap-2">
                <Trash2 className="w-4 h-4 text-red-500" /> Delete
              </h3>
              <button onClick={() => setIsDeleteModalOpen(false)} className="text-textMuted hover:text-text transition-colors">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6 space-y-4 text-center">
              <div className="w-16 h-16 bg-red-500/10 rounded-full flex items-center justify-center mx-auto mb-2 text-red-500">
                <AlertTriangle className="w-8 h-8" />
              </div>
              <p className="text-sm text-text">
                Confirm delete <span className="font-bold">"{workspaceInAction.name}"</span>?
              </p>
              <div className="pt-2 flex gap-3">
                <button 
                  type="button" 
                  onClick={() => setIsDeleteModalOpen(false)}
                  className="flex-1 px-4 py-2 border border-border rounded-md text-sm font-medium text-text hover:bg-surfaceHighlight transition-colors"
                  disabled={isProcessing}
                >
                  Cancel
                </button>
                <button 
                  onClick={handleDeleteWorkspace}
                  disabled={isProcessing}
                  className="flex-1 px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-md text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 shadow-lg shadow-red-500/20"
                >
                  {isProcessing ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Confirm'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

    </div>
  );
};

export default App;
