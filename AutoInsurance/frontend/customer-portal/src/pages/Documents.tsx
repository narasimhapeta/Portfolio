import { useState } from 'react';
import { useGetPoliciesQuery, useGetDocumentsQuery, useGenerateDocumentMutation } from '../api/customerApi';
import AppLayout from '../components/AppLayout';
import LoadingSpinner from '../components/LoadingSpinner';

const DOC_TYPES = ['InsuranceCard', 'DeclarationPage'];

export default function Documents() {
  const { data: policies } = useGetPoliciesQuery();
  const [selectedPolicyId, setSelectedPolicyId] = useState('');
  const policyId = selectedPolicyId || policies?.[0]?.id || '';

  const { data: docs, isLoading } = useGetDocumentsQuery(policyId, { skip: !policyId });
  const [generateDocument, { isLoading: generating }] = useGenerateDocumentMutation();

  const onGenerate = async (docType: string) => {
    if (!policyId) return;
    await generateDocument({ policyId, documentType: docType }).unwrap();
  };

  return (
    <AppLayout>
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Documents</h1>
          <p className="text-gray-500 text-sm mt-1">Insurance cards and declaration pages</p>
        </div>
        {policies && policies.length > 1 && (
          <select value={selectedPolicyId} onChange={e => setSelectedPolicyId(e.target.value)}
            className="border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white">
            {policies.map(p => <option key={p.id} value={p.id}>{p.policyNumber}</option>)}
          </select>
        )}
      </div>

      <div className="flex gap-3 mb-6">
        {DOC_TYPES.map(dt => (
          <button key={dt} onClick={() => onGenerate(dt)} disabled={generating || !policyId}
            className="bg-white border border-gray-300 hover:border-blue-400 hover:text-blue-600 text-gray-700 px-4 py-2 rounded-lg text-sm font-medium transition-colors disabled:opacity-50">
            {generating ? 'Generating…' : `Generate ${dt === 'InsuranceCard' ? 'Insurance Card' : 'Declaration Page'}`}
          </button>
        ))}
      </div>

      {isLoading && <LoadingSpinner />}
      {!policyId && <p className="text-gray-400 text-sm">Select a policy to view documents.</p>}

      {docs?.length === 0 && policyId && (
        <p className="text-gray-400 text-sm">No documents yet. Generate one above.</p>
      )}

      <div className="space-y-3">
        {docs?.map(d => (
          <div key={d.id} className="bg-white border border-gray-200 rounded-xl p-4 flex items-center justify-between">
            <div>
              <p className="font-medium text-gray-900">{d.type === 'InsuranceCard' ? 'Insurance Card' : 'Declaration Page'}</p>
              <p className="text-xs text-gray-400 mt-0.5">Generated {new Date(d.generatedAt).toLocaleString()}</p>
            </div>
            <a href={d.blobUrl} target="_blank" rel="noopener noreferrer"
              className="text-sm text-blue-600 hover:text-blue-800 font-medium">View →</a>
          </div>
        ))}
      </div>
    </AppLayout>
  );
}
