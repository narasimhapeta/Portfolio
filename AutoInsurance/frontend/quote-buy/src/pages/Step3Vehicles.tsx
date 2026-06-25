import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useDispatch, useSelector } from 'react-redux';
import { useNavigate } from 'react-router-dom';
import { useSaveVehiclesMutation } from '../api/quoteApi';
import { setVehicles, setStep } from '../store/quoteSlice';
import type { RootState } from '../store';
import QuoteLayout from '../components/QuoteLayout';
import FormField, { Input, Select } from '../components/FormField';

const vehicleSchema = z.object({
  year: z.number().int().min(1990).max(new Date().getFullYear() + 1),
  make: z.string().min(1, 'Required'),
  model: z.string().min(1, 'Required'),
  vin: z.string().length(17, 'VIN must be 17 characters'),
  primaryUse: z.string().min(1, 'Required'),
});

const schema = z.object({ vehicles: z.array(vehicleSchema).min(1) });
type FormData = z.infer<typeof schema>;

const PRIMARY_USES = ['Commute', 'Pleasure', 'Business', 'Farm'];

export default function Step3Vehicles() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const session = useSelector((s: RootState) => s.quote.session);
  const [saveVehicles, { isLoading, error }] = useSaveVehiclesMutation();

  const { register, control, handleSubmit, formState: { errors } } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { vehicles: [{ year: new Date().getFullYear(), make: '', model: '', vin: '', primaryUse: '' }] },
  });

  const { fields, append, remove } = useFieldArray({ control, name: 'vehicles' });

  const onSubmit = async (data: FormData) => {
    if (!session) return;
    try {
      await saveVehicles({ quoteId: session.quoteId, vehicles: data.vehicles }).unwrap();
      dispatch(setVehicles(data.vehicles));
      dispatch(setStep(4));
      navigate('/coverages');
    } catch { /* error shown below */ }
  };

  return (
    <QuoteLayout step={3} title="Vehicles">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
        {fields.map((field, i) => (
          <div key={field.id} className="border border-gray-200 rounded-lg p-4 space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="font-medium text-gray-700">Vehicle {i + 1}</h3>
              {i > 0 && (
                <button type="button" onClick={() => remove(i)} className="text-red-500 hover:text-red-700 text-sm">Remove</button>
              )}
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <FormField label="Year" error={errors.vehicles?.[i]?.year?.message} required>
                <Input type="number" {...register(`vehicles.${i}.year`, { valueAsNumber: true })} />
              </FormField>
              <FormField label="Make" error={errors.vehicles?.[i]?.make?.message} required>
                <Input {...register(`vehicles.${i}.make`)} placeholder="Toyota" />
              </FormField>
              <FormField label="Model" error={errors.vehicles?.[i]?.model?.message} required>
                <Input {...register(`vehicles.${i}.model`)} placeholder="Camry" />
              </FormField>
              <FormField label="VIN" error={errors.vehicles?.[i]?.vin?.message} required>
                <Input {...register(`vehicles.${i}.vin`)} placeholder="17-character VIN" maxLength={17} className="uppercase" />
              </FormField>
              <FormField label="Primary Use" error={errors.vehicles?.[i]?.primaryUse?.message} required>
                <Select {...register(`vehicles.${i}.primaryUse`)}>
                  <option value="">Select use</option>
                  {PRIMARY_USES.map(u => <option key={u} value={u}>{u}</option>)}
                </Select>
              </FormField>
            </div>
          </div>
        ))}
        <button type="button"
          onClick={() => append({ year: new Date().getFullYear(), make: '', model: '', vin: '', primaryUse: '' })}
          className="text-blue-600 hover:text-blue-800 text-sm font-medium">+ Add another vehicle</button>
        {error && <p className="text-sm text-red-600">Failed to save. Please try again.</p>}
        <div className="flex justify-between mt-4">
          <button type="button" onClick={() => navigate('/drivers')} className="text-gray-500 hover:text-gray-700 text-sm">← Back</button>
          <button type="submit" disabled={isLoading}
            className="bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white px-8 py-2.5 rounded-lg font-medium text-sm transition-colors">
            {isLoading ? 'Saving…' : 'Next: Coverages →'}
          </button>
        </div>
      </form>
    </QuoteLayout>
  );
}
