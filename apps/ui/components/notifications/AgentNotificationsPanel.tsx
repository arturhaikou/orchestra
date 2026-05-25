import React, { useState } from 'react';
import { AgentQuestion } from '../../types';
import AgentQuestionModal from './AgentQuestionModal';

interface Props {
  questions: AgentQuestion[];
  onRefresh: () => void;
  position?: { top: number; right: number };
}

const AgentNotificationsPanel: React.FC<Props> = ({ questions, onRefresh, position = { top: 0, right: 0 } }) => {
  const [selectedQuestion, setSelectedQuestion] = useState<AgentQuestion | null>(null);

  const handleQuestionClick = (q: AgentQuestion) => {
    setSelectedQuestion(q);
  };

  const handleCloseModal = () => {
    setSelectedQuestion(null);
  };

  const handleAnswered = () => {
    onRefresh();
    setSelectedQuestion(null);
  };

  return (
    <>
      <div
        className="fixed w-80 bg-surface border border-border rounded-lg shadow-lg z-50"
        style={{
          top: `${position.top}px`,
          right: `${position.right}px`
        }}
      >
        <div className="p-4 border-b border-border">
          <h3 className="text-sm font-bold text-text">Pending Questions ({questions.length})</h3>
        </div>

        {questions.length === 0 ? (
          <div className="p-8 text-center">
            <p className="text-sm text-textMuted">No pending questions</p>
          </div>
        ) : (
          <div className="max-h-96 overflow-y-auto">
            {questions.map((q) => (
              <div
                key={q.id}
                className="p-4 border-b border-border last:border-b-0 hover:bg-surfaceHighlight cursor-pointer transition-colors"
              >
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-text truncate">
                      {q.questions[0]?.question || 'Question'}
                    </p>
                    <p className="text-xs text-textMuted mt-1">Agent ID: {q.agentId}</p>
                  </div>
                  <button
                    onClick={() => handleQuestionClick(q)}
                    className="px-3 py-1.5 text-xs font-medium bg-primary hover:bg-primaryHover text-white rounded transition-colors whitespace-nowrap"
                  >
                    Answer
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {selectedQuestion && (
        <AgentQuestionModal
          question={selectedQuestion}
          onClose={handleCloseModal}
          onAnswered={handleAnswered}
        />
      )}
    </>
  );
};

export default AgentNotificationsPanel;
