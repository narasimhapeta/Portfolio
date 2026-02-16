import { createContext, useContext, useState } from "react";
import { useNavigate } from "react-router-dom";

const AuthContext = createContext();

export const useAuth = () => {
    return useContext(AuthContext);
}

function AuthProvider({children}){
    const navigate = useNavigate();
    const [currentUser, setCurrentUser] = useState(JSON.parse(sessionStorage.getItem("auth")));

    const loginClick = (user) => {
        setCurrentUser(user);
        sessionStorage.setItem("auth", JSON.stringify(user));
       
    }

    const logoutClick = () => {
        setCurrentUser(null);
        sessionStorage.removeItem("auth");
        navigate("/login");
    }

    return (
        <AuthContext.Provider value={{currentUser, loginClick, logoutClick}}>
            {children}
        </AuthContext.Provider>
    )
}

export default AuthProvider;