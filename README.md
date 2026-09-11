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
| `JWT_SECRET` | firma del JWT del backend |
| `GOOGLE_CLIENT_ID` | login con Google |
| `RESEND_API_KEY` | envío de códigos OTP por email |
| `TMDB_API_KEY` | portadas/duración/título traducido para películas y series (opcional — sin ella simplemente no enriquece) |
| `NEXT_PUBLIC_API_URL`, `CORS_ALLOWED_ORIGINS` | cómo se hablan frontend ↔ backend |

## Desplegar en una VM

`docker compose up` levanta todo: postgres, backend, frontend, dozzle (visor de logs) y caddy (proxy
con HTTPS).

`caddy` es un proxy único con [Caddy](https://caddyserver.com/): sirve el frontend en `/` y reenvía
`/api` al backend, con HTTPS automático (pide y renueva el certificado de Let's Encrypt él solo, sin
pasos manuales). Backend y frontend no publican puertos al host — solo son alcanzables a través de
Caddy, en el 80/443. Es la única puerta de entrada, tanto para el navegador como para clientes
externos (la extensión de Netflix, el plugin de KOReader, o cualquier otro cliente de la API).

> Nota: `Caddyfile` tiene el dominio (`taletrack.app` / `www.taletrack.app`) escrito a fuego — si
> despliegas con otro dominio, cámbialo ahí.

> Necesitas los puertos 80 y 443 abiertos hacia fuera (Security Group / firewall de tu proveedor) —
> Caddy los usa para servir la web y para que Let's Encrypt valide el dominio.

```bash
cp .env.example .env
```

Ajusta estas variables al dominio real de la VM (tiene que ser un dominio que ya resuelva a la IP del
servidor — Let's Encrypt no emite certificados para IPs sueltas):

```bash
CORS_ALLOWED_ORIGINS=https://<tu-dominio>
APP_BASE_URL=https://<tu-dominio>
```

(`NEXT_PUBLIC_API_URL` ya viene por defecto a `/api` — no hace falta tocarlo, siempre se sirve a
través de Caddy.)

Levanta todo:

```bash
docker compose up -d --build
```

No hace falta ningún paso manual extra — Caddy pide el certificado solo en cuanto arranca y el
dominio responde en el puerto 80.

Para actualizar solo la app en deploys posteriores (backend/frontend cambian mucho más que caddy):

```bash
docker compose up -d --build backend frontend
```

La extensión de Netflix y el plugin de KOReader también apuntan a una URL fija de servidor por
defecto (ver sus respectivos READMEs para cambiarla en desarrollo local).
