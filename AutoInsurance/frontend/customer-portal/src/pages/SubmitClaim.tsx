import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useGetPoliciesQuery } from '../api/customerApi';
import { useSubmitClaimMutation } from '../api/claimsApi';
import AppLayout from '../components/AppLayout';

export default function SubmitClaim() {
  const navigate = useNavigate();
  const { data: policies } = useGetPoliciesQuery();
  const [submitClaim, { isLoading, error }] = useSubmitClaimMutation();

  const [policyId, setPolicyId] = useState('');
  const [incidentDate, setIncidentDate] = useState('');
  const [description, setDescription] = useState('');

  const today = new Date().toISOString().split('T')[0];

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const pid = policyId || policies?.[0]?.id || '';
    if (!pid) return;
    try {
      await submitClaim({ policyId: pid, incidentDate, description }).unwrap();
      navigate('/claims');
    } catch { /* shown below */ }
  };

  return (
    <AppLayout>
      <button onClick={() => navigate('/claims')} className="text-sm text-gray-400 hover:text-gray-600 mb-4">← Back to Claims</button>
      <h1 className="text-2xl font-bold text-gray-900 mb-6">File a Claim</h1>

      <div className="bg-white border border-gray-200 rounded-xl p-6 max-w-lg">
        <form onSubmit={onSubmit} className="space-y-4">
          {policies && policies.length > 1 && (
            <div className="flex flex-col gap-1">
              <label className="text-sm font-medium text-gray-700">Policy</label>
              <select value={policyId} onChange={e => setPolicyId(e.target.value)}
                className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white">
                {policies.map(p => <option key={p.id} value={p.id}>{p.policyNumber}</option>)}
              </select>
            </div>
          )}
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-gray-700">Incident Date <span className="text-red-500">*</span></label>
            <input type="date" value={incidentDate} onChange={e => setIncidentDate(e.target.value)} max={today} required
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-gray-700">Description <span className="text-red-500">*</span></label>
            <textarea value={description} onChange={e => setDescription(e.target.value)} required rows={4}
              placeholder="Describe what happened, location, and any damage…"
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          </div>
          {error && <p className="text-sm text-red-600">Failed to submit. Please try again.</p>}
          <div className="flex gap-3 pt-2">
            <button type="submit" disabled={isLoading || !incidentDate || !description}
              className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-6 py-2.5 rounded-lg font-medium text-sm transition-colors">
              {isLoading ? 'Submitting…' : 'Submit Claim'}
            </button>
            <button type="button" onClick={() => navigate('/claims')}
              className="text-gray-500 hover:text-gray-700 text-sm px-4">Cancel</button>
          </div>
        </form>
      </div>
    </AppLayout>
  );
}
