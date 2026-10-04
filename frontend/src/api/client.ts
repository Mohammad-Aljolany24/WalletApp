const API_URL = import.meta.env.VITE_API_URL ?? "http://localhost:5138";

export class ApiError extends Error {
  status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = "ApiError";
  }
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown
): Promise<T> {
  const token = localStorage.getItem("token");

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
  };
  if (token) headers.Authorization = `Bearer ${token}`;

  const response = await fetch(`${API_URL}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (response.status === 401) {
    // Token is bad/expired — clear it. Caller decides whether to redirect.
    localStorage.removeItem("token");
    throw new ApiError(401, "Session expired. Please log in again.");
  }

 if (!response.ok) {
    const text = await response.text();
    let message = text || response.statusText;
    try {
      const parsed = JSON.parse(text);
      // ProblemDetails shape: { type, title, detail, status, traceId }
      // Prefer `detail` (specific) over `title` (generic).
      if (parsed?.detail) message = parsed.detail;
      else if (parsed?.title) message = parsed.title;
      // Legacy fallback for pre-Phase-1.5 endpoints still returning { error }.
      else if (parsed?.error) message = parsed.error;
    } catch {
      // Not JSON — use raw text
    }
    throw new ApiError(response.status, message);
  }

  // 204 No Content
  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

export const api = {
  get: <T>(path: string) => request<T>("GET", path),
  post: <T>(path: string, body?: unknown) => request<T>("POST", path, body),
};