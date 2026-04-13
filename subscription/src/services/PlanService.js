import axiosClient from "../utils/axiosClient";

function getInternetPlans(){

    return axiosClient.get('/api/Plans');
}

function createInternetPlan(plan){
    return axiosClient.post('/api/Plans', plan);
}

function deleteInternetPlan(planId){
    return axiosClient.delete(`/api/Plan/${planId}`);
}

export const PlanService = {
    getInternetPlans,
    createInternetPlan,
    deleteInternetPlan
}