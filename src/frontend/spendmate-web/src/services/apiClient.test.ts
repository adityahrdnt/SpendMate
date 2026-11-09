import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { deleteRequest, getJson, postJson, resetCsrfToken } from "./apiClient";

describe("apiClient", () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    resetCsrfToken();
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("includes credentials when making a GET request", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ message: "success" }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );

    const result = await getJson<{ message: string }>("/api/me");

    expect(result).toEqual({ message: "success" });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/me",
      expect.objectContaining({
        method: "GET",
        credentials: "include",
      }),
    );

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("fetches a CSRF token before making a POST request", async () => {
    fetchMock
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ requestToken: "csrf-token-123" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ id: "expense-1" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      );

    const result = await postJson<{ description: string }, { id: string }>(
      "/api/expenses",
      { description: "Lunch" },
    );

    expect(result).toEqual({ id: "expense-1" });

    expect(fetchMock).toHaveBeenNthCalledWith(
      1,
      "/api/auth/csrf",
      expect.objectContaining({
        method: "GET",
        credentials: "include",
      }),
    );

    const postCall = fetchMock.mock.calls[1];
    const postOptions = postCall[1] as RequestInit;
    const headers = new Headers(postOptions.headers);

    expect(postCall[0]).toBe("/api/expenses");
    expect(postOptions.method).toBe("POST");
    expect(postOptions.credentials).toBe("include");
    expect(headers.get("X-CSRF-TOKEN")).toBe("csrf-token-123");
    expect(headers.get("Content-Type")).toBe("application/json");
    expect(postOptions.body).toBe(JSON.stringify({ description: "Lunch" }));
  });

  it("reuses the CSRF token for subsequent unsafe requests", async () => {
    fetchMock
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ requestToken: "csrf-token-456" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await deleteRequest("/api/expenses/1");
    await deleteRequest("/api/expenses/2");

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/auth/csrf");

    for (const call of fetchMock.mock.calls.slice(1)) {
      const options = call[1] as RequestInit;
      const headers = new Headers(options.headers);

      expect(headers.get("X-CSRF-TOKEN")).toBe("csrf-token-456");
      expect(options.credentials).toBe("include");
    }
  });

  it("throws ApiError with the response status and error message", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ error: "Invalid credentials" }), {
        status: 401,
        statusText: "Unauthorized",
        headers: { "Content-Type": "application/json" },
      }),
    );

    await expect(getJson("/api/me")).rejects.toMatchObject({
      name: "ApiError",
      message: "Invalid credentials",
      status: 401,
    });
  });

  it("throws ApiError when the CSRF token request fails", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response(JSON.stringify({ error: "CSRF unavailable" }), {
        status: 500,
        statusText: "Internal Server Error",
        headers: { "Content-Type": "application/json" },
      }),
    );

    await expect(
      postJson("/api/expenses", { description: "Lunch" }),
    ).rejects.toMatchObject({
      name: "ApiError",
      message: "CSRF unavailable",
      status: 500,
    });

    // The actual POST must not execute when token acquisition fails.
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("handles HTTP 204 without trying to parse a response body", async () => {
    fetchMock
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ requestToken: "csrf-token-789" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(deleteRequest("/api/expenses/1")).resolves.toBeUndefined();
  });

  it("resets the cached CSRF token when requested", async () => {
    fetchMock
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ requestToken: "csrf-token-old" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(
        new Response(JSON.stringify({ requestToken: "csrf-token-new" }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await deleteRequest("/api/expenses/1");

    resetCsrfToken();

    await deleteRequest("/api/expenses/2");

    expect(fetchMock).toHaveBeenCalledTimes(4);

    const firstDeleteHeaders = new Headers(
      (fetchMock.mock.calls[1][1] as RequestInit).headers,
    );

    const secondDeleteHeaders = new Headers(
      (fetchMock.mock.calls[3][1] as RequestInit).headers,
    );

    expect(firstDeleteHeaders.get("X-CSRF-TOKEN")).toBe("csrf-token-old");
    expect(secondDeleteHeaders.get("X-CSRF-TOKEN")).toBe("csrf-token-new");
  });
});
