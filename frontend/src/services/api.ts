import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios';
import type {
  AuthResponse,
  StudySessionDetail,
  StudySessionSummary,
} from '../types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:8080/api';

export const api = axios.create({
  baseURL: API_BASE_URL,
});

const TOKEN_STORAGE_KEY = 'posfiap.token';
const REFRESH_TOKEN_STORAGE_KEY = 'posfiap.refreshToken';

let refreshPromise: Promise<AuthResponse | null> | null = null;

function decodeJwtPayload(token: string): { exp?: number } | null {
  try {
    const payload = token.split('.')[1];
    if (!payload) return null;
    const normalized = payload.replace(/-/g, '+').replace(/_/g, '/');
    const decoded = atob(normalized.padEnd(normalized.length + ((4 - normalized.length % 4) % 4), '='));
    return JSON.parse(decoded);
  } catch {
    return null;
  }
}

export function isTokenExpiredOrNearExpiry(token: string, safetyWindowSeconds = 60): boolean {
  const payload = decodeJwtPayload(token);
  if (!payload?.exp) return true;
  return payload.exp <= Math.floor(Date.now() / 1000) + safetyWindowSeconds;
}

function applyAccessToken(config: InternalAxiosRequestConfig, token: string) {
  config.headers.Authorization = `Bearer ${token}`;
}

export async function refreshAccessToken(): Promise<AuthResponse | null> {
  const refreshToken = localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
  if (!refreshToken) return null;

  if (!refreshPromise) {
    // Usa uma requisição Axios independente para que o interceptor de 401 não
    // tente renovar o próprio endpoint /auth/refresh em loop.
    refreshPromise = axios
      .post<AuthResponse>(`${API_BASE_URL}/auth/refresh`, { refreshToken })
      .then(({ data }) => {
        saveSessionTokens(data);
        return data;
      })
      .catch(() => {
        clearSessionTokens();
        return null;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }

  return refreshPromise;
}

api.interceptors.request.use(async (config) => {
  let token = localStorage.getItem(TOKEN_STORAGE_KEY);

  // Renova antes de enviar uma requisição se o access token estiver expirado
  // ou prestes a expirar. Isso evita que o usuário perceba a renovação.
  if (token && isTokenExpiredOrNearExpiry(token)) {
    const refreshed = await refreshAccessToken();
    token = refreshed?.token ?? localStorage.getItem(TOKEN_STORAGE_KEY);
  }

  if (token) applyAccessToken(config, token);
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<{ message?: string }>) => {
    const originalRequest = error.config as (InternalAxiosRequestConfig & { _retry?: boolean }) | undefined;

    // Se o access token expirou entre o interceptor de request e a resposta,
    // tenta renovar uma única vez e repete a chamada original.
    if (
      error.response?.status === 401 &&
      originalRequest &&
      !originalRequest._retry &&
      !originalRequest.url?.includes('/auth/login') &&
      !originalRequest.url?.includes('/auth/register') &&
      !originalRequest.url?.includes('/auth/refresh') &&
      localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY)
    ) {
      originalRequest._retry = true;
      const refreshed = await refreshAccessToken();

      if (refreshed?.token) {
        applyAccessToken(originalRequest, refreshed.token);
        return api.request(originalRequest);
      }
    }

    // Sem resposta = a requisição nem chegou a um servidor ou houve bloqueio de rede/CORS.
    if (!error.response) {
      return Promise.reject(
        new Error(
          `Não foi possível falar com a API em ${API_BASE_URL}. ` +
            `Confirme que o backend está no ar e que o CORS aceita ${window.location.origin}.`
        )
      );
    }

    const message =
      error.response.data?.message ||
      (error.response.status === 401
        ? 'Sua sessão expirou. Faça login novamente.'
        : error.message) ||
      'Ocorreu um erro inesperado. Tente novamente.';

    return Promise.reject(new Error(message));
  }
);

export function saveSessionTokens(authResponse: Pick<AuthResponse, 'token' | 'refreshToken'>) {
  localStorage.setItem(TOKEN_STORAGE_KEY, authResponse.token);
  localStorage.setItem(REFRESH_TOKEN_STORAGE_KEY, authResponse.refreshToken);
}

export function saveToken(token: string) {
  localStorage.setItem(TOKEN_STORAGE_KEY, token);
}

export function clearSessionTokens() {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
  localStorage.removeItem(REFRESH_TOKEN_STORAGE_KEY);
}

export function clearToken() {
  clearSessionTokens();
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_STORAGE_KEY);
}

export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_STORAGE_KEY);
}

// ---------- Auth ----------

export async function login(email: string, password: string): Promise<AuthResponse> {
  const { data } = await api.post<AuthResponse>('/auth/login', { email, password });
  return data;
}

export async function register(
  name: string,
  email: string,
  password: string
): Promise<AuthResponse> {
  const { data } = await api.post<AuthResponse>('/auth/register', {
    name,
    email,
    password,
  });
  return data;
}

// ---------- Study Sessions ----------

export async function uploadZip(
  file: File,
  onProgress?: (percent: number) => void
): Promise<StudySessionDetail> {
  const formData = new FormData();
  formData.append('file', file);

  // Não definimos Content-Type manualmente: o navegador/Axios adiciona o
  // boundary correto do multipart/form-data.
  const { data } = await api.post<StudySessionDetail>('/study-sessions/upload', formData, {
    onUploadProgress: (evt) => {
      if (onProgress && evt.total) {
        onProgress(Math.round((evt.loaded / evt.total) * 100));
      }
    },
  });
  return data;
}

export async function listStudySessions(): Promise<StudySessionSummary[]> {
  const { data } = await api.get<StudySessionSummary[]>('/study-sessions');
  return data;
}

export async function getStudySession(id: string): Promise<StudySessionDetail> {
  const { data } = await api.get<StudySessionDetail>(`/study-sessions/${id}`);
  return data;
}
