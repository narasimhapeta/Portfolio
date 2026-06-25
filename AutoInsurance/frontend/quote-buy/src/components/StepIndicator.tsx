const STEPS = ['Personal Info', 'Drivers', 'Vehicles', 'Coverages', 'Review', 'Payment', 'Done'];

interface Props { current: number; }

export default function StepIndicator({ current }: Props) {
  return (
    <div className="flex items-center justify-center gap-0 mb-8">
      {STEPS.map((label, i) => {
        const step = i + 1;
        const done = step < current;
        const active = step === current;
        return (
          <div key={step} className="flex items-center">
            <div className="flex flex-col items-center">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-semibold border-2 transition-colors
                ${done ? 'bg-blue-600 border-blue-600 text-white' : active ? 'border-blue-600 text-blue-600 bg-white' : 'border-gray-300 text-gray-400 bg-white'}`}>
                {done ? '✓' : step}
              </div>
              <span className={`text-xs mt-1 hidden sm:block ${active ? 'text-blue-600 font-medium' : 'text-gray-400'}`}>{label}</span>
            </div>
            {i < STEPS.length - 1 && (
              <div className={`h-0.5 w-8 sm:w-16 mx-1 ${step < current ? 'bg-blue-600' : 'bg-gray-200'}`} />
            )}
          </div>
        );
      })}
    </div>
  );
}
