import { Route, Routes } from "react-router-dom";
import AdminPlan from "../AdminPlan";
import AdminNavbar from "./AdminNavbar";
import AdminFooter from "./AdminFooter";
import InternetPlansList from "../InternetPlansList";

function AdminLayout(){

    return (<>
        <h4>Admin Layout</h4>
        <AdminNavbar/>
        <Routes>
            <Route path="addplan" element={<AdminPlan/>}></Route>
            <Route path="plans" element={<InternetPlansList/>}></Route>
        </Routes>
        <AdminFooter/>
    </>
    )
}

export default AdminLayout;