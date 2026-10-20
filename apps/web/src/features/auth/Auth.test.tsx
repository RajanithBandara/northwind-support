import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import {
  ApiError,
  clearStoredSession,
  getStoredSession,
  request,
  setStoredSession,
} from '../../api/client';
import { routes } from '../../routes';
import { AuthProvider } from './AuthContext';

describe('Authentication & Protected Routes', () => {
  beforeEach(() => {
    clearStoredSession();
    vi.restoreAllMocks();
  });

  it('redirects unauthenticated visitors to /login and returns them to their original page after login', async () => {
    const user = userEvent.setup();

    vi.spyOn(authApi, 'login').mockResolvedValue({
      token: 'valid-jwt-token',
      expiresAtUtc: '2026-10-20T20:00:00Z',
      agent: {
        id: 1,
        fullName: 'Alex Turner',
        email: 'alex.turner@northwind-support.example',
      },
    });

    const memoryRouter = createMemoryRouter(
      [
        routes[0],
        {
          path: '/',
          element: routes[1].element,
          children: [
            {
              path: 'customers',
              element: <div>Protected Customers Page</div>,
            },
          ],
        },
      ],
      { initialEntries: ['/customers'] },
    );

    render(
      <AuthProvider>
        <RouterProvider router={memoryRouter} />
      </AuthProvider>,
    );

    // Unauthenticated visitor is redirected to /login
    expect(screen.getByRole('heading', { name: /sign in to northwind support/i })).toBeInTheDocument();

    await user.type(screen.getByLabelText(/email/i), 'alex.turner@northwind-support.example');
    await user.type(screen.getByLabelText(/password/i), 'Northwind_Dev_Pass1!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByText('Protected Customers Page')).toBeInTheDocument();
    });

    expect(getStoredSession()?.token).toBe('valid-jwt-token');
  });

  it('shows clear error on /login when credentials are rejected with 401', async () => {
    const user = userEvent.setup();

    vi.spyOn(authApi, 'login').mockRejectedValue(
      new ApiError(401, 'Invalid email or password.', {
        status: 401,
        title: 'Unauthorized',
        detail: 'Invalid email or password.',
      }),
    );

    const memoryRouter = createMemoryRouter(routes, { initialEntries: ['/login'] });

    render(
      <AuthProvider>
        <RouterProvider router={memoryRouter} />
      </AuthProvider>,
    );

    await user.type(screen.getByLabelText(/email/i), 'alex.turner@northwind-support.example');
    await user.type(screen.getByLabelText(/password/i), 'WrongPassword!');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Invalid email or password.');
    });

    expect(getStoredSession()).toBeNull();
  });

  it('attaches Authorization Bearer token to API calls and clears session on 401 response', async () => {
    setStoredSession({
      token: 'my-secret-jwt',
      expiresAtUtc: '2026-10-20T20:00:00Z',
      agent: { id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' },
    });

    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({ id: 1, fullName: 'Alex Turner', email: 'alex.turner@northwind-support.example' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await request('/auth/me');

    expect(fetchSpy).toHaveBeenCalledTimes(1);
    const [, init] = fetchSpy.mock.calls[0];
    const headers = new Headers(init?.headers);
    expect(headers.get('Authorization')).toBe('Bearer my-secret-jwt');

    // Simulate expired token returning 401
    fetchSpy.mockResolvedValueOnce(
      new Response(JSON.stringify({ status: 401, title: 'Unauthorized', detail: 'Token expired.' }), {
        status: 401,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await expect(request('/tickets')).rejects.toThrow('Token expired.');
    expect(getStoredSession()).toBeNull();
  });
});
