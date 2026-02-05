import { Route, Routes } from "react-router-dom";
import SearchPlan from "../SearchPlans";
import InternetPlanDetail from "../InternetPlanDetails";

function HomeLayout(){
    return <>
        <Routes>            
            <Route path="/plans" element={<SearchPlan/>}></Route>
            <Route path="/plan/:planId" element={<InternetPlanDetail/>}></Route>
        </Routes>
    </>
}

export default HomeLayout;