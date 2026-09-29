import { createContext, useCallback, useContext, useMemo, useState } from "react";
import type { ReactNode } from "react";
import * as authApi from "../api/auth";
import { getToken, setToken } from "../api/client";

interface CurrentUser {
  userId: number;
  name: string;
  email: string;
}

interface AuthContextValue {
  user: CurrentUser | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  logout: () => void;
}

const USER_KEY = "boardroom_user";

function readStoredUser(): CurrentUser | null {
  const raw = localStorage.getItem(USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as CurrentUser;
  } catch {
    return null;
  }
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(() =>
    getToken() ? readStoredUser() : null
  );

  const applyAuthResponse = useCallback(
    (auth: { token: string; userId: number; name: string; email: string }) => {
      setToken(auth.token);
      const nextUser: CurrentUser = { userId: auth.userId, name: auth.name, email: auth.email };
      localStorage.setItem(USER_KEY, JSON.stringify(nextUser));
      setUser(nextUser);
    },
    []
  );

  const login = useCallback(
    async (email: string, password: string) => {
      const auth = await authApi.login(email, password);
      applyAuthResponse(auth);
    },
    [applyAuthResponse]
  );

  const register = useCallback(
    async (name: string, email: string, password: string) => {
      const auth = await authApi.register(name, email, password);
      applyAuthResponse(auth);
    },
    [applyAuthResponse]
  );

  const logout = useCallback(() => {
    setToken(null);
    localStorage.removeItem(USER_KEY);
    setUser(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ user, isAuthenticated: user !== null, login, register, logout }),
    [user, login, register, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
