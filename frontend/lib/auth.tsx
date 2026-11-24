"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode
} from "react";
import {
  AUTH_INVALIDATED_EVENT,
  api,
  apiJson,
  clearTokens,
  getRefreshToken,
  invalidateAuth,
  refreshAccessToken,
  setTokens,
  type AuthResponse,
  type CurrentUser
} from "@/lib/api";
import { hasAnyRole, type PublicPurpose } from "@/lib/roles";
import { enrollPublicRole } from "@/lib/portal";
import { resendVerificationEmail } from "@/lib/verification";
import type { ResendView } from "@/lib/verification-state";

interface RegisterInput {
  fullName: string;
  email: string;
  phoneNumber: string;
  password: string;
  accountPurpose?: PublicPurpose;
}

interface AuthContextValue {
  user: CurrentUser | null;
  ready: boolean;
  login: (email: string, password: string) => Promise<CurrentUser>;
  register: (input: RegisterInput) => Promise<CurrentUser>;
  enroll: (purpose: PublicPurpose) => Promise<CurrentUser>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
  resendVerification: (email?: string) => Promise<ResendView>;
  can: (allowed: readonly string[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [ready, setReady] = useState(false);

  const loadCurrentUser = useCallback(async () => {
    const current = await api<CurrentUser>("/api/v1/auth/me");
    setUser(current);
    return current;
  }, []);

  useEffect(() => {
    const onInvalidated = () => setUser(null);
    window.addEventListener(AUTH_INVALIDATED_EVENT, onInvalidated);
    return () => window.removeEventListener(AUTH_INVALIDATED_EVENT, onInvalidated);
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function restore() {
      try {
        if (getRefreshToken()) {
          const refreshed = await refreshAccessToken();
          if (refreshed && !cancelled) {
            await loadCurrentUser();
          }
        }
      } catch {
        invalidateAuth();
      } finally {
        if (!cancelled) {
          setReady(true);
        }
      }
    }

    void restore();
    return () => {
      cancelled = true;
    };
  }, [loadCurrentUser]);

  const completeAuth = useCallback(
    async (auth: AuthResponse) => {
      setTokens(auth);
      return loadCurrentUser();
    },
    [loadCurrentUser]
  );

  const login = useCallback(
    async (email: string, password: string) => {
      const auth = await apiJson<AuthResponse>("/api/v1/auth/login", "POST", {
        email,
        password
      });
      return completeAuth(auth);
    },
    [completeAuth]
  );

  const register = useCallback(
    async (input: RegisterInput) => {
      const auth = await apiJson<AuthResponse>("/api/v1/auth/register", "POST", input);
      return completeAuth(auth);
    },
    [completeAuth]
  );

  const enroll = useCallback(
    async (purpose: PublicPurpose) => {
      // The server validates the purpose and returns fresh tokens containing
      // the new role claim. The frontend never assigns roles itself.
      const auth = await enrollPublicRole(purpose);
      return completeAuth(auth);
    },
    [completeAuth]
  );

  const logout = useCallback(async () => {
    const refreshToken = getRefreshToken();

    try {
      if (refreshToken) {
        await apiJson<void>("/api/v1/auth/logout", "POST", { refreshToken });
      }
    } finally {
      clearTokens();
      setUser(null);
    }
  }, []);

  const refresh = useCallback(async () => {
    const token = await refreshAccessToken();
    if (token) {
      await loadCurrentUser();
    } else {
      invalidateAuth();
    }
  }, [loadCurrentUser]);

  const resendVerification = useCallback(
    (email?: string) => resendVerificationEmail(email ?? user?.email ?? ""),
    [user?.email]
  );

  const can = useCallback(
    (allowed: readonly string[]) =>
      Boolean(user && hasAnyRole(user.roles, allowed)),
    [user]
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      ready,
      login,
      register,
      enroll,
      logout,
      refresh,
      resendVerification,
      can
    }),
    [user, ready, login, register, enroll, logout, refresh, resendVerification, can]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }

  return context;
}
