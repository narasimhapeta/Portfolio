import axios from "axios";
import { BASEURL } from "./constants";

const axiosClient = axios.create({
    baseURL: `${BASEURL}`,
    headers: {
        "Content-Type": "application/json",
        "Accept": "application/json"
    }
})

//request interceptor
axiosClient.interceptors.request.use(
    (config) => {

        const currentUser = JSON.parse(sessionStorage.getItem('auth'));
        if (currentUser) {
            config.headers.Authorization = `Bearer ${currentUser.token}`
        }
        return config;
    }, (error) => { Promise.reject(error); }
)

//response interceptor
axiosClient.interceptors.response.use(
    response => response,
    (error) => {
        if(error.response?.status === 401){
            sessionStorage.removeItem('auth');
            window.location.href = "/login";
        }
        return Promise.reject(error);
    }
)

export default axiosClient;