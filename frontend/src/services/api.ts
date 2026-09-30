import axios, { AxiosError } from 'axios';
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

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  (error: AxiosError<{ message?: string }>) => {
    // Sem resposta = a requisição nem chegou a um servidor (API fora do ar, URL errada)
    // ou o navegador bloqueou por CORS. A mensagem "Network Error" do axios não ajuda,
    // então mostramos para onde o front tentou ligar e de qual origem.
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
      error.message ||
      'Ocorreu um erro inesperado. Tente novamente.';
    return Promise.reject(new Error(message));
  }
);

export function saveToken(token: string) {
  localStorage.setItem(TOKEN_STORAGE_KEY, token);
}

export function clearToken() {
  localStorage.removeItem(TOKEN_STORAGE_KEY);
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_STORAGE_KEY);
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

  const { data } = await api.post<StudySessionDetail>('/study-sessions/upload', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
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
