import React from 'react';
import { Moon, Sun } from 'lucide-react';

interface PublicHeaderProps {
  isDarkMode: boolean;
  toggleTheme: () => void;
}

const PublicHeader: React.FC<PublicHeaderProps> = ({ isDarkMode, toggleTheme }) => {
  return (
    <header className="sticky top-0 z-50 shrink-0 border-b border-border bg-background/80 backdrop-blur-md">
      <div className="mx-auto flex max-w-screen-2xl items-center justify-between gap-4 px-4 py-4 sm:px-6">
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 items-center justify-center overflow-hidden rounded-lg border border-border bg-surface shadow-sm">
            <img
              src="/orchestra_logo.png"
              alt="Orchestra logo"
              className="h-full w-full object-contain"
            />
          </div>
          <span className="text-lg font-bold tracking-tight text-text">Orchestra</span>
        </div>

        <button
          onClick={toggleTheme}
          className="flex items-center justify-center rounded-lg border border-transparent bg-surface px-3 py-2 text-textMuted transition-colors hover:border-border hover:bg-surfaceHighlight hover:text-text"
          title={isDarkMode ? 'Switch to Light Mode' : 'Switch to Dark Mode'}
          aria-label={isDarkMode ? 'Switch to Light Mode' : 'Switch to Dark Mode'}
        >
          {isDarkMode ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}
        </button>
      </div>
    </header>
  );
};

export default PublicHeader;