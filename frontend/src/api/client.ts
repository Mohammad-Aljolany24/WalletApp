const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5138";

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ApiError";
  }
}

const NO_RETRY_PATHS = [
  "/api/v1/auth/refresh",
  "/api/v1/auth/login",
  "/api/v1/auth/register",
  "/api/v1/auth/logout",
];

function clearSession() {
  localStorage.removeItem("accessToken");
  localStorage.removeItem("refreshToken");
}

async function tryRefresh(): Promise<boolean> {
  const refreshToken = localStorage.getItem("refreshToken");
  if (!refreshToken) return false;

  try {
    const response = await fetch(`${API_URL}/api/v1/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });

    if (!response.ok) return false;

    const data = await response.json();
    localStorage.setItem("accessToken", data.accessToken);
    localStorage.setItem("refreshToken", data.refreshToken);
    return true;
  } catch {
    return false;
  }
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  idempotencyKey?: string,
  retried = false
): Promise<T> {
  const token = localStorage.getItem("accessToken");

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (idempotencyKey) headers["Idempotency-Key"] = idempotencyKey;

  const response = await fetch(`${API_URL}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (response.status === 401) {
    const canRetry =
      !retried && !NO_RETRY_PATHS.some((p) => path.startsWith(p));

    if (canRetry) {
      const refreshed = await tryRefresh();
      if (refreshed) {
        return request<T>(method, path, body, idempotencyKey, true);
      }
    }

    clearSession();
    throw new ApiError(401, "Session expired. Please log in again.");
  }

  if (!response.ok) {
    const text = await response.text();
    let message = text || response.statusText;
    try {
      const parsed = JSON.parse(text);
      if (parsed?.detail) message = parsed.detail;
      else if (parsed?.title) message = parsed.title;
    } catch {
      // Not JSON — use raw text
    }
    throw new ApiError(response.status, message);
  }

  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string) => request<T>("GET", path),
  post: <T>(path: string, body?: unknown, idempotencyKey?: string) =>
    request<T>("POST", path, body, idempotencyKey),
};