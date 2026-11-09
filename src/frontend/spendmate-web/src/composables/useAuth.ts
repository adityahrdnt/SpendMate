import { computed, ref } from "vue";
import { ApiError } from "../services/apiClient";
import {
  getCurrentUser,
  login as loginApi,
  logout as logoutApi,
  register as registerApi,
} from "../services/authApi";
import type {
  AuthResponse,
  LoginRequest,
  RegisterRequest,
} from "../types/auth";

type AuthStatus = "initializing" | "authenticated" | "unauthenticated";

export function useAuth() {
  const user = ref<AuthResponse | null>(null);
  const status = ref<AuthStatus>("initializing");
  const errorMessage = ref("");

  const isAuthenticated = computed(
    () => status.value === "authenticated" && user.value !== null,
  );

  async function initialize(): Promise<void> {
    status.value = "initializing";
    errorMessage.value = "";

    try {
      user.value = await getCurrentUser();
      status.value = "authenticated";
    } catch (error) {
      user.value = null;

      if (error instanceof ApiError && error.status === 401) {
        status.value = "unauthenticated";
        return;
      }

      status.value = "unauthenticated";
      errorMessage.value =
        error instanceof Error
          ? error.message
          : "Unable to verify your session. Please try again.";
    }
  }

  async function login(request: LoginRequest): Promise<boolean> {
    errorMessage.value = "";

    try {
      user.value = await loginApi(request);
      status.value = "authenticated";
      return true;
    } catch (error) {
      errorMessage.value =
        error instanceof ApiError && error.status === 401
          ? "Invalid email or password."
          : error instanceof Error
            ? error.message
            : "Unable to sign in. Please try again.";

      return false;
    }
  }

  async function register(request: RegisterRequest): Promise<boolean> {
    errorMessage.value = "";

    try {
      user.value = await registerApi(request);
      status.value = "authenticated";
      return true;
    } catch (error) {
      errorMessage.value =
        error instanceof Error
          ? error.message
          : "Unable to create your account. Please try again.";

      return false;
    }
  }

  async function logout(): Promise<boolean> {
    errorMessage.value = "";

    try {
      await logoutApi();
      user.value = null;
      status.value = "unauthenticated";
      return true;
    } catch (error) {
      errorMessage.value =
        error instanceof Error
          ? error.message
          : "Unable to sign out. Please try again.";

      return false;
    }
  }

  return {
    user,
    status,
    errorMessage,
    isAuthenticated,
    initialize,
    login,
    register,
    logout,
  };
}
