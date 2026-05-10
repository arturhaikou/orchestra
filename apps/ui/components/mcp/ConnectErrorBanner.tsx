import React from 'react';
import { AlertCircle } from 'lucide-react';
import { ConnectErrorCode } from '../../types';

interface ConnectErrorBannerProps {
  errorCode: ConnectErrorCode;
  onDismiss: () => void;
}

const ERROR_MESSAGES: Record<ConnectErrorCode, string> = {
  CONNECTION_TIMEOUT:
    'Connection timed out. The server did not respond within 30 seconds.',
  AUTH_FAILED:
    'Authentication failed. Please check your API key and try again.',
  UNREACHABLE:
    'Could not reach the server. Please check the URL or command and try again.',
  INVALID_COMMAND:
    'Could not reach the server. Please check the URL or command and try again.',
  UNKNOWN:
    'An unexpected error occurred. Please try again.',
};

export const ConnectErrorBanner: React.FC<ConnectErrorBannerProps> = ({
  errorCode,
  onDismiss,
}) => (
  <div className="banner banner-error" role="alert">
    <AlertCircle size={15} className="banner-icon" aria-hidden="true" />
    <div className="banner-body">
      <div className="banner-title">Connection failed</div>
      <div className="banner-msg">{ERROR_MESSAGES[errorCode]}</div>
    </div>
    <button
      type="button"
      className="banner-dismiss"
      aria-label="Dismiss error"
      onClick={onDismiss}
    >
      ×
    </button>
  </div>
);

export default ConnectErrorBanner;
