
import React, { useState, useRef, useEffect } from 'react';
import { 
  Layers, 
  Ticket as TicketIcon, 
  Bot, 
  GitBranch, 
  Cpu, 
  Settings,
  ChevronDown,
  Plus,
  Check,
  LogOut,
  Pencil,
  Trash2,
  X,
  User as UserIcon
} from 'lucide-react';
import { Workspace, User } from '../types';
import { getUser } from '../services/authService';

interface SidebarProps {
  activeView: string;
  setActiveView: (view: string) => void;
  workspaces: Workspace[];
  activeWorkspaceId: string;
  onSwitchWorkspace: (id: string) => void;
  onCreateWorkspace: () => void;
  onEditWorkspace: (ws: Workspace) => void;
  onDeleteWorkspace: (ws: Workspace) => void;
  onEditProfile: () => void;
  onLogout: () => void;
  isOpen: boolean;
  onClose: () => void;
}

const SidebarItem: React.FC<{ 
  icon: React.FC<any>; 
  label: string; 
  active: boolean; 
  onClick: () => void 
}> = ({ icon: Icon, label, active, onClick }) => (
  <button 
    onClick={onClick}
    className={`w-full flex items-center gap-3 px-4 py-3 rounded-md transition-all duration-200 group
      ${active 
        ? 'bg-primary/10 text-primary border-r-2 border-primary' 
        : 'text-textMuted hover:bg-surfaceHighlight hover:text-text'
      }`}
  >
    <Icon className={`w-5 h-5 ${active ? 'text-primary' : 'text-textMuted group-hover:text-text'}`} />
    <span className="font-medium text-sm">{label}</span>
  </button>
);

