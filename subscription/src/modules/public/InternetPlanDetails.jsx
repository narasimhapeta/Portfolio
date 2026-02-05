import { useEffect, useState } from "react";
import { internetplans } from "../../data/sample";
import { useParams } from "react-router-dom";


function InternetPlanDetail() {

    const { planId } = useParams();
    const [plan, setPlan] = useState({});

    useEffect(() => {

        let filterSelectedPlan = internetplans.filter(d => d.id === Number(planId))
        setPlan(...filterSelectedPlan);

    }, [planId]);

    return (
        <>
            <h4>Plan Details</h4>

            <div className="card">
                <div className="card-body">
                    <h5 className="card-title">{plan.name}</h5>
                    <h6 className="card-subtitle mb-2 text-body-secondary">Duration: {plan.duration}</h6>
                    <p className="card-text">Speed: {plan.speed}</p>
                    <p className="card-text">Price: {plan.price}</p>
                    <p className="card-text">Some quick example text to build on the card title and make up the bulk of the card’s content.</p>
                    <a href="#" className="card-link">Card link</a>
                    <a href="#" className="card-link">Another link</a>
                </div>
            </div>
        </>
    )
}

export default InternetPlanDetail;