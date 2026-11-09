import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [vue()],
  test: {
    environment: "jsdom",
    clearMocks: true,
  },
  server: {
    proxy: {
      "/api": {
        target: "https://localhost:7012",
        changeOrigin: true,
        secure: false,
      },
      "/health": {
        target: "https://localhost:7012",
        changeOrigin: true,
        secure: false,
      },
    },
  },
});
