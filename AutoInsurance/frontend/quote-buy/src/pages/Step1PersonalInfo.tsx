import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useDispatch } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useCreateQuoteMutation } from '../api/quoteApi';
import { setSession, setPersonalInfo, setStep } from '../store/quoteSlice';
import QuoteLayout from '../components/QuoteLayout';
import FormField, { Input, Select } from '../components/FormField';

const schema = z.object({
  firstName: z.string().min(1, 'Required'),
  lastName: z.string().min(1, 'Required'),
  dateOfBirth: z.string().min(1, 'Required'),
  email: z.string().email('Invalid email'),
  phone: z.string().min(10, 'Enter a valid phone number'),
  address: z.string().min(1, 'Required'),
  city: z.string().min(1, 'Required'),
  state: z.string().length(2, 'Use 2-letter state code'),
  zipCode: z.string().regex(/^\d{5}$/, 'Enter 5-digit zip'),
});

type FormData = z.infer<typeof schema>;

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VT','VA','WA','WV','WI','WY'];

export default function Step1PersonalInfo() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const [createQuote, { isLoading, error }] = useCreateQuoteMutation();

  const { register, handleSubmit, formState: { errors } } = useForm<FormData>({ resolver: zodResolver(schema) });

  const onSubmit = async (data: FormData) => {
    try {
      const res = await createQuote(data).unwrap();
      dispatch(setSession({ quoteId: res.quoteId, quoteNumber: res.quoteNumber, sessionToken: res.sessionToken, zipCode: data.zipCode, stepReached: res.stepReached }));
      dispatch(setPersonalInfo(data));
      dispatch(setStep(2));
      navigate('/drivers');
    } catch { /* handled via error state */ }
  };

  return (
    <QuoteLayout step={1} title="Tell us about yourself">
      <form onSubmit={handleSubmit(onSubmit)} className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <FormField label="First Name" error={errors.firstName?.message} required>
          <Input {...register('firstName')} placeholder="John" />
        </FormField>
        <FormField label="Last Name" error={errors.lastName?.message} required>
          <Input {...register('lastName')} placeholder="Doe" />
        </FormField>
        <FormField label="Date of Birth" error={errors.dateOfBirth?.message} required>
          <Input type="date" {...register('dateOfBirth')} />
        </FormField>
        <FormField label="Email" error={errors.email?.message} required>
          <Input type="email" {...register('email')} placeholder="john@example.com" />
        </FormField>
        <FormField label="Phone" error={errors.phone?.message} required>
          <Input type="tel" {...register('phone')} placeholder="5551234567" />
        </FormField>
        <FormField label="Street Address" error={errors.address?.message} required>
          <Input {...register('address')} placeholder="123 Main St" />
        </FormField>
        <FormField label="City" error={errors.city?.message} required>
          <Input {...register('city')} placeholder="Springfield" />
        </FormField>
        <FormField label="State" error={errors.state?.message} required>
          <Select {...register('state')}>
            <option value="">Select state</option>
            {US_STATES.map(s => <option key={s} value={s}>{s}</option>)}
          </Select>
        </FormField>
        <FormField label="Zip Code" error={errors.zipCode?.message} required>
          <Input {...register('zipCode')} placeholder="12345" maxLength={5} />
        </FormField>
        {error && <p className="col-span-2 text-sm text-red-600">Something went wrong. Please try again.</p>}
        <div className="col-span-2 flex justify-end mt-2">
          <button type="submit" disabled={isLoading}
            className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-8 py-2.5 rounded-lg font-medium text-sm transition-colors">
            {isLoading ? 'Creating quote…' : 'Next: Drivers →'}
          </button>
        </div>
      </form>
    </QuoteLayout>
  );
}
