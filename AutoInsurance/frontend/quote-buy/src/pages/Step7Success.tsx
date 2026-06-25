import { useSelector } from 'react-redux';
import { useDispatch } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { resetQuote } from '../store/quoteSlice';
import { clearEncryptionKey } from '../store/encryptedStorage';
import type { RootState } from '../store';
import QuoteLayout from '../components/QuoteLayout';

export default function Step7Success() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const policy = useSelector((s: RootState) => s.quote.policy);
  const review = useSelector((s: RootState) => s.quote.review);

  const startNew = () => {
    clearEncryptionKey();
    dispatch(resetQuote());
    navigate('/');
  };

  return (
    <QuoteLayout step={7} title="You're covered!">
      <div className="space-y-6 text-center">
        <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto">
          <svg className="w-8 h-8 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
          </svg>
        </div>
        <div>
          <h2 className="text-xl font-bold text-gray-900">Policy Activated</h2>
          <p className="text-gray-500 mt-1">Your auto insurance policy is now active.</p>
        </div>
        <div className="bg-gray-50 border border-gray-200 rounded-lg p-5 text-left space-y-2">
          <div className="flex justify-between text-sm">
            <span className="text-gray-500">Policy Number</span>
            <span className="font-mono font-semibold text-gray-900">{policy?.policyNumber}</span>
          </div>
          {review && (
            <div className="flex justify-between text-sm">
              <span className="text-gray-500">Annual Premium</span>
              <span className="font-semibold text-gray-900">${review.annualPremium.toFixed(2)}</span>
            </div>
          )}
          <div className="flex justify-between text-sm">
            <span className="text-gray-500">Quote Number</span>
            <span className="font-mono text-gray-600">{review?.quoteNumber}</span>
          </div>
        </div>
        <p className="text-sm text-gray-500">Documents (insurance card, declaration page) will be available in your customer portal.</p>
        <div className="flex flex-col sm:flex-row gap-3 justify-center pt-2">
          <button type="button" onClick={startNew}
            className="text-gray-600 border border-gray-300 hover:border-gray-400 px-6 py-2.5 rounded-lg text-sm font-medium transition-colors">
            Start a New Quote
          </button>
        </div>
      </div>
    </QuoteLayout>
  );
}
