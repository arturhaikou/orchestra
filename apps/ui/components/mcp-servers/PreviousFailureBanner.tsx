import React from 'react';

interface PreviousFailureBannerProps {
  show: boolean;
}

export function PreviousFailureBanner({ show }: PreviousFailureBannerProps) {
  if (!show) return null;
  return (
    <div
      role="alert"
      className="mb-4 rounded-md border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800"
    >
      ⚠ This server previously failed to connect. Please verify the connection details below.
    </div>
  );
}
