export class ApiError extends Error {
  public readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

let csrfToken: string | null = null;
let csrfTokenRequest: Promise<string> | null = null;

async function readErrorMessage(response: Response): Promise<string> {
  try {
    const body: unknown = await response.json();

    if (typeof body === "object" && body !== null) {
      const problem = body as Record<string, unknown>;

      if (typeof problem.detail === "string") return problem.detail;
      if (typeof problem.error === "string") return problem.error;
      if (typeof problem.title === "string") return problem.title;
    }
  } catch {
    // The response may not contain a JSON body.
  }

  return `Request failed (${response.status} ${response.statusText})`;
}

async function getCsrfToken(): Promise<string> {
  if (csrfToken) return csrfToken;

  if (!csrfTokenRequest) {
    csrfTokenRequest = (async () => {
      const response = await fetch("/api/auth/csrf", {
        method: "GET",
        credentials: "include",
      });

      if (!response.ok) {
        throw new ApiError(await readErrorMessage(response), response.status);
      }

      const body: unknown = await response.json();

      if (
        typeof body !== "object" ||
        body === null ||
        !("requestToken" in body) ||
        typeof body.requestToken !== "string" ||
        body.requestToken.length === 0
      ) {
        throw new Error("The server did not return a valid CSRF token.");
      }

      csrfToken = body.requestToken;
      return csrfToken;
    })().finally(() => {
      csrfTokenRequest = null;
    });
  }

  return csrfTokenRequest;
}

/**
 * Clears the in-memory CSRF token.
 * The next unsafe request will obtain a fresh token from the server.
 */
export function resetCsrfToken(): void {
  csrfToken = null;
  csrfTokenRequest = null;
}

export async function apiRequest<T>(
  url: string,
  options: RequestInit = {},
): Promise<T> {
  const method = (options.method ?? "GET").toUpperCase();
  const isUnsafeMethod = ["POST", "PUT", "PATCH", "DELETE"].includes(method);

  const headers = new Headers(options.headers);

  if (isUnsafeMethod) {
    headers.set("X-CSRF-TOKEN", await getCsrfToken());
  }

  const response = await fetch(url, {
    ...options,
    method,
    headers,
    credentials: "include",
  });

  if (!response.ok) {
    throw new ApiError(await readErrorMessage(response), response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export function getJson<T>(url: string): Promise<T> {
  return apiRequest<T>(url);
}

export function postJson<TRequest, TResponse>(
  url: string,
  request: TRequest,
): Promise<TResponse> {
  return apiRequest<TResponse>(url, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });
}

export function putJson<TRequest, TResponse>(
  url: string,
  request: TRequest,
): Promise<TResponse> {
  return apiRequest<TResponse>(url, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });
}

export function deleteRequest(url: string): Promise<void> {
  return apiRequest<void>(url, {
    method: "DELETE",
  });
}
