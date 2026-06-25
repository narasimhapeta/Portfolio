import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import Step1PersonalInfo from './pages/Step1PersonalInfo';
import Step2Drivers from './pages/Step2Drivers';
import Step3Vehicles from './pages/Step3Vehicles';
import Step4Coverages from './pages/Step4Coverages';
import Step5Review from './pages/Step5Review';
import Step6Payment from './pages/Step6Payment';
import Step7Success from './pages/Step7Success';
import ResumePage from './pages/ResumePage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Step1PersonalInfo />} />
        <Route path="/drivers" element={<Step2Drivers />} />
        <Route path="/vehicles" element={<Step3Vehicles />} />
        <Route path="/coverages" element={<Step4Coverages />} />
        <Route path="/review" element={<Step5Review />} />
        <Route path="/payment" element={<Step6Payment />} />
        <Route path="/success" element={<Step7Success />} />
        <Route path="/resume" element={<ResumePage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}
