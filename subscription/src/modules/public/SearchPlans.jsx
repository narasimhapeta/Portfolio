import { useState } from "react";
import { internetplans } from "../../data/sample";
import InternetPlans from "./InternetPlans";

function SearchPlan(){

    const speedOptions = Array.from(new Set(internetplans.map(d=>d.speed)));
    const [selectedSpeed, setSelectedSpeed] = useState();
    const [totalRecords, setTotalRecords] = useState(0);

    const updateTotal = (count)=>{
        setTotalRecords(count);
    }

    const handleChange = (e) =>{
        setSelectedSpeed(e.target.value);
    }

    return (
        <>
            <h4>Internet Plan Options</h4>
            
            <div className="form-group">
                <span>Speed</span>
                <select className="form-select" onChange={(e)=> handleChange(e)}>
                    <option value="">--All Plans--</option>
                    {speedOptions.map((plan, indx)=>(
                        <option key={indx} value={plan}>
                            {plan}
                        </option>
                    )

                    )}
                </select>

            </div>
            <hr/>
            <div className="alert alert-info text-center">
                   <i>Filtered Records: {totalRecords}</i> 
            </div>
            <InternetPlans selectedSpeed={selectedSpeed} onFilter={updateTotal}/>
           
        </>
    )
}

export default SearchPlan;