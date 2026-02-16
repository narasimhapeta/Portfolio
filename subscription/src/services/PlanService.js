import axiosClient from "../utils/axiosClient";

function getInternetPlans(){

    return axiosClient.get('/api/Plans');
}

function createInternetPlan(plan){
    return axiosClient.post('/api/Plans', plan);
}

export const PlanService = {
    getInternetPlans,
    createInternetPlan
}