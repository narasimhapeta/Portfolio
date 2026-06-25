import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import Dashboard from './pages/Dashboard';
import PolicyDetail from './pages/PolicyDetail';
import CoverageChange from './pages/CoverageChange';
import Documents from './pages/Documents';
import Claims from './pages/Claims';
import SubmitClaim from './pages/SubmitClaim';
import ClaimDetail from './pages/ClaimDetail';
import Payments from './pages/Payments';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Dashboard />} />
        <Route path="/policies/:id" element={<PolicyDetail />} />
        <Route path="/policies/:id/coverages" element={<CoverageChange />} />
        <Route path="/documents" element={<Documents />} />
        <Route path="/claims" element={<Claims />} />
        <Route path="/claims/submit" element={<SubmitClaim />} />
        <Route path="/claims/:id" element={<ClaimDetail />} />
        <Route path="/payments" element={<Payments />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
