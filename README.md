# TaleTrack

Sistema para trackear automáticamente lo que consumes — películas, series y libros — sin tener que
anotarlo a mano: una extensión de Chrome detecta lo que ves en Netflix, un plugin de KOReader detecta
los libros que terminas, y ambos lo mandan a un backend común con biblioteca, reseñas y actividad de
amigos.

## Componentes

| Carpeta | Qué es | README |
|---|---|---|
| `TaleTrackApp/` | Backend — ASP.NET Core 10, Minimal APIs, PostgreSQL | [`TaleTrackApp/README.md`](TaleTrackApp/README.md) |
| `TaleTrackApp.Tests/` | Tests de integración del backend (xUnit) | ver sección de tests en el README del backend |
| `taletrack-frontend/` | Frontend — Next.js 16, App Router | [`taletrack-frontend/README.md`](taletrack-frontend/README.md) |
| `taletrack-netflix-extension/` | Extensión de Chrome que trackea Netflix automáticamente | [`taletrack-netflix-extension/README.md`](taletrack-netflix-extension/README.md) |
| `taletrack.koplugin/` | Plugin de KOReader que trackea libros leídos (submódulo git aparte) | [`taletrack.koplugin/README.md`](taletrack.koplugin/README.md) |

Todo gira alrededor del backend: los otros tres son distintas puertas de entrada de datos (web,
Netflix, lector de libros) sobre la misma API.

## Arrancar todo (Docker)

La forma más rápida, con PowerShell:

```powershell
./scripts/start-all.ps1          # levanta postgres + backend + frontend + dozzle, espera y siembra un usuario demo
./scripts/start-all.ps1 -NoSeed  # igual pero sin datos de ejemplo
./scripts/start-all.ps1 -Down    # apaga todo
```

O a mano:

```bash
cp .env.example .env   # y rellena las variables (ver tabla abajo)
docker compose -f docker-compose.dev.yml up -d
```

- Frontend: http://localhost:8090
- Backend / Swagger: http://localhost:8080/swagger
- Logs (dozzle): http://localhost:9999

Usuario de prueba (si sembraste datos): `demo@taletrack.dev` / `demo1234`.

### Solo base de datos + migraciones (sin .NET local)

```bash
docker compose up -d postgres
docker compose --profile db run --rm migrator
```

## Variables de entorno (`.env` en la raíz)

Ver `.env.example` para la lista completa. Las importantes para que todo se hable entre sí:

| Variable | Para qué |
|---|---|
| `POSTGRES_*` | conexión a la base de datos |
| `JWT_SECRET`, `INTERNAL_API_KEY` | autenticación del backend (JWT + API key interna) |
| `GOOGLE_CLIENT_ID` | login con Google |
| `RESEND_API_KEY` | envío de códigos OTP por email |
| `TMDB_API_KEY` | portadas/duración/título traducido para películas y series (opcional — sin ella simplemente no enriquece) |
| `NEXT_PUBLIC_API_URL`, `CORS_ALLOWED_ORIGINS` | cómo se hablan frontend ↔ backend |

`docker-compose.dev.yml` copia `INTERNAL_API_KEY` en `NEXT_PUBLIC_INTERNAL_API_KEY` automáticamente
para que el navegador la tenga disponible — no hace falta definirla aparte en `.env`.

## Desplegar en una VM

Para que el navegador (fuera de la red de Docker) pueda llamar al backend, hace falta una URL pública
en vez de `backend:8080`:

```bash
NEXT_PUBLIC_API_URL=http://<ip-o-dominio>:8080/api
CORS_ALLOWED_ORIGINS=http://<ip-o-dominio>
```

Y reconstruir el frontend después de cambiar `.env`:

```bash
docker compose up -d --build frontend backend
```

La extensión de Netflix y el plugin de KOReader también apuntan a una URL fija de servidor por
defecto (ver sus respectivos READMEs para cambiarla en desarrollo local).
