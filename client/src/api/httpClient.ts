import axios, { AxiosError, type AxiosRequestConfig } from 'axios';

import { useAuthStore } from '../store/authStore';
import i18n from '../i18n/i18n';
import type { ApiProblem, AuthResponse } from '../types';

export const http = axios.create({
  // Relative, so the Vite proxy keeps the browser on one origin in development
  // and the SPA talks to its own host in production.
  baseURL: '/api',

  // Required for the refresh cookie to travel at all.
  withCredentials: true,
});

http.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  // Lets the API answer validation messages and charter enquiries in the
  // language the visitor is actually reading.
  config.headers['Accept-Language'] = i18n.resolvedLanguage ?? 'fr';

  return config;
});

/**
 * A single in-flight refresh, shared by every request that got a 401 while it
 * was running. Without this, five parallel requests expiring together would
 * fire five refreshes — and since the server rotates the token on every use,
 * four of them would present an already-revoked token and trip the reuse
 * detection, logging the user out for being legitimate.
 */
let refreshInFlight: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  try {
    // A bare axios call, not `http`: going through the interceptor would
    // attach the dead token and recurse on its own 401.
    const { data } = await axios.post<AuthResponse>(
      '/api/auth/refresh',
      null,
      { withCredentials: true }
    );

    useAuthStore.getState().setSession(data.accessToken, data.user);
    return data.accessToken;
  } catch {
    useAuthStore.getState().clearSession();
    return null;
  }
}

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiProblem>) => {
    const original = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined;

    const isAuthEndpoint = original?.url?.includes('/auth/');
    const shouldRefresh =
      error.response?.status === 401 && original && !original._retried && !isAuthEndpoint;

    if (!shouldRefresh) {
      return Promise.reject(error);
    }

    original._retried = true;

    refreshInFlight ??= refreshAccessToken().finally(() => {
      refreshInFlight = null;
    });

    const token = await refreshInFlight;
    if (!token) {
      return Promise.reject(error);
    }

    original.headers = { ...original.headers, Authorization: `Bearer ${token}` };
    return http(original);
  }
);

/**
 * Restores a session from the refresh cookie on page load. Called once at
 * startup; a failure is the normal "not signed in" case, not an error.
 */
export async function bootstrapSession(): Promise<void> {
  const store = useAuthStore.getState();
  try {
    await refreshAccessToken();
  } finally {
    store.setBootstrapped();
  }
}

/** Pulls the useful parts out of an axios error for display. */
export function toProblem(error: unknown): ApiProblem {
  if (axios.isAxiosError<ApiProblem>(error) && error.response?.data) {
    return error.response.data;
  }
  return { title: 'Network error' };
}

/** Flattens ValidationProblemDetails into field -> first message. */
export function fieldErrors(problem: ApiProblem): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [field, messages] of Object.entries(problem.errors ?? {})) {
    if (messages?.length) {
      // Camel-case the key so it matches the form field names.
      out[field.charAt(0).toLowerCase() + field.slice(1)] = messages[0];
    }
  }
  return out;
}
