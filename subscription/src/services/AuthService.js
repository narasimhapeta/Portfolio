import axiosClient from "../utils/axiosClient";

function authenticate(login){
    return axiosClient.post('api/auth/login', login);
}

export const AuthService = {
    authenticate
}