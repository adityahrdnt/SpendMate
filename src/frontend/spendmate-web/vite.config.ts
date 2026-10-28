import vue from "@vitejs/plugin-vue";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      "/api": {
        target: "http://localhost:5080",
        changeOrigin: true,
      },
      "/health": {
        target: "http://localhost:5080",
        changeOrigin: true,
      },
    },
  },
});
