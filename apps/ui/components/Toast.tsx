import React from 'react';

interface ToastProps {
  message: string;
  type?: 'error' | 'success' | 'info';
  onClose?: () => void;
}

const Toast: React.FC<ToastProps> = ({ message, type = 'error', onClose }) => {
  return (
    <div className={`fixed bottom-6 right-6 z-[100] px-4 py-3 rounded-lg shadow-lg border text-sm font-semibold flex items-center gap-3 transition-all bg-surface text-text ${type === 'error' ? 'border-red-500 bg-red-50 text-red-700' : type === 'success' ? 'border-emerald-500 bg-emerald-50 text-emerald-700' : 'border-primary bg-primary/10 text-primary'}`}> 
      <span>{message}</span>
      {onClose && (
        <button onClick={onClose} className="ml-3 text-xs font-bold text-textMuted hover:text-text">Dismiss</button>
      )}
    </div>
  );
};

export default Toast;
