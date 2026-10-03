// Builds the whole documentation site into docs/_site:
//   1. Lua reference (lua-language-server) -> docs/koplugin/reference.md
//   2. TypeScript reference (TypeDoc)      -> docs/ts
//   3. .NET reference + portal (DocFX)     -> docs/_site, copying the two above in
// Usage: bun docs/build.ts [--serve]
import { $ } from "bun";
import { existsSync, mkdtempSync, readdirSync, rmSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { join, resolve } from "node:path";

const docsDir = import.meta.dir;
const repoRoot = resolve(docsDir, "..");
const serve = process.argv.includes("--serve");

$.cwd(docsDir);

/** Finds lua-language-server: $LUA_LANGUAGE_SERVER, PATH, or the binary bundled with the VS Code Lua extension. */
function findLuaLanguageServer(): string | null {
  const fromEnv = process.env.LUA_LANGUAGE_SERVER;
  if (fromEnv && existsSync(fromEnv)) return fromEnv;

  const onPath = Bun.which("lua-language-server");
  if (onPath) return onPath;

  const extensionsDir = join(homedir(), ".vscode", "extensions");
  if (!existsSync(extensionsDir)) return null;
  const exe = process.platform === "win32" ? "lua-language-server.exe" : "lua-language-server";
  const bundled = readdirSync(extensionsDir)
    .filter((dir) => dir.startsWith("sumneko.lua-"))
    .sort()
    .reverse()
    .map((dir) => join(extensionsDir, dir, "server", "bin", exe))
    .find((path) => existsSync(path));
  return bundled ?? null;
}

interface LuaDefine {
  file: string;
  type: string;
  desc?: string;
  view?: string;
  start: [number, number];
}

interface LuaField {
  name: string;
  file: string;
  desc?: string;
  view: string;
  visible?: string;
  start: [number, number];
  extends?: { view?: string };
}

interface LuaEntry {
  name: string;
  defines?: LuaDefine[];
  fields?: LuaField[];
}

// LuaLS marks everything outside the documented folder (stdlib, bundled meta files) as [FOREIGN].
const isOwn = (file: string) => !file.startsWith("[FOREIGN]");

/** Renders the plugin's own classes and aliases from LuaLS doc.json, leaving out the Lua stdlib. */
function renderLuaReference(entries: LuaEntry[]): string {
  const own = entries
    .map((entry) => ({ entry, define: entry.defines?.find((d) => isOwn(d.file)) }))
    .filter((x): x is { entry: LuaEntry; define: LuaDefine } => x.define !== undefined)
    .sort((a, b) => a.define.file.localeCompare(b.define.file) || a.entry.name.localeCompare(b.entry.name));

  const lines = ["# Referencia de módulos", ""];
  if (own.length === 0) {
    lines.push("Aún no hay módulos anotados con `---@class` o `---@alias`.");
    return lines.join("\n") + "\n";
  }

  for (const { entry, define } of own) {
    lines.push(`## ${entry.name}`, "", `Definido en \`${define.file}\`.`, "");
    if (define.type === "doc.alias") {
      lines.push(define.desc ?? ["```lua", define.view, "```"].join("\n"), "");
      continue;
    }
    if (define.desc) lines.push(define.desc.trim(), "");

    const fields = (entry.fields ?? [])
      .filter((f) => isOwn(f.file) && f.visible !== "private")
      .sort((a, b) => a.start[0] - b.start[0]);
    for (const field of fields) {
      lines.push(`### ${field.name}`, "", "```lua", field.extends?.view ?? field.view, "```", "");
      if (field.desc) lines.push(field.desc.trim(), "");
    }
  }
  return lines.join("\n");
}

async function buildLua() {
  const luals = findLuaLanguageServer();
  if (!luals) {
    throw new Error(
      "lua-language-server not found. Install it (https://luals.github.io) or set LUA_LANGUAGE_SERVER to its binary.",
    );
  }
  const outDir = mkdtempSync(join(tmpdir(), "taletrack-luadoc-"));
  try {
    const plugin = join(repoRoot, "taletrack.koplugin");
    await $`${luals} --doc=${plugin} --doc_out_path=${outDir}`.quiet();
    const entries: LuaEntry[] = await Bun.file(join(outDir, "doc.json")).json();
    await Bun.write(join(docsDir, "koplugin", "reference.md"), renderLuaReference(entries));
  } finally {
    rmSync(outDir, { recursive: true, force: true });
  }
}

console.log("› Lua (lua-language-server)");
await buildLua();

console.log("› TypeScript (TypeDoc)");
await $`${process.execPath} x typedoc`;

console.log("› .NET (DocFX)");
await $`dotnet tool restore`.cwd(repoRoot).quiet();
await $`dotnet docfx docfx.json`;

console.log(`✓ Site built at ${join(docsDir, "_site")}`);

if (serve) {
  await $`dotnet docfx serve _site`;
}
