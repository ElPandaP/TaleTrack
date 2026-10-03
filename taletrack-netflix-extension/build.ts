/// <reference types="bun" />
/**
 * Build script for the extension (`bun build.ts`, or `bun run build`; add `--debug` for verbose
 * console output). It bundles the scripts into `dist/`, bakes in the backend and web app URLs
 * (`TT_BACKEND_URL`, `TT_FRONTEND_URL`), writes the manifest with the permissions those URLs need,
 * and copies the popup, fonts and locale bundles.
 */
import { copyFileSync, mkdirSync, existsSync, rmSync, readFileSync, writeFileSync, cpSync } from "fs";

// Start from an empty dist/
console.log("Cleaning dist/...");
if (existsSync("./dist")) {
  rmSync("./dist", { recursive: true, force: true });
}
mkdirSync("./dist", { recursive: true });

// Server URLs baked in at build time. Override for local dev, e.g.
//   TT_BACKEND_URL=http://localhost:8080 TT_FRONTEND_URL=http://localhost:8090 bun build.ts
// `bun run build --debug` turns on the extension's verbose console output.
const BACKEND_URL = process.env.TT_BACKEND_URL ?? "https://taletrack.app";
const FRONTEND_URL = process.env.TT_FRONTEND_URL ?? "https://taletrack.app";
const DEBUG = process.argv.includes("--debug");
console.log(`Backend:  ${BACKEND_URL}`);
console.log(`Frontend: ${FRONTEND_URL}`);
console.log(`Debug:    ${DEBUG}`);

const define = {
  __TT_BACKEND_URL__: JSON.stringify(BACKEND_URL),
  __TT_FRONTEND_URL__: JSON.stringify(FRONTEND_URL),
  __TT_DEBUG__: JSON.stringify(DEBUG),
};

console.log("Building extension scripts...");
const result = await Bun.build({
  entrypoints: [
    "./src/popup.ts",
    "./src/content.ts",
    "./src/injected.ts",
    "./src/background.ts",
  ],
  outdir: "./dist",
  target: "browser",
  minify: false,
  sourcemap: "external",
  define,
});

if (!result.success) {
  for (const log of result.logs) console.error(log);
  process.exit(1);
}

// Copy static files
console.log("Copying static files...");

// The auth bridge (background.ts's onMessageExternal) must only ever accept
// messages from the exact frontend origin this build talks to, never a
// wildcard, so pages on any other site can't hand the extension bogus tokens.
const manifest = JSON.parse(readFileSync("./manifest.json", "utf8"));
manifest.externally_connectable = { matches: [`${new URL(FRONTEND_URL).origin}/*`] };
// Least privilege: Netflix, plus only the backend this build actually talks to (so a
// production build never asks for localhost).
manifest.host_permissions = ["https://www.netflix.com/*", `${new URL(BACKEND_URL).origin}/*`];
writeFileSync("./dist/manifest.json", JSON.stringify(manifest, null, 2));

copyFileSync("./public/popup.html", "./dist/popup.html");
// The web app's typefaces (Geist, Cormorant Garamond), bundled so the popup needs no network.
cpSync("./public/fonts", "./dist/fonts", { recursive: true });

// Locale message bundles (chrome.i18n), picked automatically by the browser's
// UI language, falling back to manifest.json's default_locale ("en").
cpSync("./_locales", "./dist/_locales", { recursive: true });

console.log("Build complete!");
console.log("Output: ./dist/");
console.log("\nNext steps:");
console.log("   1. Open chrome://extensions/");
console.log("   2. Enable 'Developer mode'");
console.log("   3. Click 'Load unpacked'");
console.log("   4. Select the 'dist' folder");
