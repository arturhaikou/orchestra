import React, { useState, useEffect, useRef } from 'react';
import { Bot } from 'lucide-react';
import { createPortal } from 'react-dom';
import { useGlobalAgentQuestions } from '../../hooks/useGlobalAgentQuestions';
import GlobalNotificationsPanel from './GlobalNotificationsPanel';

const BotWidget: React.FC = () => {
  const { questions, totalCount, removeQuestion } = useGlobalAgentQuestions();
  const [isPanelOpen, setIsPanelOpen] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!isPanelOpen) return;
    const handleClickOutside = (e: MouseEvent) => {
      const target = e.target as Node;
      if (
        panelRef.current &&
        buttonRef.current &&
        !panelRef.current.contains(target) &&
        !buttonRef.current.contains(target)
      ) {
        setIsPanelOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isPanelOpen]);

  return (
    <>
      <button
        ref={buttonRef}
        onClick={() => setIsPanelOpen(open => !open)}
        aria-label={totalCount > 0 ? `${totalCount} pending agent questions` : 'Agent questions'}
        aria-expanded={isPanelOpen}
        className="fixed bottom-6 right-6 z-[99] w-14 h-14 rounded-full bg-surface border border-border shadow-lg flex items-center justify-center hover:bg-surfaceHighlight transition-colors"
      >
        <Bot className="w-7 h-7 text-primary" />
        {totalCount > 0 && (
          <span className="absolute -top-1 -right-1 min-w-5 h-5 px-1 bg-red-500 rounded-full ring-2 ring-surface flex items-center justify-center text-[10px] font-bold text-white">
            {totalCount > 99 ? '99+' : totalCount}
          </span>
        )}
      </button>

      {isPanelOpen && createPortal(
        <div ref={panelRef}>
          <GlobalNotificationsPanel
            questions={questions}
            onQuestionAnswered={(id) => {
              removeQuestion(id);
              if (questions.length <= 1) setIsPanelOpen(false);
            }}
            onClose={() => setIsPanelOpen(false)}
          />
        </div>,
        document.body
      )}
    </>
  );
};

export default BotWidget;
