import React from 'react';

export const StaleConnectionBanner: React.FC = () => (
  <div className="banner banner-warning" role="status">
    <span className="banner-icon" aria-hidden="true">⚠</span>
    <div className="banner-body">
      <div className="banner-title">Connection details changed</div>
      <div className="banner-msg">
        Reconnect to verify before saving.
      </div>
    </div>
  </div>
);

export default StaleConnectionBanner;
