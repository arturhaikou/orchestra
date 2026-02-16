
import React from 'react';
import { Search, Bell, Sun, Moon, Menu } from 'lucide-react';

interface HeaderProps {
    activeView: string;
    isDarkMode: boolean;
    toggleTheme: () => void;
    toggleSidebar: () => void;
}

const Header: React.FC<HeaderProps> = ({ activeView, isDarkMode, toggleTheme, toggleSidebar }) => {
    return (
        <header className="h-16 bg-surface/50 backdrop-blur border-b border-border flex items-center justify-between px-4 md:px-6 shrink-0 z-20">
          <div className="flex items-center gap-3 md:gap-4 text-sm text-textMuted">
             <button 
                onClick={toggleSidebar}
                className="p-2 -ml-2 hover:bg-surfaceHighlight rounded-md text-text md:hidden"
                aria-label="Toggle Menu"
             >
                <Menu className="w-5 h-5" />
             </button>
             <span className="flex items-center gap-1 hover:text-text cursor-pointer truncate max-w-[150px] md:max-w-none">
                <span className="opacity-50 hidden sm:inline">Workspace /</span> 
                {activeView.charAt(0).toUpperCase() + activeView.slice(1)}
             </span>
          </div>

          <div className="flex items-center gap-2 md:gap-4">
             <div className="relative hidden sm:block">
                <Search className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-textMuted" />
                <input 
                  type="text" 
                  placeholder="Global search (Cmd+K)" 
                  className="bg-background border border-border rounded-full pl-9 pr-4 py-1.5 text-sm w-48 lg:w-64 focus:outline-none focus:border-primary transition-all text-text placeholder:text-textMuted/50" 
                />
             </div>

             <button className="sm:hidden p-2 text-textMuted hover:text-text transition-colors rounded-full hover:bg-surfaceHighlight">
                <Search className="w-5 h-5" />
             </button>
             
             <button 
                onClick={toggleTheme} 
                className="text-textMuted hover:text-text transition-colors p-2 rounded-full hover:bg-surfaceHighlight"
                title={isDarkMode ? "Switch to Light Mode" : "Switch to Dark Mode"}
             >
                {isDarkMode ? <Sun className="w-5 h-5" /> : <Moon className="w-5 h-5" />}
             </button>

             <button className="relative text-textMuted hover:text-text transition-colors p-2 rounded-full hover:bg-surfaceHighlight">
               <Bell className="w-5 h-5" />
               <span className="absolute top-2 right-2 w-2 h-2 bg-primary rounded-full ring-2 ring-surface"></span>
             </button>
          </div>
        </header>
    );
};

export default Header;
