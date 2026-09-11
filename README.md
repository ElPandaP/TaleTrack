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

`docker-compose.yml` es solo la app (postgres, backend, frontend, dozzle) — igual que en local, pero
con `NEXT_PUBLIC_API_URL` apuntando a la IP/dominio real en vez de `localhost`. Con eso sola ya puedes
levantarla y ponerle delante lo que quieras (nginx, Caddy, Traefik, nada...).

`docker-compose.proxy.yml` es opcional y añade un proxy único con nginx: sirve el frontend en `/` y
reenvía `/api` al backend, con HTTPS vía Let's Encrypt (certbot en su propio contenedor). Al estar
separado del compose de la app, puedes actualizar/reiniciar backend+frontend sin tocar nginx ni
disparar renovaciones de certificado de más.

> Nota: `nginx/nginx.conf` tiene el dominio (`taletrack.app` / `www.taletrack.app`) escrito a fuego —
> si despliegas con otro dominio, cámbialo ahí y en `scripts/init-letsencrypt.sh`.

```bash
cp .env.example .env
```

Ajusta estas variables al dominio real de la VM (tiene que ser un dominio que ya resuelva a la IP del
servidor — Let's Encrypt no emite certificados para IPs sueltas):

```bash
NEXT_PUBLIC_API_URL=/api            # solo si usas el proxy; si no, pon la URL absoluta del backend
CORS_ALLOWED_ORIGINS=https://<tu-dominio>
APP_BASE_URL=https://<tu-dominio>
```

Levanta la app:

```bash
docker compose up -d --build
```

Si quieres HTTPS con el proxy, la primera vez pide el certificado (solo hace falta una vez por
servidor — necesita el puerto 80 abierto y el dominio ya apuntando aquí):

```bash
chmod +x scripts/init-letsencrypt.sh
./scripts/init-letsencrypt.sh
```

Esto deja nginx sirviendo HTTPS en el 443 (y redirigiendo el 80 a HTTPS) y un contenedor `certbot`
corriendo en segundo plano que renueva el certificado automáticamente cada 12h si toca.

A partir de ahí, para actualizar solo la app (sin tocar nginx/certbot):

```bash
docker compose up -d --build backend frontend
```

Y si quieres tirar y recrear el proxy (raro, solo si cambias `nginx.conf` o el dominio):

```bash
docker compose -f docker-compose.yml -f docker-compose.proxy.yml up -d --build nginx
```

La extensión de Netflix y el plugin de KOReader también apuntan a una URL fija de servidor por
defecto (ver sus respectivos READMEs para cambiarla en desarrollo local).
