import { useState } from 'react'
// import reactLogo from './assets/react.svg'
// import viteLogo from '/vite.svg'
// import './App.css'
import InternetPlans from './modules/public/InternetPlans'
import SearchPlan from './modules/public/SearchPlans'
import HomeLayout from './modules/public/shared/homelayout'
import { BrowserRouter } from 'react-router-dom';
import Footer from './modules/public/shared/Footer';
import Navbar from './modules/public/shared/Navbar';
import AuthProvider from './context/AuthProvider';

function App() {
  const [count, setCount] = useState(0)

  return (
    <>

      {/* <SearchPlan/> */}
      <BrowserRouter>
        <AuthProvider>
          <Navbar />
          <HomeLayout />
          <Footer />
        </AuthProvider>
      </BrowserRouter>
    </>
  )
}

export default App
