"use client";

export const API_BASE =
  process.env.NEXT_PUBLIC_API_BASE ?? "http://localhost:5080";

const TOKEN_KEY = "dataviz_token";
const USER_KEY = "dataviz_user";

export type User = {
  id: number;
  name: string;
  email: string;
  role: string;
  createdAt: string;
};

export type AuthResponse = {
  token: string;
  expiresAt: string;
  user: User;
};

export type ApiError = { error: string };

export function saveAuth(auth: AuthResponse) {
  if (typeof window === "undefined") return;
  localStorage.setItem(TOKEN_KEY, auth.token);
  localStorage.setItem(USER_KEY, JSON.stringify(auth.user));
}

export function clearAuth() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_KEY);
}

export function getUser(): User | null {
  if (typeof window === "undefined") return null;
  const raw = localStorage.getItem(USER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as User;
  } catch {
    return null;
  }
}

export async function api<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string> | undefined),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });

  if (!res.ok) {
    let message = `Request failed: ${res.status}`;
    try {
      const body = await res.json();
      if (typeof body === "object" && body && "error" in body) {
        message = (body as ApiError).error;
      } else if (typeof body === "object" && body && "title" in body) {
        message = (body as { title: string }).title;
      }
    } catch {
      // ignore body parsing errors
    }
    if (res.status === 401) clearAuth();
    throw new Error(message);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const swrFetcher = <T>(path: string) => api<T>(path);
