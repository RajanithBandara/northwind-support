import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { Field } from '../../components/Field';
import { useAuth } from './authContextObject';

interface LocationState {
  from?: {
    pathname: string;
    search?: string;
  };
}

export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ email?: string; password?: string }>({});
  const [submitError, setSubmitError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const state = location.state as LocationState | null;
  const redirectTarget = state?.from
    ? `${state.from.pathname}${state.from.search ?? ''}`
    : '/tickets';

  if (isAuthenticated) {
    return <Navigate to={redirectTarget} replace />;
  }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitError(undefined);

    const validationErrors: { email?: string; password?: string } = {};
    const trimmedEmail = email.trim();

    if (!trimmedEmail) {
      validationErrors.email = 'Email is required.';
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      validationErrors.email = 'Enter a valid email address.';
    }

    if (!password) {
      validationErrors.password = 'Password is required.';
    }

    setErrors(validationErrors);
    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);
    try {
      await login({ email: trimmedEmail, password });
      void navigate(redirectTarget, { replace: true });
    } catch (caught) {
      setSubmitError(toErrorMessage(caught, 'Invalid email or password.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="app">
      <main className="app__main" style={{ maxWidth: '440px', marginTop: '64px' }}>
        <section className="card">
          <header style={{ marginBottom: '20px' }}>
            <h1>Sign in to Northwind Support</h1>
            <p className="page-header__subtitle">
              Use your support agent credentials to access the workspace.
            </p>
          </header>

          <form className="form" onSubmit={handleSubmit} noValidate>
            <Field id="login-email" label="Email" error={errors.email}>
              {(fieldProps) => (
                <input
                  {...fieldProps}
                  type="email"
                  autoComplete="email"
                  value={email}
                  disabled={isSubmitting}
                  placeholder="alex.turner@northwind-support.example"
                  onChange={(e) => {
                    setEmail(e.target.value);
                    if (errors.email) setErrors((prev) => ({ ...prev, email: undefined }));
                  }}
                />
              )}
            </Field>

            <Field id="login-password" label="Password" error={errors.password}>
              {(fieldProps) => (
                <input
                  {...fieldProps}
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  disabled={isSubmitting}
                  onChange={(e) => {
                    setPassword(e.target.value);
                    if (errors.password) setErrors((prev) => ({ ...prev, password: undefined }));
                  }}
                />
              )}
            </Field>

            {submitError && (
              <p className="field__error" role="alert">
                {submitError}
              </p>
            )}

            <div className="button-row">
              <button
                type="submit"
                className="button button--primary"
                disabled={isSubmitting}
                style={{ width: '100%' }}
              >
                {isSubmitting ? 'Signing in...' : 'Sign in'}
              </button>
            </div>
          </form>
        </section>
      </main>
    </div>
  );
}
