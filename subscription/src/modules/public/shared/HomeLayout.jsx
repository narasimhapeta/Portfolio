import { Route, Routes } from "react-router-dom";
import SearchPlan from "../SearchPlans";
import InternetPlanDetail from "../InternetPlanDetails";
import Login from "../Login";
import RegisterForm from "../RegisterForm";
import InternetPlansFromApi from "../InternetPlansFromApi";
import Navbar from "./Navbar";
import Footer from "./Footer";

function HomeLayout(){
    return <>
        <Navbar/>
        <Routes>            
            <Route path="/plans" element={<SearchPlan/>}></Route>
            <Route path="/plan/:planId" element={<InternetPlanDetail/>}></Route>
             <Route path="/plansapi" element={<InternetPlansFromApi/>}></Route>
            <Route path="/login" element={<Login/>}></Route>
            <Route path="/signup" element={<RegisterForm/>}></Route>
        </Routes>
        <Footer/>
    </>
}

export default HomeLayout;