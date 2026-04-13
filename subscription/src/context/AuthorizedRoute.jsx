import { Navigate } from "react-router-dom";
import { useAuth } from "./AuthProvider";

function AuthorizedRoute({children, role=""}){

    const { currentUser } = useAuth();
    
    if(!currentUser){
        return <Navigate to="/login"/>
    }

    if(role && role.length > 0){
        const hasRole = (currentUser.user.role.toLowerCase() === role.toLowerCase());
        if(!hasRole){
            return <Navigate to="/unauthorize"/>;
        }

    }
    
    return <>{children}</>;
}

export default AuthorizedRoute();