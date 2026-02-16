import axios from "axios";

const axiosClient = axios.create({
    baseURL: "https://localhost:7178",
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


export default axiosClient;