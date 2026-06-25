import { useNavigate } from 'react-router-dom';
import { useGetPoliciesQuery } from '../api/customerApi';
import AppLayout from '../components/AppLayout';
import StatusBadge from '../components/StatusBadge';
import LoadingSpinner from '../components/LoadingSpinner';

export default function Dashboard() {
  const { data: policies, isLoading, error } = useGetPoliciesQuery();
  const navigate = useNavigate();

  return (
    <AppLayout>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">My Policies</h1>
        <p className="text-gray-500 text-sm mt-1">Manage your auto insurance coverage</p>
      </div>

      {isLoading && <LoadingSpinner />}
      {error && <p className="text-red-500 text-sm">Failed to load policies.</p>}

      {policies?.length === 0 && (
        <div className="text-center py-16 border border-dashed border-gray-200 rounded-xl">
          <p className="text-gray-500">No policies found for your account.</p>
          <p className="text-sm text-gray-400 mt-1">Purchase a policy through the Quote & Buy portal.</p>
        </div>
      )}

      <div className="space-y-4">
        {policies?.map(p => (
          <div key={p.id} onClick={() => navigate(`/policies/${p.id}`)}
            className="bg-white border border-gray-200 rounded-xl p-5 cursor-pointer hover:border-blue-300 hover:shadow-sm transition-all">
            <div className="flex items-start justify-between">
              <div>
                <p className="font-mono font-semibold text-gray-900">{p.policyNumber}</p>
                <p className="text-sm text-gray-500 mt-0.5">
                  {p.effectiveDate} → {p.expirationDate}
                </p>
              </div>
              <div className="text-right">
                <StatusBadge status={p.status} />
                <p className="text-lg font-bold text-gray-900 mt-1">${p.totalAnnualPremium.toFixed(2)}<span className="text-sm font-normal text-gray-500">/yr</span></p>
              </div>
            </div>
          </div>
        ))}
      </div>
    </AppLayout>
  );
}
