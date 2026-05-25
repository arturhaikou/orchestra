import React, { useState, useRef, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { Bell } from 'lucide-react';
import { useAgentQuestions } from '../../hooks/useAgentQuestions';
import AgentNotificationsPanel from './AgentNotificationsPanel';

interface Props {
  workspaceId: string;
}

const AgentQuestionsBell: React.FC<Props> = ({ workspaceId }) => {
  const { questions, pendingCount, refetch } = useAgentQuestions(workspaceId);
  const [isPanelOpen, setIsPanelOpen] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (
        panelRef.current &&
        buttonRef.current &&
        !panelRef.current.contains(e.target as Node) &&
        !buttonRef.current.contains(e.target as Node)
      ) {
        setIsPanelOpen(false);
      }
    };

    if (isPanelOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      return () => document.removeEventListener('mousedown', handleClickOutside);
    }
  }, [isPanelOpen]);

  const getButtonPosition = () => {
    if (!buttonRef.current) return { top: 0, right: 0 };
    const rect = buttonRef.current.getBoundingClientRect();
    return {
      top: rect.bottom + 8,
      right: window.innerWidth - rect.right
    };
  };

  const panelPosition = getButtonPosition();

  return (
    <div className="relative">
      <button
        ref={buttonRef}
        onClick={() => setIsPanelOpen(!isPanelOpen)}
        className="relative text-textMuted hover:text-text transition-colors p-2 rounded-full hover:bg-surfaceHighlight"
        aria-label={`Pending questions: ${pendingCount}`}
        aria-expanded={isPanelOpen}
      >
        <Bell className="w-5 h-5" />
        {pendingCount > 0 && (
          <span className="absolute top-1 right-1 w-3 h-3 bg-red-500 rounded-full ring-2 ring-surface"></span>
        )}
      </button>

      {isPanelOpen && createPortal(
        <div ref={panelRef}>
          <AgentNotificationsPanel
            questions={questions}
            onRefresh={refetch}
            position={panelPosition}
          />
        </div>,
        document.body
      )}
    </div>
  );
};

export default AgentQuestionsBell;
