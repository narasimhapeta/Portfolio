import { ErrorMessage, Field, Form, Formik } from "formik";
import { useContext, useState } from "react";
import * as Yup from 'yup';
import { useAuth } from "../../context/AuthProvider";
import { AuthService } from "../../services/AuthService";


function Login() {

    const { loginClick } = useAuth();

    const loginForm = {
        email: "",
        password: ""
    }

    const [message, setMessage] = useState();

    const handleSubmit = (frm) => {
        
        // if (frm.emailId === "admin@gmail.com" && frm.password === "admin") {
        //     setMessage("Login is Successful");
        //     setTimeout(() => {
        //         loginClick(frm.email);
        //     }, 2000);

        // }
        // else {
        //     setMessage("Invalid Credentials");
        // }

        AuthService.authenticate(frm).then( (response) => {
            if(response.status == 200 && response.data){
                const user = response.data;
                setMessage("Login is Successful");
                setTimeout(() => {
                    loginClick(user);
                }, 2000);

            }
        }

        );

    }

    const validationSchema = Yup.object({
        email: Yup.string().required('Email Id is mandatory')
            .email('Email Id is invalid'),
        password: Yup.string().required('Password is mandatory')
    })

    return (
        <>
            <div className="row">
                <div className="col-lg-4"></div>
                <div className="col-lg-4">
                    <h4>Sign In</h4>
                    <p> Sign in to your account</p>
                    {message && <><div className="alert alert-success">{message}</div></>}
                    <Formik initialValues={loginForm} onSubmit={handleSubmit} validationSchema={validationSchema}>
                        {({ errors, touched }) => (
                            <Form>
                                <div className="form-group">
                                    <label>Email</label>
                                    <Field name="email" className={errors.email ? "form-control is-invalid" : "form-control"}></Field>
                                    <ErrorMessage className="text-danger" component="label" name="email" />
                                </div>
                                <div className="form-group">
                                    <label>Password</label>
                                    <Field type="password" name="password" className={errors.password ? "form-control is-invalid" : "form-control"}></Field>
                                    <ErrorMessage className="text-danger" component="label" name="password" />
                                </div>
                                <input type="submit" className="btn btn-primary" value="Sign In" />
                            </Form>
                        )}
                    </Formik>
                </div>
                <div className="col-lg-4"></div>
            </div>
        </>
    )
}

export default Login;