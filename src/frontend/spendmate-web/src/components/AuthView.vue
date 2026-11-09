<script setup lang="ts">
    import { computed, ref } from "vue";
    import type { LoginRequest, RegisterRequest } from "../types/auth";

    const props = defineProps<{
        isSubmitting: boolean;
        errorMessage: string;
    }>();

    const emit = defineEmits<{
        login: [request: LoginRequest];
        register: [request: RegisterRequest];
    }>();

    const mode = ref<"login" | "register">("login");

    const email = ref("");
    const displayName = ref("");
    const password = ref("");
    const familyName = ref("");
    const useDisplayNameAsFamilyName = ref(false);

    const isRegister = computed(() => mode.value === "register");

    function switchMode(nextMode: "login" | "register") {
        mode.value = nextMode;
        password.value = "";
    }

    function syncFamilyName() {
        if (useDisplayNameAsFamilyName.value) {
            familyName.value = displayName.value;
        }
    }

    function submitForm() {
        if (mode.value === "login") {
            emit("login", {
            email: email.value.trim(),
            password: password.value,
            });
            return;
        }

        emit("register", {
            email: email.value.trim(),
            displayName: displayName.value.trim(),
            password: password.value,
            familyName: familyName.value.trim(),
        });
    }
</script>

<template>
  <main class="auth-page">
    <section class="auth-card">
      <a class="auth-brand" href="#" @click.prevent>
        <span class="brand-mark">S</span>
        <span>spend<span class="brand-light">mate</span></span>
      </a>

      <div class="auth-heading">
        <h1>{{ isRegister ? "Create your account" : "Welcome back" }}</h1>
        <p>
          {{
            isRegister
              ? "Start managing your family's expenses together."
              : "Sign in to continue to your family workspace."
          }}
        </p>
      </div>

      <div class="auth-tabs" role="tablist" aria-label="Authentication mode">
        <button
          type="button"
          role="tab"
          :aria-selected="mode === 'login'"
          :class="{ active: mode === 'login' }"
          @click="switchMode('login')"
        >
          Sign in
        </button>
        <button
          type="button"
          role="tab"
          :aria-selected="mode === 'register'"
          :class="{ active: mode === 'register' }"
          @click="switchMode('register')"
        >
          Register
        </button>
      </div>

      <form class="auth-form" @submit.prevent="submitForm">
        <label>
          Email
          <input
            v-model="email"
            type="email"
            autocomplete="email"
            placeholder="you@example.com"
            maxlength="254"
            required
          />
        </label>

        <template v-if="isRegister">
          <label>
            Display name
            <input
              v-model="displayName"
              type="text"
              autocomplete="name"
              placeholder="Your name"
              maxlength="100"
              required
              @input="syncFamilyName"
            />
          </label>

          <label>
            Family name
            <input
              v-model="familyName"
              type="text"
              placeholder="Your family workspace"
              maxlength="100"
              required
            />
          </label>

          <label class="copy-name-option">
            <input
              v-model="useDisplayNameAsFamilyName"
              type="checkbox"
              @change="syncFamilyName"
            />
            Use display name as family name
          </label>
        </template>

        <label>
          Password
          <input
            v-model="password"
            type="password"
            :autocomplete="isRegister ? 'new-password' : 'current-password'"
            :placeholder="isRegister ? 'At least 8 characters' : 'Your password'"
            minlength="8"
            required
          />
        </label>

        <p v-if="props.errorMessage" class="auth-error" role="alert">
            {{ props.errorMessage }}
        </p>

        <button class="auth-submit" type="submit" :disabled="isSubmitting">
          {{
            isSubmitting
              ? "Please wait..."
              : isRegister
                ? "Create account"
                : "Sign in"
          }}
        </button>
      </form>

      <p class="auth-footer">
        {{
          isRegister
            ? "Your family workspace will be created with your account."
            : "Your session is protected with secure cookie authentication."
        }}
      </p>
    </section>
  </main>
</template>

<style scoped>
.auth-page {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 32px 16px;
  background: #f5f7f6;
}

.auth-card {
  width: min(100%, 440px);
  padding: 36px;
  border: 1px solid #e5e9e7;
  border-radius: 20px;
  background: #fff;
  box-shadow: 0 16px 48px rgb(24 48 38 / 8%);
}

.auth-brand {
  display: flex;
  align-items: center;
  gap: 10px;
  color: #243b32;
  font-size: 22px;
  font-weight: 700;
  text-decoration: none;
}

.brand-mark {
  display: grid;
  width: 36px;
  height: 36px;
  place-items: center;
  border-radius: 10px;
  background: #3c8c70;
  color: white;
}

.brand-light {
  color: #3c8c70;
  font-weight: 400;
}

.auth-heading {
  margin-top: 32px;
}

.auth-heading h1 {
  margin: 0;
  color: #243b32;
  font-size: 26px;
}

.auth-heading p {
  margin: 8px 0 0;
  color: #77827d;
  line-height: 1.5;
}

.auth-tabs {
  display: flex;
  gap: 8px;
  margin: 24px 0;
  padding: 4px;
  border-radius: 10px;
  background: #f1f4f2;
}

.auth-tabs button {
  flex: 1;
  padding: 10px;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: #68746e;
  cursor: pointer;
}

.auth-tabs button.active {
  background: white;
  color: #2e765d;
  font-weight: 600;
  box-shadow: 0 1px 4px rgb(0 0 0 / 8%);
}

.auth-form {
  display: grid;
  gap: 16px;
}

.auth-form > label {
  display: grid;
  gap: 7px;
  color: #34443c;
  font-size: 14px;
  font-weight: 600;
}

.auth-form input:not([type="checkbox"]) {
  width: 100%;
  padding: 12px 13px;
  border: 1px solid #dce3df;
  border-radius: 9px;
  background: white;
  color: #243b32;
  font: inherit;
  font-weight: 400;
}

.auth-form input:focus {
  outline: 2px solid #a7d5c1;
  border-color: #3c8c70;
}

.auth-form .copy-name-option {
  display: flex;
  align-items: center;
  gap: 9px;
  font-weight: 400;
}

.auth-submit {
  margin-top: 4px;
  padding: 13px;
  border: 0;
  border-radius: 9px;
  background: #3c8c70;
  color: white;
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

.auth-submit:disabled {
  cursor: wait;
  opacity: 0.7;
}

.auth-error {
  margin: 0;
  color: #b42318;
  font-size: 14px;
}

.auth-footer {
  margin: 24px 0 0;
  color: #77827d;
  font-size: 12px;
  line-height: 1.5;
  text-align: center;
}

@media (max-width: 480px) {
  .auth-card {
    padding: 26px 20px;
  }
}
</style>
