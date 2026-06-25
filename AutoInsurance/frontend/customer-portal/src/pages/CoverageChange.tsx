import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGetPolicyDetailQuery, useChangeCoverageMutation } from '../api/customerApi';
import AppLayout from '../components/AppLayout';
import LoadingSpinner from '../components/LoadingSpinner';
import type { CoverageChangeDto } from '../types/portal';

const LIMITS_OPTIONS: Record<string, string[]> = {
  'Liability': ['50/100', '100/300', '250/500'],
  'Collision': ['$500 Deductible', '$1,000 Deductible', '$2,000 Deductible'],
  'Comprehensive': ['$250 Deductible', '$500 Deductible', '$1,000 Deductible'],
  'Uninsured Motorist': ['25/50', '50/100', '100/300'],
  'Medical Payments': ['$1,000', '$2,500', '$5,000'],
};

export default function CoverageChange() {
  const { id: policyId = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: policy, isLoading } = useGetPolicyDetailQuery(policyId, { skip: !policyId });
  const [changeCoverage, { isLoading: saving, error }] = useChangeCoverageMutation();

  const [changes, setChanges] = useState<Map<number, string>>(new Map());

  const onLimitChange = (covId: number, newLimits: string) => {
    setChanges(prev => new Map(prev).set(covId, newLimits));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!policyId || changes.size === 0) return;
    const payload: CoverageChangeDto[] = Array.from(changes.entries()).map(([coverageTypeId, newLimits]) => ({
      coverageTypeId, newLimits, newPremium: 0,
    }));
    await changeCoverage({ policyId, changes: payload }).unwrap();
    navigate(`/policies/${policyId}`);
  };

  if (isLoading) return <AppLayout><LoadingSpinner /></AppLayout>;
  if (!policy) return <AppLayout><p className="text-red-500">Policy not found.</p></AppLayout>;

  return (
    <AppLayout>
      <button onClick={() => navigate(`/policies/${policyId}`)} className="text-sm text-gray-400 hover:text-gray-600 mb-4">← Back to Policy</button>
      <h1 className="text-2xl font-bold text-gray-900 mb-2">Change Coverages</h1>
      <p className="text-sm text-gray-500 mb-6">Changes will create an endorsement effective tomorrow.</p>

      <form onSubmit={onSubmit} className="max-w-lg space-y-4">
        {policy.coverages.map(c => {
          const options = LIMITS_OPTIONS[c.name] ?? [c.limits];
          const currentLimit = changes.get(c.id) ?? c.limits;
          return (
            <div key={c.id} className="bg-white border border-gray-200 rounded-xl p-4">
              <div className="flex items-center justify-between mb-2">
                <p className="font-medium text-gray-900">{c.name}</p>
                <p className="text-sm text-gray-500">${c.annualPremium.toFixed(0)}/yr</p>
              </div>
              <select value={currentLimit} onChange={e => onLimitChange(c.id, e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white">
                {options.map(o => <option key={o} value={o}>{o}</option>)}
              </select>
              {changes.has(c.id) && changes.get(c.id) !== c.limits && (
                <p className="text-xs text-blue-600 mt-1">Changed from: {c.limits}</p>
              )}
            </div>
          );
        })}
        {error && <p className="text-sm text-red-600">Failed to save changes. Please try again.</p>}
        {changes.size === 0 && <p className="text-xs text-amber-600">Make at least one change to submit.</p>}
        <div className="flex gap-3 pt-2">
          <button type="submit" disabled={saving || changes.size === 0}
            className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-6 py-2.5 rounded-lg font-medium text-sm transition-colors">
            {saving ? 'Saving…' : 'Submit Endorsement'}
          </button>
          <button type="button" onClick={() => navigate(`/policies/${policyId}`)}
            className="text-gray-500 hover:text-gray-700 text-sm px-4">Cancel</button>
        </div>
      </form>
    </AppLayout>
  );
}
