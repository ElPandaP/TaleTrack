# Code documentation

Static site with the reference of the four subprojects, generated from the documentation comments in
the code. It is published at [elpandap.github.io/TaleTrack](https://elpandap.github.io/TaleTrack/).

| Subproject | Comments | Generator | Configuration |
|---|---|---|---|
| Backend (.NET) | XML (`/// <summary>`) | DocFX | `docfx.json`, `filterConfig.yml` |
| Web app (Next.js) | TSDoc (`/** ... */`) | TypeDoc | `typedoc.json` + `taletrack-frontend/typedoc.json` |
| Netflix extension | TSDoc (`/** ... */`) | TypeDoc | `typedoc.json` + `taletrack-netflix-extension/typedoc.json` |
| KOReader plugin | LuaCATS (`---@class`, `---@param`...) | lua-language-server | `build.ts` |

DocFX works as the portal: it generates the .NET reference, renders the Markdown pages in this
folder and copies in the TypeDoc output (`ts/`) and the Lua reference (`koplugin/reference.md`).

## Requirements

- .NET 10 SDK (DocFX is installed as a local tool, see `dotnet-tools.json` at the root).
- Bun.
- lua-language-server. It is looked up in `LUA_LANGUAGE_SERVER`, in the `PATH` or in VS Code's Lua
  extension (`sumneko.lua`).
- Dependencies installed in `docs/`, `taletrack-frontend/` and `taletrack-netflix-extension/`
  (`bun install`).

## Usage

From the repository root:

```bash
bun docs/build.ts           # generates docs/_site
bun docs/build.ts --serve   # generates and serves it at http://localhost:8080
```

The `.github/workflows/docs.yml` workflow generates the site on every push to `main` and publishes
it to GitHub Pages.

## What gets documented

- Backend: every type and member of the `TaleTrackApp` project, private ones included, except the
  EF Core migrations.
- Web app: `lib/`, `components/` (without the shadcn/ui components in `components/ui/`) and
  `proxy.ts`. The pages in `app/` are left out.
- Extension: every module in `src/`.
- Plugin: only what is annotated with `---@class` or `---@alias`. Each module must declare its table
  as a class to show up:

```lua
--- Offline queue of progress updates.
---@class Queue
local Queue = {}

--- Pushes an update to the queue.
---@param item table The update.
---@return boolean ok True if stored.
function Queue:push(item) end
```
