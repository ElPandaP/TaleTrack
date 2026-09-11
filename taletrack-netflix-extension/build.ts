/// <reference types="bun" />
import { copyFileSync, mkdirSync, existsSync, rmSync } from "fs";

// Clean dist/
console.log("Cleaning dist/...");
if (existsSync("./dist")) {
  rmSync("./dist", { recursive: true, force: true });
}
mkdirSync("./dist", { recursive: true });

// Server URLs baked in at build time. Override for local dev, e.g.
//   TT_BACKEND_URL=http://localhost:8080 TT_FRONTEND_URL=http://localhost:8090 bun build.ts
const BACKEND_URL = process.env.TT_BACKEND_URL ?? "https://taletrack.app";
const FRONTEND_URL = process.env.TT_FRONTEND_URL ?? "https://taletrack.app";
console.log(`Backend:  ${BACKEND_URL}`);
console.log(`Frontend: ${FRONTEND_URL}`);

const define = {
  __TT_BACKEND_URL__: JSON.stringify(BACKEND_URL),
  __TT_FRONTEND_URL__: JSON.stringify(FRONTEND_URL),
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
copyFileSync("./manifest.json", "./dist/manifest.json");
copyFileSync("./public/popup.html", "./dist/popup.html");

// Copy icons if they exist
const icons = ["icon16.png", "icon48.png", "icon128.png"];
for (const icon of icons) {
  const iconPath = `./public/${icon}`;
  if (existsSync(iconPath)) {
    copyFileSync(iconPath, `./dist/${icon}`);
    console.log(`Copied ${icon}`);
  } else {
    console.log(`${icon} not found (optional)`);
  }
}

console.log("Build complete!");
console.log("Output: ./dist/");
console.log("\nNext steps:");
console.log("   1. Open chrome://extensions/");
console.log("   2. Enable 'Developer mode'");
console.log("   3. Click 'Load unpacked'");
console.log("   4. Select the 'dist' folder");
