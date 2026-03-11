import React from 'react';
import { AlertTriangle } from 'lucide-react';

interface StaleModelWarningProps {
  featureName: string; // Display name of the AI feature (e.g., "AI Summarization")
  message?: string;    // Optional custom warning message; defaults to standard message
}

/**
 * StaleModelWarning component displays an inline warning when a previously saved
 * AI model is no longer available in the current models list.
 * 
 * The warning is styled with an amber/yellow background and border, includes
 * an icon, and is fully accessible to screen readers.
 */
const StaleModelWarning: React.FC<StaleModelWarningProps> = ({
  featureName,
  message,
}) => {
  const defaultMessage = `The previously selected model for ${featureName} is no longer available. Please select a valid model.`;
  const warningMessage = message || defaultMessage;

  return (
    <div
      className="flex items-start gap-2.5 px-3 py-3 bg-amber-500/10 border border-amber-500/40 rounded-md"
      role="alert"
      aria-live="polite"
    >
      <AlertTriangle
        className="w-4 h-4 text-amber-600 flex-shrink-0 mt-0.5"
        aria-hidden="true"
      />
      <p className="text-sm text-amber-800">
        {warningMessage}
      </p>
    </div>
  );
};

export default StaleModelWarning;
