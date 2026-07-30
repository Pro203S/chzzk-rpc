import { resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const rootDir = fileURLToPath(new URL(".", import.meta.url));
const silentConsole = Object.fromEntries(
    ["debug", "error", "info", "trace", "warn"].map((method) => [
        `console.${method}`,
        "(() => {})",
    ]),
);

export default defineConfig({
    "base": "./",
    "publicDir": "public",
    "plugins": [react()],
    "define": silentConsole,
    "build": {
        "outDir": "dist",
        "emptyOutDir": true,
        "rollupOptions": {
            "input": {
                "popup": resolve(rootDir, "popup.html"),
                "background": resolve(rootDir, "src/background.ts"),
            },
            "output": {
                "entryFileNames": "assets/[name].js",
                "chunkFileNames": "assets/[name].js",
                "assetFileNames": "assets/[name][extname]",
            },
        },
    },
});
