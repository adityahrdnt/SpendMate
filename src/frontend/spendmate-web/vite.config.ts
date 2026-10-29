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
        target: "http://localhost:5267",
        changeOrigin: true,
      },
      "/health": {
        target: "http://localhost:5267",
        changeOrigin: true,
      },
    },
  },
});
