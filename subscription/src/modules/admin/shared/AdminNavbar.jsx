import { NavLink } from "react-router-dom";
import { useAuth } from "../../../context/AuthProvider";

function AdminNavbar() {
    const { currentUser, logoutClick } = useAuth();
    return (
        <>
            <nav className="navbar navbar-expand-lg bg-primary">
                <div className="container-fluid">
                    <a className="navbar-brand" href="#">ADMIN</a>
                    <button className="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target="#navbarNav" aria-controls="navbarNav" aria-expanded="false" aria-label="Toggle navigation">
                        <span className="navbar-toggler-icon"></span>
                    </button>
                    <div className="collapse navbar-collapse" id="navbarNav">
                        <ul className="navbar-nav">
                            <NavLink to="addplan" className="nav-link">Add Plan</NavLink>
                            <li className="nav-item">
                                <a className="nav-link" href="#">Features</a>
                            </li>
                            <li className="nav-item">
                                <a className="nav-link" href="#">Pricing</a>
                            </li>
                            {currentUser &&
                                <>Welcome {currentUser.user.name}
                                    <button className="btn btn-primary" onClick={logoutClick}>Logout</button>
                                </> 
                            }
                           
                        </ul>
                    </div>
                </div>
            </nav>
        </>
    )
}

export default AdminNavbar;