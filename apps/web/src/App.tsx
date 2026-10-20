import { Link, NavLink, Outlet } from 'react-router-dom';
import { useAuth } from './features/auth/authContextObject';

/** Application shell: the header, the navigation, signed-in agent controls, and page outlet. */
export function App() {
  const { agent, logout } = useAuth();

  return (
    <div className="app">
      <header className="app__header">
        <Link className="app__brand" to="/tickets">
          Northwind Support
        </Link>

        <nav className="app__nav" aria-label="Main">
          <NavLink to="/tickets">Tickets</NavLink>
          <NavLink to="/customers">Customers</NavLink>
        </nav>

        {agent && (
          <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '12px' }}>
            <span aria-label="Signed in agent" style={{ fontSize: '0.9rem', fontWeight: 500 }}>
              {agent.fullName}
            </span>
            <button type="button" className="button button--ghost" onClick={logout}>
              Sign out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
