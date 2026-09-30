import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import * as api from '../services/api';

interface AuthUser {
  userId: string;
  name: string;
  email: string;
}

interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (name: string, email: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);
const USER_STORAGE_KEY = 'posfiap.user';

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function restoreSession() {
      const token = api.getToken();
      const refreshToken = api.getRefreshToken();
      const storedUser = localStorage.getItem(USER_STORAGE_KEY);

      if (!storedUser || (!token && !refreshToken)) {
        setIsLoading(false);
        return;
      }

      try {
        const parsedUser = JSON.parse(storedUser) as AuthUser;

        // Se o access token expirou enquanto o usuário estava longe do site,
        // tenta renová-lo silenciosamente usando o refresh token.
        if (refreshToken && (!token || api.isTokenExpiredOrNearExpiry(token))) {
          await api.refreshAccessToken();
        }

        if (!cancelled && api.getToken()) {
          setUser(parsedUser);
        } else if (!cancelled) {
          clearLocalSession();
        }
      } catch {
        if (!cancelled) clearLocalSession();
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    }

    restoreSession();

    return () => {
      cancelled = true;
    };
  }, []);

  function clearLocalSession() {
    api.clearSessionTokens();
    localStorage.removeItem(USER_STORAGE_KEY);
    setUser(null);
  }

  function persistSession(authResponse: { token: string; refreshToken: string; name: string; email: string; userId: string }) {
    api.saveSessionTokens(authResponse);
    const authUser: AuthUser = {
      userId: authResponse.userId,
      name: authResponse.name,
      email: authResponse.email,
    };
    localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(authUser));
    setUser(authUser);
  }

  async function login(email: string, password: string) {
    const response = await api.login(email, password);
    persistSession(response);
  }

  async function register(name: string, email: string, password: string) {
    const response = await api.register(name, email, password);
    persistSession(response);
  }

  function logout() {
    clearLocalSession();
  }

  return (
    <AuthContext.Provider
      value={{ user, isAuthenticated: !!user, isLoading, login, register, logout }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth deve ser usado dentro de um AuthProvider.');
  return ctx;
}
