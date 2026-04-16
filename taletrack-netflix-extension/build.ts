/// <reference types="bun" />
import { copyFileSync, mkdirSync, existsSync, rmSync } from "fs";

// Clean dist/
console.log("Cleaning dist/...");
if (existsSync("./dist")) {
  rmSync("./dist", { recursive: true, force: true });
}
mkdirSync("./dist", { recursive: true });

// Build TypeScript with Bun
console.log("Building content.ts...");
await Bun.build({
  entrypoints: [
    './src/popup.ts',
    './src/content.ts',
    './src/injected.ts'
  ],
  outdir: "./dist",
  target: "browser",
  minify: false,
  sourcemap: "external",
});

console.log("Building popup.ts...");
await Bun.build({
  entrypoints: ["./src/popup.ts"],
  outdir: "./dist",
  target: "browser",
  minify: false,
  sourcemap: "external",
});

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