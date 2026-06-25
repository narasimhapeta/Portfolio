import { useState } from 'react';
import { useGetPoliciesQuery } from '../api/customerApi';
import { useGetPaymentHistoryQuery, useSetBillingScheduleMutation } from '../api/paymentApi';
import AppLayout from '../components/AppLayout';
import StatusBadge from '../components/StatusBadge';
import LoadingSpinner from '../components/LoadingSpinner';

const FREQUENCIES = ['Monthly', 'Quarterly', 'Yearly'];

export default function Payments() {
  const { data: policies } = useGetPoliciesQuery();
  const [selectedPolicyId, setSelectedPolicyId] = useState('');
  const policyId = selectedPolicyId || policies?.[0]?.id || '';

  const { data: history, isLoading } = useGetPaymentHistoryQuery(policyId, { skip: !policyId });
  const [setBillingSchedule, { isLoading: scheduling }] = useSetBillingScheduleMutation();
  const [frequency, setFrequency] = useState('Yearly');
  const [scheduled, setScheduled] = useState(false);

  const onSetSchedule = async () => {
    if (!policyId) return;
    await setBillingSchedule({ policyId, frequency }).unwrap();
    setScheduled(true);
  };

  return (
    <AppLayout>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Payments</h1>
          <p className="text-gray-500 text-sm mt-1">Payment history and billing schedule</p>
        </div>
        {policies && policies.length > 1 && (
          <select value={selectedPolicyId} onChange={e => setSelectedPolicyId(e.target.value)}
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white">
            {policies.map(p => <option key={p.id} value={p.id}>{p.policyNumber}</option>)}
          </select>
        )}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <h2 className="font-semibold text-gray-800 mb-3">Payment History</h2>
          {isLoading && <LoadingSpinner />}
          {history?.length === 0 && <p className="text-gray-400 text-sm">No payments yet.</p>}
          <div className="space-y-3">
            {history?.map(t => (
              <div key={t.id} className="bg-white border border-gray-200 rounded-xl p-4 flex items-center justify-between">
                <div>
                  <p className="font-medium text-gray-900">${t.amount.toFixed(2)}</p>
                  <p className="text-xs text-gray-400 mt-0.5 font-mono">{t.transactionRef}</p>
                  {t.paidAt && <p className="text-xs text-gray-400">{new Date(t.paidAt).toLocaleDateString()}</p>}
                </div>
                <StatusBadge status={t.status} />
              </div>
            ))}
          </div>
        </div>

        <div>
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h2 className="font-semibold text-gray-800 mb-4">Billing Schedule</h2>
            <div className="space-y-3">
              <select value={frequency} onChange={e => { setFrequency(e.target.value); setScheduled(false); }}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white">
                {FREQUENCIES.map(f => <option key={f} value={f}>{f}</option>)}
              </select>
              <button onClick={onSetSchedule} disabled={scheduling || !policyId}
                className="w-full bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white py-2 rounded-lg text-sm font-medium transition-colors">
                {scheduling ? 'Saving…' : 'Update Schedule'}
              </button>
              {scheduled && <p className="text-xs text-green-600">Schedule updated!</p>}
            </div>
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
