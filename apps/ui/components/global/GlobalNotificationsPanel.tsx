import React, { useState } from 'react';
import { X } from 'lucide-react';
import { GlobalAgentQuestion, AgentQuestion, QuestionItem } from '../../types';
import AgentQuestionModal from '../notifications/AgentQuestionModal';

interface Props {
  questions: GlobalAgentQuestion[];
  onQuestionAnswered: (questionId: string) => void;
  onClose: () => void;
}

const GlobalNotificationsPanel: React.FC<Props> = ({ questions, onQuestionAnswered, onClose }) => {
  const [selectedQuestion, setSelectedQuestion] = useState<AgentQuestion | null>(null);

  const openQuestion = (q: GlobalAgentQuestion) => {
    let parsedItems: QuestionItem[] = [];
    try {
      parsedItems = JSON.parse(q.questionsJson);
    } catch {
      parsedItems = [];
    }

    const asAgentQuestion: AgentQuestion = {
      id: q.questionId,
      jobId: '',
      agentId: '',
      workspaceId: q.workspaceId,
      questions: parsedItems,
      status: 'Pending',
      createdAt: q.createdAt,
    };

    setSelectedQuestion(asAgentQuestion);
  };

  const handleAnswered = () => {
    if (selectedQuestion) {
      onQuestionAnswered(selectedQuestion.id);
    }
    setSelectedQuestion(null);
  };

  const byWorkspace = questions.reduce<Record<string, { name: string; items: GlobalAgentQuestion[] }>>(
    (acc, q) => {
      if (!acc[q.workspaceId]) {
        acc[q.workspaceId] = { name: q.workspaceName, items: [] };
      }
      acc[q.workspaceId].items.push(q);
      return acc;
    },
    {}
  );

  return (
    <>
      <div className="fixed bottom-24 right-6 w-96 bg-surface border border-border rounded-xl shadow-2xl z-50 overflow-hidden">
        <div className="flex items-center justify-between px-4 py-3 border-b border-border">
          <h3 className="text-sm font-bold text-text">
            Agent Questions
            {questions.length > 0 && (
              <span className="ml-2 text-xs font-normal text-textMuted">({questions.length} pending)</span>
            )}
          </h3>
          <button
            onClick={onClose}
            className="text-textMuted hover:text-text p-1 rounded hover:bg-surfaceHighlight transition-colors"
            aria-label="Close panel"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="max-h-[70vh] overflow-y-auto">
          {questions.length === 0 ? (
            <div className="p-8 text-center">
              <p className="text-sm text-textMuted">No pending questions</p>
            </div>
          ) : (
            Object.entries(byWorkspace).map(([wsId, { name, items }]) => (
              <div key={wsId}>
                <div className="px-4 py-2 bg-background/50 border-b border-border">
                  <span className="text-xs font-semibold text-textMuted uppercase tracking-wide">{name}</span>
                </div>
                {items.map(q => (
                  <div
                    key={q.questionId}
                    className="px-4 py-3 border-b border-border last:border-b-0 hover:bg-surfaceHighlight transition-colors"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div className="flex-1 min-w-0">
                        <p className="text-xs text-textMuted mb-0.5">{q.agentName}</p>
                        {q.ticketTitle && (
                          <p className="text-xs text-primary truncate mb-1">{q.ticketTitle}</p>
                        )}
                        <p className="text-sm text-text truncate">
                          {(() => {
                            try {
                              const items: QuestionItem[] = JSON.parse(q.questionsJson);
                              return items[0]?.question ?? 'Question';
                            } catch {
                              return 'Question';
                            }
                          })()}
                        </p>
                      </div>
                      <button
                        onClick={() => openQuestion(q)}
                        className="px-3 py-1.5 text-xs font-medium bg-primary hover:bg-primaryHover text-white rounded transition-colors whitespace-nowrap flex-shrink-0"
                      >
                        Answer
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            ))
          )}
        </div>
      </div>

      {selectedQuestion && (
        <AgentQuestionModal
          question={selectedQuestion}
          onClose={() => setSelectedQuestion(null)}
          onAnswered={handleAnswered}
        />
      )}
    </>
  );
};

export default GlobalNotificationsPanel;
