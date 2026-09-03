import { create } from 'zustand';

import type { User } from '../types';

/**
 * Session state.
 *
 * The access token lives here and nowhere else — not localStorage, not a
 * cookie readable by script. Any XSS on the page would be able to read
 * localStorage, and a stolen token is valid until it expires with no way to
 * revoke it. Holding it in a module closure means it dies with the tab, and the
 * HttpOnly refresh cookie is what restores the session on reload.
 *
 * The cost is a flash of "signed out" on first paint, which `bootstrapped`
 * exists to suppress.
 */
interface AuthState {
  accessToken: string | null;
  user: User | null;
  /** False until the initial refresh attempt has settled. */
  bootstrapped: boolean;

  setSession: (accessToken: string, user: User) => void;
  clearSession: () => void;
  setBootstrapped: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  user: null,
  bootstrapped: false,

  setSession: (accessToken, user) => set({ accessToken, user }),
  clearSession: () => set({ accessToken: null, user: null }),
  setBootstrapped: () => set({ bootstrapped: true }),
}));

/**
 * Read the token outside React — the axios interceptor runs on every request
 * and must not depend on a component being mounted.
 */
export const getAccessToken = () => useAuthStore.getState().accessToken;
export const isSignedIn = () => useAuthStore.getState().accessToken !== null;
