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
  api,
  apiJson,
  clearTokens,
  getRefreshToken,
  refreshAccessToken,
  setTokens,
  type AuthResponse,
  type CurrentUser
} from "@/lib/api";
import { hasAnyRole } from "@/lib/roles";

interface RegisterInput {
  fullName: string;
  email: string;
  password: string;
}

interface AuthContextValue {
  user: CurrentUser | null;
  ready: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (input: RegisterInput) => Promise<void>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
  can: (allowed: readonly string[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [ready, setReady] = useState(false);

  const loadCurrentUser = useCallback(async () => {
    const current = await api<CurrentUser>("/api/v1/auth/me");
    setUser(current);
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
        clearTokens();
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
      await loadCurrentUser();
    },
    [loadCurrentUser]
  );

  const login = useCallback(
    async (email: string, password: string) => {
      const auth = await apiJson<AuthResponse>("/api/v1/auth/login", "POST", {
        email,
        password
      });
      await completeAuth(auth);
    },
    [completeAuth]
  );

  const register = useCallback(
    async (input: RegisterInput) => {
      const auth = await apiJson<AuthResponse>("/api/v1/auth/register", "POST", input);
      await completeAuth(auth);
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
    }
  }, [loadCurrentUser]);

  const can = useCallback(
    (allowed: readonly string[]) =>
      Boolean(user && hasAnyRole(user.roles, allowed)),
    [user]
  );

  const value = useMemo<AuthContextValue>(
    () => ({ user, ready, login, register, logout, refresh, can }),
    [user, ready, login, register, logout, refresh, can]
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
