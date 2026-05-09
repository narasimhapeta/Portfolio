import React from 'react';
import '../styles/Navigation.css';

const Navigation: React.FC = () => {
  return (
    <nav className="navbar">
      <div className="navbar-brand">
        <span className="brand-icon">🛡️</span>
        <span className="brand-name">AutoInsure<span className="brand-ai">AI</span></span>
      </div>
      <div className="navbar-info">
        <span className="user-badge">John Doe</span>
        <span className="policy-badge">Policy: AUTO-2026-001</span>
      </div>
    </nav>
  );
};

export default Navigation;
