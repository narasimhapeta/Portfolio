import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGetPolicyDetailQuery, useRenewPolicyMutation } from '../api/customerApi';
import AppLayout from '../components/AppLayout';
import StatusBadge from '../components/StatusBadge';
import LoadingSpinner from '../components/LoadingSpinner';

type Tab = 'overview' | 'drivers' | 'vehicles' | 'coverages' | 'endorsements';

export default function PolicyDetail() {
  const { id = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>('overview');
  const { data: policy, isLoading, error } = useGetPolicyDetailQuery(id, { skip: !id });
  const [renewPolicy, { isLoading: renewing }] = useRenewPolicyMutation();

  const onRenew = async () => {
    if (!id) return;
    await renewPolicy(id).unwrap();
    alert('Renewal request submitted!');
  };

  if (isLoading) return <AppLayout><LoadingSpinner /></AppLayout>;
  if (error || !policy) return <AppLayout><p className="text-red-500">Policy not found.</p></AppLayout>;

  const TABS: { key: Tab; label: string }[] = [
    { key: 'overview', label: 'Overview' },
    { key: 'drivers', label: `Drivers (${policy.drivers.length})` },
    { key: 'vehicles', label: `Vehicles (${policy.vehicles.length})` },
    { key: 'coverages', label: `Coverages (${policy.coverages.length})` },
    { key: 'endorsements', label: `Endorsements (${policy.endorsements.length})` },
  ];

  return (
    <AppLayout>
      <button onClick={() => navigate('/')} className="text-sm text-gray-400 hover:text-gray-600 mb-4">← Back to Dashboard</button>

      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 font-mono">{policy.policyNumber}</h1>
          <div className="flex items-center gap-3 mt-1">
            <StatusBadge status={policy.status} />
            <span className="text-sm text-gray-500">{policy.effectiveDate} → {policy.expirationDate}</span>
          </div>
        </div>
        <div className="text-right">
          <p className="text-2xl font-bold text-gray-900">${policy.totalAnnualPremium.toFixed(2)}<span className="text-sm font-normal text-gray-500">/yr</span></p>
          <div className="flex gap-2 mt-2">
            <button onClick={() => navigate(`/policies/${id}/coverages`)}
              className="text-xs bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 rounded-lg transition-colors">
              Change Coverages
            </button>
            <button onClick={onRenew} disabled={renewing}
              className="text-xs border border-gray-300 hover:border-gray-400 text-gray-700 px-3 py-1.5 rounded-lg transition-colors">
              {renewing ? 'Renewing…' : 'Renew Policy'}
            </button>
          </div>
        </div>
      </div>

      <div className="border-b border-gray-200 mb-6">
        <nav className="flex gap-1">
          {TABS.map(t => (
            <button key={t.key} onClick={() => setTab(t.key)}
              className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${tab === t.key ? 'border-blue-600 text-blue-600' : 'border-transparent text-gray-500 hover:text-gray-700'}`}>
              {t.label}
            </button>
          ))}
        </nav>
      </div>

      {tab === 'overview' && (
        <div className="grid grid-cols-2 gap-4">
          {[
            ['Policy Number', policy.policyNumber],
            ['Status', policy.status],
            ['Effective Date', policy.effectiveDate],
            ['Expiration Date', policy.expirationDate],
            ['Annual Premium', `$${policy.totalAnnualPremium.toFixed(2)}`],
          ].map(([label, value]) => (
            <div key={label} className="bg-gray-50 rounded-lg p-4">
              <p className="text-xs text-gray-500 uppercase tracking-wide">{label}</p>
              <p className="font-medium text-gray-900 mt-0.5">{value}</p>
            </div>
          ))}
        </div>
      )}

      {tab === 'drivers' && (
        <div className="space-y-3">
          {policy.drivers.map((d, i) => (
            <div key={i} className="bg-gray-50 rounded-lg p-4 flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">{d.firstName} {d.lastName}</p>
                <p className="text-sm text-gray-500">License: {d.licenseNumber} ({d.licenseState})</p>
              </div>
              {d.isPrimary && <span className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">Primary</span>}
            </div>
          ))}
        </div>
      )}

      {tab === 'vehicles' && (
        <div className="space-y-3">
          {policy.vehicles.map((v, i) => (
            <div key={i} className="bg-gray-50 rounded-lg p-4">
              <p className="font-medium text-gray-900">{v.year} {v.make} {v.model}</p>
              <p className="text-sm text-gray-500">VIN: {v.vin} · {v.primaryUse}</p>
            </div>
          ))}
        </div>
      )}

      {tab === 'coverages' && (
        <div className="space-y-3">
          {policy.coverages.map((c, i) => (
            <div key={i} className="bg-gray-50 rounded-lg p-4 flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">{c.name}</p>
                <p className="text-sm text-gray-500">{c.limits}</p>
              </div>
              <p className="font-semibold text-gray-900">${c.annualPremium.toFixed(0)}/yr</p>
            </div>
          ))}
        </div>
      )}

      {tab === 'endorsements' && (
        policy.endorsements.length === 0
          ? <p className="text-gray-400 text-sm">No endorsements on this policy.</p>
          : <div className="space-y-3">
            {policy.endorsements.map((e) => (
              <div key={e.id} className="bg-gray-50 rounded-lg p-4">
                <div className="flex items-center justify-between">
                  <p className="font-medium text-gray-900">{e.type}</p>
                  <p className="text-xs text-gray-400">Effective {e.effectiveDate}</p>
                </div>
                <p className="text-sm text-gray-500 mt-1">{e.description}</p>
              </div>
            ))}
          </div>
      )}
    </AppLayout>
  );
}
