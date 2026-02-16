import { useEffect, useState } from "react";
// import { internetplans } from "../../data/sample";
import { useNavigate } from "react-router-dom";
import { PlanService } from "../../services/PlanService";

function InternetPlansFromApi() {

    // const plans = internetplans;
    const [filteredPlans, setFilteredPlans] = useState([]);
    const navigate = useNavigate();
    useEffect(() => {

        PlanService.getInternetPlans()
        .then((response) => {
            setFilteredPlans(response.data);
        })
        .catch((err) => {
            console.log(err);
        })

            // let filtered = props.selectedSpeed ? plans.filter(d=>d.speed === props.selectedSpeed) : plans;
            // setFilteredPlans(filtered);
            // props.onFilter(filtered.length);

        },[]
    );

    const redirect = (planId)=> {
        navigate(`/plan/${planId}`)
    }

    return (    <>
        <div className="row">
            <div className="col-lg-12">
                Internet Plans (API)
            </div>
        </div>
        <div className="row">
            {filteredPlans.map((plan, indx) => (

                <div key={plan.id} className="col-lg-4">
                    <div className="card text-center">
                        <div className="card-header">
                            {plan.name}
                        </div>
                        <div className="card-body">
                            <h5 className="card-title">Duration: {plan.duration}</h5>
                            <p className="card-text">Price: {plan.price}</p>
                            <p className="card-text">Speed: {plan.speed}</p>
                            <a href="#" className="btn btn-primary">Go somewhere</a>
                            <button className="btn btn-warning" onClick={()=>redirect(plan.id)}>View</button>
                        </div>
                        <ul className="list-group">
                            {plan.features.map((feature, findx) => (
                                <li key={findx} className="list-group-item"> {feature}</li>

                            ))}
                        </ul>
                    </div>
                </div>
            ))}
        </div>

    </>
    )
}

export default InternetPlansFromApi;

