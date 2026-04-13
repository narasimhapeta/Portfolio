import { Route, Routes } from "react-router-dom";
import UserNavbar from "./UserNavbar";
import UserFooter from "./UserFooter";


function UserLayout(){

    return (<>
        <h4>User Layout</h4>
        <UserNavbar/>
        <Routes>
            {/* <Route path="addplan" element={<AdminPlan/>}></Route> */}
        </Routes>
        <UserFooter/>
    </>
    )
}

export default UserLayout;