import { getJson, postJson, resetCsrfToken } from "./apiClient";

import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
} from "../types/auth";

export async function getCurrentUser(): Promise<AuthResponse> {
  return getJson<AuthResponse>("/api/me");
}

export async function register(
  request: RegisterRequest,
): Promise<AuthResponse> {
  const response = await postJson<RegisterRequest, AuthResponse>(
    "/api/auth/register",
    request,
  );

  resetCsrfToken();

  return response;
}

export async function login(request: LoginRequest): Promise<AuthResponse> {
  const response = await postJson<LoginRequest, AuthResponse>(
    "/api/auth/login",
    request,
  );

  resetCsrfToken();

  return response;
}

export async function logout(): Promise<void> {
  await postJson<Record<string, never>, void>("/api/auth/logout", {});

  resetCsrfToken();
}
