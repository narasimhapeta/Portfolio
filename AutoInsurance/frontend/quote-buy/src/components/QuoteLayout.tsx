import type { ReactNode } from 'react';
import StepIndicator from './StepIndicator';

interface Props { step: number; title: string; children: ReactNode; }

export default function QuoteLayout({ step, title, children }: Props) {
  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 px-6 py-4">
        <div className="max-w-3xl mx-auto flex items-center gap-3">
          <div className="w-8 h-8 bg-blue-600 rounded-full flex items-center justify-center text-white font-bold text-sm">AI</div>
          <span className="text-lg font-semibold text-gray-900">AutoInsure</span>
          <span className="text-gray-300 ml-2">|</span>
          <span className="text-gray-500 text-sm ml-2">Get a Quote</span>
        </div>
      </header>
      <main className="max-w-3xl mx-auto px-4 py-8">
        <StepIndicator current={step} />
        <div className="bg-white rounded-xl shadow-sm border border-gray-100 p-6 sm:p-8">
          <h1 className="text-2xl font-bold text-gray-900 mb-6">{title}</h1>
          {children}
        </div>
      </main>
    </div>
  );
}
