import React, { useState, useEffect, useRef } from 'react';
import { X, Loader2, AlertTriangle } from 'lucide-react';
import { AgentQuestion } from '../../types';
import { submitAnswers } from '../../services/agentQuestionService';
import { useModalAction } from '../../hooks/useModalAction';

type AnswerValue = string | string[];

interface Props {
  question: AgentQuestion;
  onClose: () => void;
  onAnswered: () => void;
}

const AgentQuestionModal: React.FC<Props> = ({ question, onClose, onAnswered }) => {
  const [answers, setAnswers] = useState<Record<string, AnswerValue>>({});
  const [customTexts, setCustomTexts] = useState<Record<string, string>>({});
  const triggerRef = useRef<HTMLElement | null>(null);

  useEffect(() => {
    triggerRef.current = document.activeElement as HTMLElement;
    setTimeout(() => {
      const firstInput = document.querySelector('textarea, input[type="radio"], input[type="checkbox"]') as HTMLElement;
      firstInput?.focus();
    }, 100);
  }, []);

  const handleAnswer = async () => {
    const finalAnswers = { ...answers };
    for (const [qIndex, text] of Object.entries(customTexts)) {
      if (text.trim()) {
        const answerKey = qIndex.includes('_custom') ? qIndex.replace('_custom', '') : qIndex;
        if (!finalAnswers[answerKey]) {
          finalAnswers[answerKey] = text;
        }
      }
    }
    await submitAnswers(question.id, finalAnswers);
    onAnswered();
    onClose();
  };

  const { execute, isLoading, error } = useModalAction(
    handleAnswer,
    onAnswered
  );

  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !isLoading) onClose();
    };
    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [onClose, isLoading]);



  const isAnswered = question.questions.every((q, idx) => {
    const answer = answers[`q${idx}`];
    if (q.type === 'Checkbox') return Array.isArray(answer) && answer.length > 0;
    return !!answer;
  });

  const handleTabTrap = (e: React.KeyboardEvent) => {
    if (e.key !== 'Tab') return;
    const focusableElements = document.querySelectorAll(
      'button:not([disabled]), textarea:not([disabled]), input:not([disabled])'
    );
    if (focusableElements.length === 0) return;
    const first = focusableElements[0];
    const last = focusableElements[focusableElements.length - 1];

    if (e.shiftKey && document.activeElement === first) {
      e.preventDefault();
      (last as HTMLElement).focus();
    } else if (!e.shiftKey && document.activeElement === last) {
      e.preventDefault();
      (first as HTMLElement).focus();
    }
  };

  return (
    <div
      className="fixed inset-0 z-[60] flex items-center justify-center p-4 bg-black/60 backdrop-blur-sm"
      onClick={(e) => { if (e.target === e.currentTarget && !isLoading) onClose(); }}
      role="dialog"
      aria-modal="true"
      aria-label="Answer agent questions"
    >
      <div
        className="bg-surface border border-border w-full max-w-2xl rounded-xl shadow-2xl overflow-hidden"
        onKeyDown={handleTabTrap}
      >
        <div className="flex items-center justify-between p-6 border-b border-border">
          <h2 className="text-lg font-bold text-text">Answer Questions</h2>
          <button
            onClick={onClose}
            disabled={isLoading}
            className="text-textMuted hover:text-text p-1.5 rounded hover:bg-surfaceHighlight transition-colors"
            aria-label="Close modal"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 space-y-6 max-h-[70vh] overflow-y-auto">
          {error && (
            <div className="bg-red-500/10 border border-red-500/20 rounded-lg p-4 flex items-start gap-3">
              <AlertTriangle className="w-5 h-5 text-red-400 flex-shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-red-400">Error</p>
                <p className="text-sm text-red-300 mt-1">{error}</p>
              </div>
            </div>
          )}

          {question.questions.map((item, idx) => (
            <div key={idx} className="space-y-2">
              <label className="text-sm font-medium text-text">
                {item.question}
                <span className="text-red-400">*</span>
              </label>
              {item.hint && (
                <p className="text-xs text-textMuted italic">{item.hint}</p>
              )}

              {item.type === 'Text' && (
                <textarea
                  value={(answers[`q${idx}`] as string) || ''}
                  onChange={(e) => setAnswers({ ...answers, [`q${idx}`]: e.target.value })}
                  placeholder="Enter your answer…"
                  disabled={isLoading}
                  className="w-full bg-background border border-border rounded-md p-3 text-sm text-text min-h-[100px] focus:outline-none focus:border-primary resize-y"
                  aria-label={`Answer to: ${item.question}`}
                  aria-required="true"
                />
              )}

              {item.type === 'Radio' && (
                <div className="space-y-2">
                  {item.options?.map((opt) => (
                    <label key={opt} className="flex items-center gap-3 cursor-pointer">
                      <input
                        type="radio"
                        name={`q${idx}`}
                        value={opt}
                        checked={(answers[`q${idx}`] as string) === opt}
                        onChange={() => setAnswers({ ...answers, [`q${idx}`]: opt })}
                        disabled={isLoading}
                        className="w-4 h-4 text-primary accent-primary"
                        aria-label={opt}
                      />
                      <span className="text-sm text-text">{opt}</span>
                    </label>
                  ))}
                  {item.allowCustom && (
                    <div className="mt-2 space-y-2">
                      <label className="flex items-center gap-3 cursor-pointer">
                        <input
                          type="radio"
                          name={`q${idx}`}
                          value="__custom__"
                          checked={(answers[`q${idx}`] as string) === '__custom__'}
                          onChange={() => setAnswers({ ...answers, [`q${idx}`]: '__custom__' })}
                          disabled={isLoading}
                          className="w-4 h-4 text-primary accent-primary"
                        />
                        <span className="text-sm text-text">Other…</span>
                      </label>
                      {(answers[`q${idx}`] as string) === '__custom__' && (
                        <input
                          type="text"
                          value={customTexts[`q${idx}_custom`] || ''}
                          onChange={(e) => setCustomTexts({ ...customTexts, [`q${idx}_custom`]: e.target.value })}
                          placeholder="Please specify…"
                          disabled={isLoading}
                          className="w-full bg-background border border-border rounded-md p-2 text-sm text-text ml-7 focus:outline-none focus:border-primary"
                          aria-label="Custom answer"
                        />
                      )}
                    </div>
                  )}
                </div>
              )}

              {item.type === 'Checkbox' && (
                <div className="space-y-2">
                  {item.options?.map((opt) => (
                    <label key={opt} className="flex items-center gap-3 cursor-pointer">
                      <input
                        type="checkbox"
                        name={`q${idx}`}
                        value={opt}
                        checked={((answers[`q${idx}`] as string[]) || []).includes(opt)}
                        onChange={(e) => {
                          const current = ((answers[`q${idx}`] as string[]) || []);
                          if (e.target.checked) {
                            setAnswers({ ...answers, [`q${idx}`]: [...current, opt] });
                          } else {
                            setAnswers({ ...answers, [`q${idx}`]: current.filter(v => v !== opt) });
                          }
                        }}
                        disabled={isLoading}
                        className="w-4 h-4 text-primary accent-primary"
                        aria-label={opt}
                      />
                      <span className="text-sm text-text">{opt}</span>
                    </label>
                  ))}
                  {item.allowCustom && (
                    <div className="mt-2 space-y-2">
                      <label className="flex items-center gap-3 cursor-pointer">
                        <input
                          type="checkbox"
                          value="__custom__"
                          checked={((answers[`q${idx}`] as string[]) || []).includes('__custom__')}
                          onChange={(e) => {
                            const current = ((answers[`q${idx}`] as string[]) || []);
                            if (e.target.checked) {
                              setAnswers({ ...answers, [`q${idx}`]: [...current, '__custom__'] });
                            } else {
                              setAnswers({ ...answers, [`q${idx}`]: current.filter(v => v !== '__custom__') });
                            }
                          }}
                          disabled={isLoading}
                          className="w-4 h-4 text-primary accent-primary"
                        />
                        <span className="text-sm text-text">Other…</span>
                      </label>
                      {((answers[`q${idx}`] as string[]) || []).includes('__custom__') && (
                        <input
                          type="text"
                          value={customTexts[`q${idx}_custom`] || ''}
                          onChange={(e) => setCustomTexts({ ...customTexts, [`q${idx}_custom`]: e.target.value })}
                          placeholder="Please specify…"
                          disabled={isLoading}
                          className="w-full bg-background border border-border rounded-md p-2 text-sm text-text ml-7 focus:outline-none focus:border-primary"
                          aria-label="Custom answer"
                        />
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          ))}
        </div>

        <div className="flex items-center justify-end gap-3 p-6 border-t border-border">
          <button
            onClick={onClose}
            disabled={isLoading}
            className="px-4 py-2 text-sm text-textMuted hover:text-text transition-colors rounded-md hover:bg-surfaceHighlight"
          >
            Cancel
          </button>
          <button
            onClick={() => execute()}
            disabled={!isAnswered || isLoading}
            aria-disabled={!isAnswered || isLoading}
            className={`px-6 py-2.5 rounded-md text-sm font-medium shadow-lg shadow-primary/20 transition-all ${
              isAnswered && !isLoading
                ? 'bg-primary hover:bg-primaryHover text-white'
                : 'bg-primary text-white opacity-50 cursor-not-allowed'
            }`}
          >
            {isLoading ? (
              <span className="flex items-center gap-2">
                <Loader2 className="w-4 h-4 animate-spin" />
                Submitting…
              </span>
            ) : (
              'Submit Answers'
            )}
          </button>
        </div>
      </div>
    </div>
  );
};

export default AgentQuestionModal;
