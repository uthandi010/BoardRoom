import { Link, useNavigate } from "react-router-dom";
import { LayoutGrid, LogOut } from "lucide-react";
import { useAuth } from "../auth/AuthContext";

export function TopNav() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate("/login");
  };

  return (
    <header className="topnav">
      <Link to="/workspaces" className="topnav-brand">
        <LayoutGrid size={20} />
        <span>BoardRoom</span>
      </Link>
      {user && (
        <div className="topnav-actions">
          <span className="topnav-user">{user.name}</span>
          <button type="button" className="btn btn-ghost" onClick={handleLogout}>
            <LogOut size={16} />
            Log out
          </button>
        </div>
      )}
    </header>
  );
}
