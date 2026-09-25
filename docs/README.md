# Documentación del código

Sitio estático con la referencia de los cuatro subproyectos, generado a partir de los comentarios de documentación del código.

| Subproyecto | Comentarios | Generador | Configuración |
|---|---|---|---|
| Backend (.NET) | XML (`/// <summary>`) | DocFX | `docfx.json`, `filterConfig.yml` |
| Frontend (Next.js) | TSDoc (`/** ... */`) | TypeDoc | `typedoc.json` + `taletrack-frontend/typedoc.json` |
| Extensión de Netflix | TSDoc (`/** ... */`) | TypeDoc | `typedoc.json` + `taletrack-netflix-extension/typedoc.json` |
| Plugin KOReader | LuaCATS (`---@class`, `---@param`...) | lua-language-server | `build.ts` |

DocFX actúa como portal: genera la referencia .NET, renderiza las páginas Markdown de esta carpeta y copia dentro la salida de TypeDoc (`ts/`) y la referencia Lua (`koplugin/reference.md`).

## Requisitos

- .NET 10 SDK (DocFX se instala como herramienta local, ver `dotnet-tools.json` en la raíz).
- Bun.
- lua-language-server. Se busca en `LUA_LANGUAGE_SERVER`, en el `PATH` o en la extensión de Lua de VS Code (`sumneko.lua`).
- Dependencias instaladas en `docs/`, `taletrack-frontend/` y `taletrack-netflix-extension/` (`bun install`).

## Uso

Desde la raíz del repositorio:

```bash
bun docs/build.ts           # genera docs/_site
bun docs/build.ts --serve   # genera y sirve en http://localhost:8080
```

El workflow `.github/workflows/docs.yml` genera el sitio en cada push a `main` y lo publica en GitHub Pages.

## Qué se documenta

- Backend: todos los tipos y miembros del proyecto `TaleTrackApp`, privados incluidos, salvo las migraciones de EF Core.
- Frontend: `lib/`, `components/` (sin los componentes de shadcn/ui en `components/ui/`) y `proxy.ts`. Las páginas de `app/` quedan fuera.
- Extensión: todos los módulos de `src/`.
- Plugin: solo lo anotado con `---@class` o `---@alias`. Cada módulo debe declarar su tabla como clase para aparecer:

```lua
--- Offline queue of progress updates.
---@class Queue
local Queue = {}

--- Pushes an update to the queue.
---@param item table The update.
---@return boolean ok True if stored.
function Queue:push(item) end
```
