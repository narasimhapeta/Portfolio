import { useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useGetReviewQuery, useBindQuoteMutation } from '../api/quoteApi';
import { useDispatch } from 'react-redux';
import { setPolicy, setStep } from '../store/quoteSlice';
import type { RootState } from '../store';
import QuoteLayout from '../components/QuoteLayout';

export default function Step5Review() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const session = useSelector((s: RootState) => s.quote.session);
  const { data: review, isLoading, error } = useGetReviewQuery(session?.quoteId ?? '', { skip: !session });
  const [bindQuote, { isLoading: binding, error: bindError }] = useBindQuoteMutation();

  const onBind = async () => {
    if (!session || !review) return;
    try {
      const policy = await bindQuote(session.quoteId).unwrap();
      dispatch(setPolicy(policy));
      dispatch(setStep(6));
      navigate('/payment');
    } catch { /* shown below */ }
  };

  if (isLoading) return <QuoteLayout step={5} title="Review Your Quote"><p className="text-gray-500">Loading your quote…</p></QuoteLayout>;
  if (error || !review) return <QuoteLayout step={5} title="Review Your Quote"><p className="text-red-500">Failed to load review. Please go back and retry.</p></QuoteLayout>;

  return (
    <QuoteLayout step={5} title="Review Your Quote">
      <div className="space-y-6">
        <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 flex items-center justify-between">
          <div>
            <p className="text-sm text-blue-700">Quote #{review.quoteNumber}</p>
            <p className="text-3xl font-bold text-blue-900">${review.annualPremium.toFixed(2)}<span className="text-base font-normal text-blue-600">/year</span></p>
          </div>
          <div className="text-right text-sm text-blue-600">
            <p>${(review.annualPremium / 12).toFixed(2)}/month</p>
          </div>
        </div>

        <section>
          <h3 className="font-semibold text-gray-700 mb-2">Drivers</h3>
          <ul className="space-y-1">
            {review.drivers.map((d, i) => (
              <li key={i} className="text-sm text-gray-600">{d.firstName} {d.lastName}{i === 0 ? ' (Primary)' : ''}</li>
            ))}
          </ul>
        </section>

        <section>
          <h3 className="font-semibold text-gray-700 mb-2">Vehicles</h3>
          <ul className="space-y-1">
            {review.vehicles.map((v, i) => (
              <li key={i} className="text-sm text-gray-600">{v.year} {v.make} {v.model} — {v.primaryUse}</li>
            ))}
          </ul>
        </section>

        <section>
          <h3 className="font-semibold text-gray-700 mb-2">Coverages</h3>
          <table className="w-full text-sm">
            <tbody>
              {review.coverages.map((c, i) => (
                <tr key={i} className="border-b border-gray-100 last:border-0">
                  <td className="py-1.5 text-gray-700">{c.name}</td>
                  <td className="py-1.5 text-gray-500">{c.limits}</td>
                  <td className="py-1.5 text-right text-gray-700 font-medium">${c.annualPremium.toFixed(0)}/yr</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>

        {bindError && <p className="text-sm text-red-600">Failed to bind. Please try again.</p>}
        <div className="flex justify-between mt-4">
          <button type="button" onClick={() => navigate('/coverages')} className="text-gray-500 hover:text-gray-700 text-sm">← Back</button>
          <button type="button" onClick={onBind} disabled={binding}
            className="bg-green-600 hover:bg-green-700 disabled:opacity-50 text-white px-8 py-2.5 rounded-lg font-medium text-sm transition-colors">
            {binding ? 'Binding…' : 'Confirm & Pay →'}
          </button>
        </div>
      </div>
    </QuoteLayout>
  );
}
