import React, { useState } from 'react';
import { Policy, Coverage } from '../types/policy';
import Modal from './Modal';
import '../styles/PolicyCard.css';

interface PolicyCardProps {
  policy: Policy;
  onCoverageUpdate: (policyId: string, covId: string, coverage: Partial<Coverage>) => Promise<void>;
}

const PolicyCard: React.FC<PolicyCardProps> = ({ policy, onCoverageUpdate }) => {
  const [editingCoverage, setEditingCoverage] = useState<Coverage | null>(null);
  const [formData, setFormData] = useState({ limit: 0, deductible: 0 });
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');

  const statusColor: Record<string, string> = {
    active: '#22c55e',
    expired: '#ef4444',
    pending: '#f59e0b',
    cancelled: '#6b7280',
  };

  const openEdit = (cov: Coverage) => {
    setEditingCoverage(cov);
    setFormData({ limit: cov.limit, deductible: cov.deductible });
    setSaveError('');
  };

  const handleSave = async () => {
    if (!editingCoverage) return;
    if (formData.limit <= 0 || formData.deductible < 0) {
      setSaveError('Limit must be > 0 and deductible must be ≥ 0');
      return;
    }
    setSaving(true);
    setSaveError('');
    try {
      await onCoverageUpdate(policy.id, editingCoverage.id, formData);
      setEditingCoverage(null);
    } catch {
      setSaveError('Failed to save. Please try again.');
    } finally {
      setSaving(false);
    }
  };

  const daysUntilExpiry = Math.ceil(
    (new Date(policy.endDate).getTime() - Date.now()) / (1000 * 60 * 60 * 24)
  );

  return (
    <div className="policy-card">
      <div className="policy-header">
        <div>
          <h3 className="policy-number">{policy.policyNumber}</h3>
          <p className="policy-type">{policy.type.toUpperCase()} INSURANCE</p>
        </div>
        <span className="policy-status" style={{ backgroundColor: statusColor[policy.status] ?? '#6b7280' }}>
          {policy.status.toUpperCase()}
        </span>
      </div>

      <div className="policy-meta">
        <div className="meta-item">
          <span className="meta-label">Annual Premium</span>
          <span className="meta-value">${policy.premium.toLocaleString()}</span>
        </div>
        <div className="meta-item">
          <span className="meta-label">Expires</span>
          <span className="meta-value" style={{ color: daysUntilExpiry < 30 ? '#ef4444' : 'inherit' }}>
            {new Date(policy.endDate).toLocaleDateString()}
            {daysUntilExpiry > 0 && ` (${daysUntilExpiry}d)`}
          </span>
        </div>
      </div>

      <div className="coverages-section">
        <h4 className="coverages-title">Coverages</h4>
        <ul className="coverages-list">
          {policy.coverages.map((cov) => (
            <li key={cov.id} className="coverage-item">
              <div className="coverage-info">
                <span className="coverage-type">{cov.type}</span>
                <span className="coverage-limits">
                  Limit: ${cov.limit.toLocaleString()} · Deductible: ${cov.deductible.toLocaleString()}
                </span>
              </div>
              {policy.status === 'active' && (
                <button className="btn-edit" onClick={() => openEdit(cov)}>Edit</button>
              )}
            </li>
          ))}
        </ul>
      </div>

      {editingCoverage && (
        <Modal title={`Edit ${editingCoverage.type} Coverage`} onClose={() => setEditingCoverage(null)}>
          <div className="edit-form">
            <label className="form-label">
              Coverage Limit ($)
              <input
                type="number"
                className="form-input"
                value={formData.limit}
                onChange={(e) => setFormData({ ...formData, limit: Number(e.target.value) })}
                min={0}
              />
            </label>
            <label className="form-label">
              Deductible ($)
              <input
                type="number"
                className="form-input"
                value={formData.deductible}
                onChange={(e) => setFormData({ ...formData, deductible: Number(e.target.value) })}
                min={0}
              />
            </label>
            {saveError && <p className="form-error">{saveError}</p>}
            <div className="form-actions">
              <button className="btn-secondary" onClick={() => setEditingCoverage(null)}>Cancel</button>
              <button className="btn-primary" onClick={handleSave} disabled={saving}>
                {saving ? 'Saving…' : 'Save Changes'}
              </button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
};

export default PolicyCard;
