import { Field, Formik, Form, ErrorMessage } from "formik";
import * as Yup from 'yup';
import { PlanService } from "../../services/PlanService";
import { useState } from "react";
import { useNavigate } from "react-router-dom";

function AdminPlan() {

    const planForm = {
        name: "",
        speed: "",
        price: 0,
        duration: "",
        features: ""
    }

    const navigate = useNavigate();
    const [submitMessage, setSubmitMessage] = useState();
    const [submitErrorMessage, setSubmitErrorMessage] = useState();

    const handleSavePlan = (frm) => {
        PlanService.createInternetPlan(frm).then(
            (response) => {
                if (response.status === 201) {
                    setSubmitMessage('New Plan added');
                    setTimeout(() => {
                        setSubmitMessage("");
                        navigate('admin/plans');
                    }, 3000);
                }
            }).catch((error) => {
                console.log("Error", error)
                setSubmitErrorMessage(error.message);
            })

    }

    const planvalidationSchema = Yup.object(
        {
            name: Yup.string().required('Plan name is mandatory')
                .min(3, "Plan name must be atleast 3 character"),
            speed: Yup.string().required('Speed is mandatory'),
            price: Yup.number().required('Price is mandatory')
                .positive('Price should be > 0'),
            duration: Yup.string().required('Duration is mandatory')
        }
    )

    return (<>
        <h4>Add New Plan</h4>
        {submitMessage && <div className="alert alert-sucess text-center">{submitMessage}</div>}
        {submitErrorMessage && <div className="alert alert-danger text-center">{submitErrorMessage}</div>}
        <div className="row">
            <Formik initialValues={planForm}
                onSubmit={(frm) => handleSavePlan(frm)}
                validationSchema={planvalidationSchema}
            >
                <Form>
                    <div className="col-lg-6">
                        <div className="form-group">
                            <label htmlFor="name">Plan Name</label>
                            <Field name="name" type="text" placeholder="e.g-Premium Plan" className="form-control"></Field>
                            <ErrorMessage className="text-danger" component="div" name="name" />
                        </div>
                        <div className="form-group">
                            <label htmlFor="speed">Speed</label>
                            <Field name="speed" type="text" placeholder="e.g-100MBPS" className="form-control"></Field>
                            <ErrorMessage className="text-danger" component="div" name="speed" />
                        </div>
                        <div className="form-group">
                            <label htmlFor="price">Price</label>
                            <Field name="price" type="number" placeholder="e.g-1000" className="form-control"></Field>
                            <ErrorMessage className="text-danger" component="div" name="price" />
                        </div>
                        <div className="form-group">
                            <label htmlFor="duration">Duration</label>
                            <Field name="duration" className="form-select" as="select">
                                <option value="1 Month">1 Month</option>
                                <option value="3 Month">3 Month</option>
                                <option value="6 Month">6 Month</option>
                                <option value="1 Year">1 Year</option>
                            </Field>
                            <ErrorMessage className="text-danger" component="div" name="duration" />
                        </div>
                        <div className="form-group">
                            <label htmlFor="features">Features(1 perline)</label>
                            <Field name="features" as="textarea" className="form-control" row={5}></Field>

                        </div>
                        <input type="submit" className="btn btn-primary" value="Add Plan" />
                    </div>
                </Form>
            </Formik>
        </div>
    </>)
}

export default AdminPlan;