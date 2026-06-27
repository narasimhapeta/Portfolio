import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useDispatch } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useResumeQuoteMutation } from '../api/quoteApi';
import { setSession, setStep } from '../store/quoteSlice';
import QuoteLayout from '../components/QuoteLayout';
import FormField, { Input } from '../components/FormField';

const schema = z.object({
  quoteNumber: z.string().min(1, 'Enter your quote number'),
  zipCode: z.string().regex(/^\d{5}$/, 'Enter 5-digit zip'),
});
type FormData = z.infer<typeof schema>;

const STEP_ROUTES: Record<number, string> = { 1: '/', 2: '/drivers', 3: '/vehicles', 4: '/coverages', 5: '/review', 6: '/payment' };

export default function ResumePage() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const [resumeQuote, { isLoading, error }] = useResumeQuoteMutation();

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({ resolver: zodResolver(schema) });

  const onSubmit = async (data: FormData) => {
    try {
      const res = await resumeQuote(data).unwrap();
      dispatch(setSession({
        quoteId: res.quoteId,
        quoteNumber: res.quoteNumber,
        sessionToken: '',
        zipCode: data.zipCode,
        stepReached: res.stepReached,
      }));
      dispatch(setStep(res.stepReached));
      navigate(STEP_ROUTES[res.stepReached] ?? '/');
    } catch { /* error shown below */ }
  };

  return (
    <QuoteLayout step={1} title="Resume Your Quote">
      <p className="text-gray-500 text-sm mb-6">Enter your quote number and zip code to continue where you left off.</p>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 max-w-sm">
        <FormField label="Quote Number" error={errors.quoteNumber?.message} required>
          <Input {...register('quoteNumber')} placeholder="Q-20260625-ABCDEFGH" className="uppercase" />
        </FormField>
        <FormField label="Zip Code" error={errors.zipCode?.message} required>
          <Input {...register('zipCode')} placeholder="12345" maxLength={5} />
        </FormField>
        {error && <p className="text-sm text-red-600">Quote not found or session expired. Check your details and try again.</p>}
        <div className="flex gap-3 pt-2">
          <button type="submit" disabled={isLoading}
            className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-6 py-2.5 rounded-lg font-medium text-sm transition-colors">
            {isLoading ? 'Resuming…' : 'Resume Quote'}
          </button>
          <button type="button" onClick={() => navigate('/')}
            className="text-gray-500 hover:text-gray-700 text-sm px-4 py-2.5">
            Start New
          </button>
        </div>
      </form>
    </QuoteLayout>
  );
}
