"use client";

import type { User } from "./types";
export type { User };

export const API_BASE =
  process.env.NEXT_PUBLIC_API_BASE ?? "http://localhost:5080";

const TOKEN_KEY = "dataviz_token";
const USER_KEY = "dataviz_user";
const AUTH_EVENT = "dataviz:auth";

export type AuthResponse = {
  token: string;
  expiresAt: string;
  user: User;
};

/** Структура ошибки RFC 7807 ProblemDetails, возвращаемой ASP.NET Core. */
export type ProblemDetails = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
};

export type ApiError = { error: string };

export class ApiHttpError extends Error {
  readonly status: number;
  readonly problem?: ProblemDetails;
  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = "ApiHttpError";
    this.status = status;
    this.problem = problem;
  }
}

function emitAuthEvent() {
  if (typeof window === "undefined") return;
  window.dispatchEvent(new CustomEvent(AUTH_EVENT));
}

export function saveAuth(auth: AuthResponse) {
  if (typeof window === "undefined") return;
  localStorage.setItem(TOKEN_KEY, auth.token);
  localStorage.setItem(USER_KEY, JSON.stringify(auth.user));
  emitAuthEvent();
}

export function clearAuth() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  emitAuthEvent();
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

/** Подписка на изменения авторизации (внутри одной вкладки и между ними). */
export function subscribeAuth(listener: () => void): () => void {
  if (typeof window === "undefined") return () => undefined;
  const onLocal = () => listener();
  const onStorage = (e: StorageEvent) => {
    if (e.key === TOKEN_KEY || e.key === USER_KEY) listener();
  };
  window.addEventListener(AUTH_EVENT, onLocal);
  window.addEventListener("storage", onStorage);
  return () => {
    window.removeEventListener(AUTH_EVENT, onLocal);
    window.removeEventListener("storage", onStorage);
  };
}

/** Универсальный fetch-клиент: подставляет JWT, обрабатывает ProblemDetails и 401. */
export async function api<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const token = getToken();
  const headers = new Headers(options.headers);
  if (!headers.has("Content-Type") && options.body) {
    headers.set("Content-Type", "application/json");
  }
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });

  if (!res.ok) {
    let message = `Request failed: ${res.status}`;
    let problem: ProblemDetails | undefined;
    try {
      const body = await res.json();
      if (body && typeof body === "object") {
        if ("title" in body && typeof body.title === "string") {
          problem = body as ProblemDetails;
          message = problem.title ?? message;
          if (problem.detail) message = `${message}: ${problem.detail}`;
        } else if ("error" in body && typeof body.error === "string") {
          message = (body as ApiError).error;
        }
      }
    } catch {
      // ignore body parsing errors
    }
    if (res.status === 401) clearAuth();
    throw new ApiHttpError(res.status, message, problem);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export const swrFetcher = <T>(path: string) => api<T>(path);
