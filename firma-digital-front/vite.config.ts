import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    // En desarrollo el front corre aparte, así que /api se reenvía al API local. En producción
    // los dos se sirven desde el mismo App Service y la ruta relativa resuelve sola.
    proxy: {
      "/api": {
        target: process.env.VITE_API_PROXY ?? "http://localhost:5234",
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: "dist",
    sourcemap: false,
  },
});
