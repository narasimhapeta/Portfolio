import { useContext } from "react";
import { NavLink } from "react-router-dom";
import { useAuth } from "../../../context/AuthProvider";


function Navbar() {
    const { currentUser, logoutClick } = useAuth();

    return (
        <>

            <nav className="navbar navbar-expand-lg bg-body-tertiary">
                <div className="container-fluid">
                    <a className="navbar-brand" href="#">Navbar</a>
                    <button className="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNavDropdown" aria-controls="navbarNavDropdown" aria-expanded="false" aria-label="Toggle navigation">
                        <span className="navbar-toggler-icon"></span>
                    </button>
                    <div className="collapse navbar-collapse" id="navbarNavDropdown">
                        <ul className="navbar-nav">
                            <li className="nav-item">
                                <NavLink to="/plans" className="nav-link active">Internet Plans</NavLink>
                                <NavLink to="/plansapi" className="nav-link active">Internet Plans(API)</NavLink>
                            </li>


                            <li className="nav-item dropdown">
                                <a className="nav-link dropdown-toggle" href="#" role="button" data-bs-toggle="dropdown" aria-expanded="false">
                                    More Links
                                </a>
                                <ul className="dropdown-menu">
                                    <li><a className="dropdown-item" href="#">Action</a></li>
                                    <li><a className="dropdown-item" href="#">Another action</a></li>
                                    <li><a className="dropdown-item" href="#">Something else here</a></li>
                                </ul>
                            </li>
                            {currentUser ?
                                <>Welcome {currentUser.user.name}
                                    <button className="btn btn-primary" onClick={logoutClick}>Logout</button>
                                </> :
                                <>
                                <li className="nav-item">
                                    <NavLink to="/signup" className="nav-link active">Sign up</NavLink>
                                </li>
                                <li className="nav-item">
                                    <NavLink to="/login" className="nav-link active">Sign in</NavLink>
                                </li>
                                </>
                            }
                        </ul>
                    </div>
                </div>
            </nav>

        </>
    )
}

export default Navbar;