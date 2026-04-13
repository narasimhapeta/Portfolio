import { useEffect, useState } from "react";
import { PlanService } from "../../services/PlanService";

const InternetPlansList = () => {

    const [plans, setPlans] = useState([]);

    useEffect(() => {
        PlanService.getInternetPlans().then((resp) => {
            setPlans(resp.data);
        }).catch((error) => {
            console.log(error);
        })

    }, [])

    const handleDeletePlan = (plan) => {
        let confirmDelete = confirm(`Do you want to delete the plan ${plan.name}?`);
        if(confirmDelete){
            PlanService.deleteInternetPlan(plan.id).then((response) => {
                if(response.status === 204){
                    alert('Plan deleted sucessfully');
                    setPlans(prev => prev.filter(p => p.id != plan.id ));
                }

            })
        }


    }

    return (
        <>
            <table className="table table-responsive">
                <thead>
                    <tr>
                        <th>Plan Name</th>
                        <th>Plan Speed</th>
                        <th>Duration</th>
                        <th>Amount</th>
                        <th>Delete</th>
                    </tr>
                </thead>
                <tbody>
                    {plans.map((plans, idx) =>
                        <tr key={plan.id}>
                            <td>{plan.name}</td>
                            <td>{plan.speed}</td>
                            <td>{plan.duration}</td>
                            <td>INR {plan.price}</td>
                            <td>
                                <button className="btn btn-danger" title="Delete Plan" onClick={()=>handleDeletePlan(plan)}>X</button>
                            </td>
                        </tr>
                    )}
                </tbody>
            </table>
        </>
    )
}

export default InternetPlansList;