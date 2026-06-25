import { useRef, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useGetClaimDetailQuery, useUploadClaimDocumentMutation } from '../api/claimsApi';
import AppLayout from '../components/AppLayout';
import StatusBadge from '../components/StatusBadge';
import LoadingSpinner from '../components/LoadingSpinner';

const DOC_TYPES = ['IncidentPhoto', 'DamagePhoto', 'Other'];

export default function ClaimDetail() {
  const { id = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { data: claim, isLoading } = useGetClaimDetailQuery(id, { skip: !id });
  const [uploadDoc, { isLoading: uploading }] = useUploadClaimDocumentMutation();

  const [docType, setDocType] = useState(DOC_TYPES[0]);
  const fileRef = useRef<HTMLInputElement>(null);
  const [uploadError, setUploadError] = useState('');

  const onUpload = async () => {
    const file = fileRef.current?.files?.[0];
    if (!file || !id) return;
    setUploadError('');
    const reader = new FileReader();
    reader.onload = async () => {
      const base64 = (reader.result as string).split(',')[1];
      try {
        await uploadDoc({ claimId: id, documentType: docType, base64Content: base64, fileName: file.name }).unwrap();
        if (fileRef.current) fileRef.current.value = '';
      } catch { setUploadError('Upload failed. Please try again.'); }
    };
    reader.readAsDataURL(file);
  };

  if (isLoading) return <AppLayout><LoadingSpinner /></AppLayout>;
  if (!claim) return <AppLayout><p className="text-red-500">Claim not found.</p></AppLayout>;

  return (
    <AppLayout>
      <button onClick={() => navigate('/claims')} className="text-sm text-gray-400 hover:text-gray-600 mb-4">← Back to Claims</button>

      <div className="flex items-start justify-between mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Claim Details</h1>
          <p className="text-sm text-gray-500 mt-1">Incident date: {claim.incidentDate}</p>
        </div>
        <StatusBadge status={claim.status} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h2 className="font-semibold text-gray-800 mb-3">Description</h2>
            <p className="text-sm text-gray-600 leading-relaxed">{claim.description}</p>
          </div>

          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h2 className="font-semibold text-gray-800 mb-4">Documents ({claim.documents.length})</h2>
            {claim.documents.length === 0
              ? <p className="text-sm text-gray-400">No documents uploaded yet.</p>
              : <div className="space-y-2">
                {claim.documents.map(d => (
                  <div key={d.id} className="flex items-center justify-between text-sm py-2 border-b border-gray-50 last:border-0">
                    <span className="text-gray-700">{d.type}</span>
                    <span className="text-xs text-gray-400">{new Date(d.uploadedAt).toLocaleString()}</span>
                    <a href={d.blobUrl} target="_blank" rel="noopener noreferrer" className="text-blue-600 hover:text-blue-800">View</a>
                  </div>
                ))}
              </div>
            }
          </div>
        </div>

        <div className="space-y-4">
          <div className="bg-white border border-gray-200 rounded-xl p-5">
            <h2 className="font-semibold text-gray-800 mb-4">Upload Document</h2>
            <div className="space-y-3">
              <select value={docType} onChange={e => setDocType(e.target.value)}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white">
                {DOC_TYPES.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
              <input ref={fileRef} type="file" accept="image/*,.pdf"
                className="w-full text-sm text-gray-500 file:mr-3 file:py-1.5 file:px-3 file:rounded file:border-0 file:text-sm file:bg-blue-50 file:text-blue-700 hover:file:bg-blue-100" />
              {uploadError && <p className="text-xs text-red-500">{uploadError}</p>}
              <button onClick={onUpload} disabled={uploading}
                className="w-full bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white py-2 rounded-lg text-sm font-medium transition-colors">
                {uploading ? 'Uploading…' : 'Upload'}
              </button>
            </div>
          </div>

          <div className="bg-gray-50 border border-gray-200 rounded-xl p-4">
            <p className="text-xs text-gray-500 uppercase tracking-wide mb-2">Claim Info</p>
            <div className="space-y-1.5 text-sm">
              <div className="flex justify-between"><span className="text-gray-500">Filed</span><span className="text-gray-900">{new Date(claim.createdAt).toLocaleDateString()}</span></div>
              <div className="flex justify-between"><span className="text-gray-500">Status</span><span className="text-gray-900">{claim.status}</span></div>
            </div>
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
