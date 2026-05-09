import React, { useEffect, useState } from 'react';
import { Policy, Coverage } from '../types/policy';
import { UploadedDocument } from '../types/upload';
import { getPolicies, updateCoverage } from '../services/policyService';
import PolicyCard from '../components/PolicyCard';
import ChatBot from '../components/ChatBot';
import FileUpload from '../components/FileUpload';
import Navigation from '../components/Navigation';
import '../styles/PolicyDashboard.css';

const PolicyDashboard: React.FC = () => {
  const [policies, setPolicies] = useState<Policy[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [toast, setToast] = useState('');
  const [activeTab, setActiveTab] = useState<'policies' | 'documents' | 'chat'>('policies');

  useEffect(() => {
    loadPolicies();
  }, []);

  const loadPolicies = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await getPolicies();
      setPolicies(data);
    } catch (err: any) {
      setError(err.message ?? 'Failed to load policies');
    } finally {
      setLoading(false);
    }
  };

  const handleCoverageUpdate = async (policyId: string, covId: string, coverage: Partial<Coverage>) => {
    await updateCoverage(policyId, covId, coverage);
    showToast('Coverage updated successfully');
    await loadPolicies();
  };

  const showToast = (msg: string) => {
    setToast(msg);
    setTimeout(() => setToast(''), 3000);
  };

  const handleDocumentUploaded = (_doc: UploadedDocument) => {
    showToast('Document uploaded — AI chatbot can now answer questions from it');
  };

  return (
    <div className="app-shell">
      <Navigation />

      <div className="dashboard-container">
        <div className="dashboard-header">
          <h1>My Insurance Portal</h1>
          <p className="dashboard-subtitle">Manage your policies, upload documents, and chat with your AI assistant</p>
        </div>

        {/* Mobile tab nav */}
        <div className="tab-nav">
          <button className={`tab-btn ${activeTab === 'policies' ? 'active' : ''}`} onClick={() => setActiveTab('policies')}>Policies</button>
          <button className={`tab-btn ${activeTab === 'documents' ? 'active' : ''}`} onClick={() => setActiveTab('documents')}>Documents</button>
          <button className={`tab-btn ${activeTab === 'chat' ? 'active' : ''}`} onClick={() => setActiveTab('chat')}>AI Chat</button>
        </div>

        <div className="dashboard-layout">
          {/* Left column — Policies */}
          <section className={`left-panel ${activeTab !== 'policies' ? 'hidden-mobile' : ''}`}>
            <div className="section-header">
              <h2>Your Policies</h2>
              <button className="btn-link" onClick={loadPolicies}>↻ Refresh</button>
            </div>
            {loading && <div className="skeleton-list">{[1,2].map(i => <div key={i} className="skeleton-card" />)}</div>}
            {error && <div className="error-banner">{error} <button className="btn-link" onClick={loadPolicies}>Retry</button></div>}
            {!loading && !error && policies.map((p) => (
              <PolicyCard key={p.id} policy={p} onCoverageUpdate={handleCoverageUpdate} />
            ))}
          </section>

          {/* Right column — Documents + Chat */}
          <section className="right-panel">
            <div className={activeTab !== 'documents' ? 'hidden-mobile' : ''}>
              <FileUpload onDocumentUploaded={handleDocumentUploaded} />
            </div>
            <div className={activeTab !== 'chat' ? 'hidden-mobile' : ''}>
              <ChatBot />
            </div>
          </section>
        </div>
      </div>

      {toast && <div className="toast">{toast}</div>}
    </div>
  );
};

export default PolicyDashboard;
