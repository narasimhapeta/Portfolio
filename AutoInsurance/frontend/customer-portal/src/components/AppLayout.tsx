import type { ReactNode } from 'react';
import { NavLink } from 'react-router-dom';

const NAV = [
  { to: '/', label: 'Dashboard', icon: '⊞' },
  { to: '/documents', label: 'Documents', icon: '📄' },
  { to: '/claims', label: 'Claims', icon: '🛡' },
  { to: '/payments', label: 'Payments', icon: '💳' },
];

export default function AppLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen bg-gray-50">
      <aside className="w-56 bg-white border-r border-gray-200 flex flex-col">
        <div className="px-5 py-4 border-b border-gray-100">
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 bg-blue-600 rounded-full flex items-center justify-center text-white text-xs font-bold">AI</div>
            <span className="font-semibold text-gray-900 text-sm">AutoInsure</span>
          </div>
          <p className="text-xs text-gray-400 mt-0.5">Customer Portal</p>
        </div>
        <nav className="flex-1 px-3 py-4 space-y-0.5">
          {NAV.map(({ to, label, icon }) => (
            <NavLink key={to} to={to} end={to === '/'}
              className={({ isActive }) =>
                `flex items-center gap-2.5 px-3 py-2 rounded-lg text-sm transition-colors ${isActive ? 'bg-blue-50 text-blue-700 font-medium' : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'}`
              }>
              <span className="text-base">{icon}</span>{label}
            </NavLink>
          ))}
        </nav>
        <div className="px-4 py-3 border-t border-gray-100">
          <p className="text-xs text-gray-400">Mock Auth Mode</p>
          <p className="text-xs text-gray-500 font-mono truncate">dev-b2c-001</p>
        </div>
      </aside>
      <main className="flex-1 overflow-y-auto">
        <div className="max-w-5xl mx-auto px-6 py-8">{children}</div>
      </main>
    </div>
  );
}
