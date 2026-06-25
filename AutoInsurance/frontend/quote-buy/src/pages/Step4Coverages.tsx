import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useSaveCoveragesMutation, useGetCoverageTypesQuery } from '../api/quoteApi';
import { setCoverages, setStep } from '../store/quoteSlice';
import type { RootState } from '../store';
import type { SelectedCoverage } from '../types/quote';
import QuoteLayout from '../components/QuoteLayout';
import { useState } from 'react';

const DEFAULT_LIMITS: Record<string, string[]> = {
  'Liability': ['50/100', '100/300', '250/500'],
  'Collision': ['$500 Deductible', '$1,000 Deductible', '$2,000 Deductible'],
  'Comprehensive': ['$250 Deductible', '$500 Deductible', '$1,000 Deductible'],
  'Uninsured Motorist': ['25/50', '50/100', '100/300'],
  'Medical Payments': ['$1,000', '$2,500', '$5,000'],
};

export default function Step4Coverages() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const session = useSelector((s: RootState) => s.quote.session);
  const [saveCoverages, { isLoading, error }] = useSaveCoveragesMutation();
  const { data: coverageTypes, isLoading: typesLoading } = useGetCoverageTypesQuery();

  const [selected, setSelected] = useState<Map<number, string>>(new Map());

  const toggle = (id: number, checked: boolean, name: string) => {
    setSelected(prev => {
      const next = new Map(prev);
      if (checked) {
        const limits = DEFAULT_LIMITS[name] ?? ['Standard'];
        next.set(id, limits[0]);
      } else {
        next.delete(id);
      }
      return next;
    });
  };

  const setLimit = (id: number, limit: string) => {
    setSelected(prev => new Map(prev).set(id, limit));
  };

  const onSubmit = async () => {
    if (!session || selected.size === 0) return;
    const coverages: SelectedCoverage[] = Array.from(selected.entries()).map(([coverageTypeId, limits]) => ({ coverageTypeId, limits }));
    try {
      await saveCoverages({ quoteId: session.quoteId, coverages }).unwrap();
      dispatch(setCoverages(coverages));
      dispatch(setStep(5));
      navigate('/review');
    } catch { /* error shown below */ }
  };

  if (typesLoading) return <QuoteLayout step={4} title="Select Coverages"><p className="text-gray-500">Loading coverage options…</p></QuoteLayout>;

  return (
    <QuoteLayout step={4} title="Select Coverages">
      <div className="space-y-4">
        {(coverageTypes ?? []).map(ct => {
          const checked = selected.has(ct.id);
          const limits = DEFAULT_LIMITS[ct.name] ?? ['Standard'];
          return (
            <div key={ct.id} className={`border rounded-lg p-4 transition-colors ${checked ? 'border-blue-400 bg-blue-50' : 'border-gray-200'}`}>
              <div className="flex items-start gap-3">
                <input type="checkbox" id={`cov-${ct.id}`} checked={checked}
                  onChange={e => toggle(ct.id, e.target.checked, ct.name)}
                  className="mt-1 w-4 h-4 accent-blue-600" />
                <div className="flex-1">
                  <label htmlFor={`cov-${ct.id}`} className="font-medium text-gray-800 cursor-pointer">{ct.name}</label>
                  <p className="text-sm text-gray-500 mt-0.5">{ct.description}</p>
                  <p className="text-sm text-blue-600 font-medium mt-1">${ct.mockAnnualRate.toFixed(0)}/yr</p>
                  {checked && (
                    <div className="mt-2">
                      <select value={selected.get(ct.id)} onChange={e => setLimit(ct.id, e.target.value)}
                        className="border border-gray-300 rounded px-2 py-1 text-sm bg-white">
                        {limits.map(l => <option key={l} value={l}>{l}</option>)}
                      </select>
                    </div>
                  )}
                </div>
              </div>
            </div>
          );
        })}
        {selected.size === 0 && <p className="text-sm text-amber-600">Select at least one coverage to continue.</p>}
        {error && <p className="text-sm text-red-600">Failed to save. Please try again.</p>}
        <div className="flex justify-between mt-6">
          <button type="button" onClick={() => navigate('/vehicles')} className="text-gray-500 hover:text-gray-700 text-sm">← Back</button>
          <button type="button" onClick={onSubmit} disabled={isLoading || selected.size === 0}
            className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-8 py-2.5 rounded-lg font-medium text-sm transition-colors">
            {isLoading ? 'Saving…' : 'Next: Review →'}
          </button>
        </div>
      </div>
    </QuoteLayout>
  );
}