const Sidebar: React.FC<SidebarProps> = ({ 
  activeView, 
  setActiveView, 
  workspaces, 
  activeWorkspaceId, 
  onSwitchWorkspace, 
  onCreateWorkspace,
  onEditWorkspace,
  onDeleteWorkspace,
  onEditProfile,
  onLogout,
  isOpen,
  onClose
}) => {
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const sidebarRef = useRef<HTMLElement>(null);
  const activeWorkspace = workspaces.find(w => w.id === activeWorkspaceId);
  const currentUser = getUser() as User;

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const getInitials = (name: string) => {
      return name.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2);
  };

  return (
    <>
      {/* Mobile Backdrop */}
      {isOpen && (
        <div 
            className="fixed inset-0 bg-black/60 backdrop-blur-sm z-40 md:hidden animate-fade-in"
            onClick={onClose}
        />
      )}

      <aside 
        ref={sidebarRef}
        className={`fixed inset-y-0 left-0 z-50 w-64 bg-surface border-r border-border flex flex-col transform transition-transform duration-300 ease-in-out md:relative md:translate-x-0 ${isOpen ? 'translate-x-0' : '-translate-x-full'}`}
      >
        {/* Sidebar Header */}
        <div className="p-4 border-b border-border relative">
          <div className="flex items-center justify-between mb-2 md:hidden">
            <span className="text-[10px] text-textMuted uppercase font-bold tracking-widest">Navigation</span>
            <button onClick={onClose} className="p-1 text-textMuted hover:text-text">
                <X className="w-4 h-4" />
            </button>
          </div>
          
          <button 
            onClick={() => setIsDropdownOpen(!isDropdownOpen)}
            className="w-full flex items-center gap-3 p-2 rounded-lg hover:bg-surfaceHighlight transition-colors text-left"
          >
            <div className="w-8 h-8 bg-primary rounded-lg flex items-center justify-center shrink-0 shadow-lg shadow-primary/20">
              <Cpu className="w-5 h-5 text-white" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-[10px] text-textMuted uppercase font-bold tracking-wider opacity-60">Orchestra</p>
              <div className="flex items-center gap-1">
                 <h1 className="text-sm font-bold tracking-tight truncate text-text">{activeWorkspace?.name}</h1>
                 <ChevronDown className={`w-3 h-3 text-textMuted transition-transform ${isDropdownOpen ? 'rotate-180' : ''}`} />
              </div>
            </div>
          </button>

          {/* Dropdown Menu */}
          {isDropdownOpen && (
            <div ref={dropdownRef} className="absolute top-full left-2 right-2 mt-1 bg-surface border border-border rounded-lg shadow-xl overflow-hidden z-50 animate-fade-in origin-top">
              <div className="p-1 max-h-64 overflow-y-auto">
                <div className="px-2 py-1.5 text-xs font-semibold text-textMuted uppercase">Switch Workspace</div>
                {workspaces.map(ws => (
                  <div 
                    key={ws.id}
                    className={`group/ws-item w-full flex items-center justify-between px-2 py-2 text-sm rounded-md transition-colors ${ws.id === activeWorkspaceId ? 'bg-primary/10 text-primary' : 'text-text hover:bg-surfaceHighlight'}`}
                  >
                    <button
                      onClick={() => {
                        onSwitchWorkspace(ws.id);
                        setIsDropdownOpen(false);
                      }}
                      className="flex-1 text-left truncate"
                    >
                      {ws.name}
                    </button>
                    <div className="flex items-center gap-1 opacity-0 group-hover/ws-item:opacity-100 transition-opacity">
                      <button 
                        onClick={(e) => {
                          e.stopPropagation();
                          onEditWorkspace(ws);
                        }}
                        className="p-1 text-textMuted hover:text-primary transition-colors"
                        title="Edit Workspace"
                      >
                        <Pencil className="w-3 h-3" />
                      </button>
                      <button 
                        onClick={(e) => {
                          e.stopPropagation();
                          onDeleteWorkspace(ws);
                        }}
                        className="p-1 text-textMuted hover:text-red-500 transition-colors"
                        title="Delete Workspace"
                      >
                        <Trash2 className="w-3 h-3" />
                      </button>
                      {ws.id === activeWorkspaceId && <Check className="w-3 h-3 ml-1" />}
                    </div>
                  </div>
                ))}
              </div>
              <div className="p-1 border-t border-border bg-surfaceHighlight/30">
                 <button 
                   onClick={() => {
                     onCreateWorkspace();
                     setIsDropdownOpen(false);
                   }}
                   className="w-full flex items-center gap-2 px-2 py-2 text-sm text-textMuted hover:text-text hover:bg-surfaceHighlight rounded-md transition-colors"
                 >
                   <Plus className="w-4 h-4" /> Create Workspace
                 </button>
              </div>
            </div>
          )}
        </div>

        <nav className="flex-1 p-4 space-y-2 overflow-y-auto">
          <SidebarItem icon={GitBranch} label="Integrations" active={activeView === 'integrations'} onClick={() => { setActiveView('integrations'); onClose(); }} />
          <SidebarItem icon={TicketIcon} label="Tickets" active={activeView === 'tickets'} onClick={() => { setActiveView('tickets'); onClose(); }} />
          <SidebarItem icon={Bot} label="Agents" active={activeView === 'agents'} onClick={() => { setActiveView('agents'); onClose(); }} />
          <SidebarItem icon={Layers} label="Workflows" active={activeView === 'workflows'} onClick={() => { setActiveView('workflows'); onClose(); }} />
        </nav>

        <div className="p-4 border-t border-border">
          <div className="flex items-center gap-3 px-2 py-2 rounded-md hover:bg-surfaceHighlight cursor-default transition-colors group">
            <div 
                className="w-8 h-8 rounded-full bg-gradient-to-tr from-primary to-purple-500 flex items-center justify-center text-[10px] font-bold text-white shadow-lg cursor-pointer"
                onClick={onEditProfile}
            >
              {getInitials(currentUser?.name || 'User')}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-text truncate">{currentUser?.name}</p>
              <p className="text-[10px] text-textMuted truncate">Account Settings</p>
            </div>
            <div className="flex gap-1">
               <button 
                onClick={onEditProfile}
                className="text-textMuted hover:text-primary transition-colors p-1" 
                title="Edit Profile"
               >
                  <Pencil className="w-4 h-4" />
               </button>
               <button onClick={onLogout} className="text-textMuted hover:text-red-400 transition-colors p-1" title="Log Out">
                  <LogOut className="w-4 h-4" />
               </button>
            </div>
          </div>
        </div>
      </aside>
    </>
  );
};

export default Sidebar;
