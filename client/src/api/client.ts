const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5199";
const TOKEN_KEY = "boardroom_token";
const USER_KEY = "boardroom_user";

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string | null): void {
  if (token) {
    localStorage.setItem(TOKEN_KEY, token);
  } else {
    localStorage.removeItem(TOKEN_KEY);
  }
}

interface RequestOptions {
  method?: "GET" | "POST" | "PATCH" | "DELETE";
  body?: unknown;
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    method: options.method ?? "GET",
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  let data: { message?: unknown } | undefined;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = undefined;
    }
  }

  if (response.status === 401 && token) {
    // The stored token was rejected (expired or invalid): drop the session
    // and send the user back to the login page.
    setToken(null);
    localStorage.removeItem(USER_KEY);
    if (window.location.pathname !== "/login") {
      window.location.assign("/login");
    }
  }

  if (!response.ok) {
    const message =
      (data && typeof data.message === "string" && data.message) ||
      `Request failed with status ${response.status}`;
    throw new ApiError(response.status, message);
  }

  if (data === undefined && text) {
    throw new ApiError(response.status, "The server returned an unexpected response.");
  }

  return data as T;
}
