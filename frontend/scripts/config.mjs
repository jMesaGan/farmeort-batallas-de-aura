import { writeFileSync } from "node:fs";
const url = process.env.BACKEND_URL || "http://localhost:5080";
const parsed = new URL(url);
if (
  !["https:", "http:"].includes(parsed.protocol) ||
  parsed.username ||
  parsed.password ||
  parsed.search ||
  parsed.hash ||
  parsed.pathname !== "/"
)
  throw new Error(
    "BACKEND_URL debe ser un origen HTTP(S), sin credenciales ni ruta.",
  );
if (process.env.VERCEL && parsed.protocol !== "https:")
  throw new Error("Vercel requiere BACKEND_URL con HTTPS.");
writeFileSync(
  new URL("../src/app/config.ts", import.meta.url),
  `export const API_URL = ${JSON.stringify(parsed.origin)};\n`,
);
