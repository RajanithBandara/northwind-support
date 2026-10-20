import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from './authContextObject';

/** Redirects unauthenticated visitors to /login and preserves the location they requested. */
export function RequireAuth() {
  const { isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  return <Outlet />;
}
