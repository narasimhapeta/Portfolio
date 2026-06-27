import { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useInitiatePaymentMutation, useConfirmPaymentMutation } from '../api/paymentApi';
import { useGetReviewQuery } from '../api/quoteApi';
import { setStep } from '../store/quoteSlice';
import type { RootState } from '../store';
import QuoteLayout from '../components/QuoteLayout';

type PaymentPhase = 'idle' | 'initiating' | 'ready' | 'confirming' | 'error';

export default function Step6Payment() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const policy = useSelector((s: RootState) => s.quote.policy);
  const session = useSelector((s: RootState) => s.quote.session);
  const { data: review } = useGetReviewQuery(session?.quoteId ?? '', { skip: !session });
  const [initiatePayment] = useInitiatePaymentMutation();
  const [confirmPayment] = useConfirmPaymentMutation();

  const [phase, setPhase] = useState<PaymentPhase>('idle');
  const [paymentIntentId, setPaymentIntentId] = useState('');
  const [amount, setAmount] = useState(0);
  const [errorMsg, setErrorMsg] = useState('');

  useEffect(() => {
    if (!policy || !review) return;
    setPhase('initiating');
    initiatePayment({ policyId: policy.policyId, amount: review.annualPremium })
      .unwrap()
      .then(res => { setPaymentIntentId(res.paymentIntentId); setAmount(res.amount); setPhase('ready'); })
      .catch(() => { setPhase('error'); setErrorMsg('Failed to initiate payment.'); });
  }, [policy?.policyId, review?.annualPremium]);

  const onConfirm = async () => {
    if (!policy || !paymentIntentId) return;
    setPhase('confirming');
    try {
      const res = await confirmPayment({ policyId: policy.policyId, paymentIntentId }).unwrap();
      if (res.success) {
        dispatch(setStep(7));
        navigate('/success');
      } else {
        setPhase('error');
        setErrorMsg('Payment was not successful. Please try again.');
      }
    } catch {
      setPhase('error');
      setErrorMsg('Payment failed. Please try again.');
    }
  };

  return (
    <QuoteLayout step={6} title="Payment">
      <div className="space-y-6">
        {phase === 'initiating' && (
          <div className="flex items-center gap-3 text-gray-500">
            <div className="w-5 h-5 border-2 border-blue-500 border-t-transparent rounded-full animate-spin" />
            <span>Preparing payment…</span>
          </div>
        )}

        {(phase === 'ready' || phase === 'confirming') && (
          <>
            <div className="bg-gray-50 border border-gray-200 rounded-lg p-5 space-y-3">
              <div className="flex items-center justify-between text-sm text-gray-600">
                <span>Policy</span>
                <span className="font-medium text-gray-900">{policy?.policyNumber}</span>
              </div>
              <div className="flex items-center justify-between text-sm text-gray-600">
                <span>Payment Reference</span>
                <span className="font-mono text-xs text-gray-500">{paymentIntentId}</span>
              </div>
              <div className="border-t border-gray-200 pt-3 flex items-center justify-between">
                <span className="font-semibold text-gray-800">Annual Premium</span>
                <span className="text-2xl font-bold text-gray-900">${amount.toFixed(2)}</span>
              </div>
            </div>

            <div className="border border-gray-200 rounded-lg p-4 bg-amber-50">
              <p className="text-sm text-amber-800 font-medium">Mock Payment Mode</p>
              <p className="text-xs text-amber-700 mt-1">This is a simulated payment. No real card is charged.</p>
            </div>

            <button type="button" onClick={onConfirm} disabled={phase === 'confirming'}
              className="w-full bg-green-600 hover:bg-green-700 disabled:opacity-50 text-white py-3 rounded-lg font-semibold text-sm transition-colors flex items-center justify-center gap-2">
              {phase === 'confirming' ? (
                <><div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" /> Processing…</>
              ) : (
                `Pay $${amount.toFixed(2)} and Activate Policy`
              )}
            </button>
          </>
        )}

        {phase === 'error' && (
          <div className="space-y-4">
            <p className="text-red-600 text-sm">{errorMsg}</p>
            <button type="button" onClick={() => { setPhase('idle'); setErrorMsg(''); }}
              className="text-blue-600 hover:text-blue-800 text-sm">Try again</button>
          </div>
        )}
      </div>
    </QuoteLayout>
  );
}
