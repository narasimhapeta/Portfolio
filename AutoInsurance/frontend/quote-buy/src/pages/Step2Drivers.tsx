import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useSaveDriversMutation } from '../api/quoteApi';
import { setDrivers, setStep } from '../store/quoteSlice';
import type { RootState } from '../store';
import QuoteLayout from '../components/QuoteLayout';
import FormField, { Input, Select } from '../components/FormField';

const driverSchema = z.object({
  firstName: z.string().min(1, 'Required'),
  lastName: z.string().min(1, 'Required'),
  dateOfBirth: z.string().min(1, 'Required'),
  licenseNumber: z.string().min(1, 'Required'),
  licenseState: z.string().length(2, '2-letter state'),
  isPrimary: z.boolean(),
});

const schema = z.object({ drivers: z.array(driverSchema).min(1, 'Add at least one driver') });
type FormData = z.infer<typeof schema>;

const US_STATES = ['AL','AK','AZ','AR','CA','CO','CT','DE','FL','GA','HI','ID','IL','IN','IA','KS','KY','LA','ME','MD','MA','MI','MN','MS','MO','MT','NE','NV','NH','NJ','NM','NY','NC','ND','OH','OK','OR','PA','RI','SC','SD','TN','TX','UT','VT','VA','WA','WV','WI','WY'];

export default function Step2Drivers() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const session = useSelector((s: RootState) => s.quote.session);
  const [saveDrivers, { isLoading, error }] = useSaveDriversMutation();

  const { register, control, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { drivers: [{ firstName: '', lastName: '', dateOfBirth: '', licenseNumber: '', licenseState: '', isPrimary: true }] },
  });

  const { fields, append, remove } = useFieldArray({ control, name: 'drivers' });

  const onSubmit = async (data: FormData) => {
    if (!session) return;
    try {
      await saveDrivers({ quoteId: session.quoteId, drivers: data.drivers }).unwrap();
      dispatch(setDrivers(data.drivers));
      dispatch(setStep(3));
      navigate('/vehicles');
    } catch { /* error shown below */ }
  };

  return (
    <QuoteLayout step={2} title="Drivers">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        {fields.map((field, i) => (
          <div key={field.id} className="border border-gray-200 rounded-lg p-4 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="font-medium text-gray-700">{i === 0 ? 'Primary Driver' : `Driver ${i + 1}`}</h3>
              {i > 0 && (
                <button type="button" onClick={() => remove(i)}
                  className="text-red-500 hover:text-red-700 text-sm">Remove</button>
              )}
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <FormField label="First Name" error={errors.drivers?.[i]?.firstName?.message} required>
                <Input {...register(`drivers.${i}.firstName`)} />
              </FormField>
              <FormField label="Last Name" error={errors.drivers?.[i]?.lastName?.message} required>
                <Input {...register(`drivers.${i}.lastName`)} />
              </FormField>
              <FormField label="Date of Birth" error={errors.drivers?.[i]?.dateOfBirth?.message} required>
                <Input type="date" {...register(`drivers.${i}.dateOfBirth`)} />
              </FormField>
              <FormField label="License Number" error={errors.drivers?.[i]?.licenseNumber?.message} required>
                <Input {...register(`drivers.${i}.licenseNumber`)} />
              </FormField>
              <FormField label="License State" error={errors.drivers?.[i]?.licenseState?.message} required>
                <Select {...register(`drivers.${i}.licenseState`)}>
                  <option value="">Select state</option>
                  {US_STATES.map(s => <option key={s} value={s}>{s}</option>)}
                </Select>
              </FormField>
              <input type="hidden" {...register(`drivers.${i}.isPrimary`)} value={i === 0 ? 'true' : 'false'} />
            </div>
          </div>
        ))}
        <button type="button"
          onClick={() => append({ firstName: '', lastName: '', dateOfBirth: '', licenseNumber: '', licenseState: '', isPrimary: false })}
          className="text-blue-600 hover:text-blue-800 text-sm font-medium">+ Add another driver</button>
        {error && <p className="text-sm text-red-600">Failed to save. Please try again.</p>}
        <div className="flex justify-between mt-4">
          <button type="button" onClick={() => navigate('/')} className="text-gray-500 hover:text-gray-700 text-sm">← Back</button>
          <button type="submit" disabled={isLoading}
            className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-8 py-2.5 rounded-lg font-medium text-sm transition-colors">
            {isLoading ? 'Saving…' : 'Next: Vehicles →'}
          </button>
        </div>
      </form>
    </QuoteLayout>
  );
}
