import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useGetPoliciesQuery } from '../api/customerApi';
import { useGetClaimsQuery } from '../api/claimsApi';
import AppLayout from '../components/AppLayout';
import StatusBadge from '../components/StatusBadge';
import LoadingSpinner from '../components/LoadingSpinner';

export default function Claims() {
  const { data: policies } = useGetPoliciesQuery();
  const [selectedPolicyId, setSelectedPolicyId] = useState('');
  const policyId = selectedPolicyId || policies?.[0]?.id || '';
  const { data: claims, isLoading } = useGetClaimsQuery(policyId, { skip: !policyId });
  const navigate = useNavigate();

  return (
    <AppLayout>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Claims</h1>
          <p className="text-gray-500 text-sm mt-1">File and track your insurance claims</p>
        </div>
        <div className="flex gap-2">
          {policies && policies.length > 1 && (
            <select value={selectedPolicyId} onChange={e => setSelectedPolicyId(e.target.value)}
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white">
              {policies.map(p => <option key={p.id} value={p.id}>{p.policyNumber}</option>)}
            </select>
          )}
          <button onClick={() => navigate('/claims/submit')}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors">
            File a Claim
          </button>
        </div>
      </div>

      {isLoading && <LoadingSpinner />}

      {claims?.length === 0 && (
        <div className="text-center py-16 border border-dashed border-gray-200 rounded-xl">
          <p className="text-gray-500">No claims found.</p>
        </div>
      )}

      <div className="space-y-3">
        {claims?.map(c => (
          <div key={c.id} onClick={() => navigate(`/claims/${c.id}`)}
            className="bg-white border border-gray-200 rounded-xl p-5 cursor-pointer hover:border-blue-300 hover:shadow-sm transition-all">
            <div className="flex items-start justify-between">
              <div>
                <p className="font-medium text-gray-900">Incident: {c.incidentDate}</p>
                <p className="text-sm text-gray-500 mt-0.5 line-clamp-1">{c.description}</p>
                <p className="text-xs text-gray-400 mt-1">Filed {new Date(c.createdAt).toLocaleDateString()}</p>
              </div>
              <StatusBadge status={c.status} />
            </div>
          </div>
        ))}
      </div>
    </AppLayout>
  );
}
