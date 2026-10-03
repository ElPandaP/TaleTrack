# TaleTrack

Documentación técnica del código de TaleTrack, una aplicación para registrar películas, series, libros y cómics.

| Componente | Tecnología | Documentación |
|---|---|---|
| Backend | ASP.NET Core 10 | [Referencia .NET](backend/index.md) |
| Frontend | Next.js 16 | [Referencia TypeScript](ts/index.html) |
| Extensión de Netflix | TypeScript | [Referencia TypeScript](ts/index.html) |
| Plugin KOReader | Lua | [Referencia Lua](koplugin/index.md) |

Cada referencia se genera a partir de los comentarios de documentación del propio código:

- C#: comentarios XML (`/// <summary>`), procesados por DocFX.
- TypeScript: comentarios TSDoc (`/** ... */`), procesados por TypeDoc.
- Lua: anotaciones LuaCATS (`---@param`, `---@return`), procesadas por lua-language-server.

Para regenerarla: `bun docs/build.ts` desde la raíz del repositorio (ver `docs/README.md`).
