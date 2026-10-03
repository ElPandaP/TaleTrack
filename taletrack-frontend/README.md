# TaleTrack — Frontend

Next.js 16 (App Router), React 19, Tailwind 4 + shadcn/ui. Consume el backend en `../TaleTrackApp`.

## Cómo arranca

```bash
bun install
bun run dev      # http://localhost:3000 (o :8090 dentro de docker-compose.dev.yml)
bun run lint
bun run build
```

En Docker (`docker compose -f ../docker-compose.dev.yml up -d`) el frontend corre en el puerto **8090**
con hot-reload, y Next.js hace de proxy: `/api/*` y `/swagger/*` se reescriben (`next.config.ts`) hacia
`http://backend:8080/...`, así que el navegador siempre habla con el mismo origen y nunca necesita saber
dónde vive el backend.

## Estructura

```
app/
├── page.tsx                # "/" — home logueado (carruseles, stats) o landing pública si no hay sesión
├── login/  register/       # formularios de auth (email+password, Google, OTP)
├── extension-auth/         # puente OAuth-like para dar tokens a la extensión de Netflix
├── (logged)/               # páginas con layout de columna + footer (la protección la da proxy.ts por prefijo)
│   ├── library/            # biblioteca: filtros, búsqueda, editar/borrar progreso
│   ├── media/[id]/         # ficha de una película/serie/libro
│   ├── reviews/            # tus reseñas
│   ├── activity/           # feed de actividad (tuya + amigos)
│   ├── friends/            # amigos, solicitudes, buscar usuarios
│   ├── profile/            # tu perfil, privacidad, borrar cuenta
│   ├── connections/        # sesiones activas y guías de conexión de KOReader y Netflix
│   ├── u/[id]/             # perfil público de otro usuario
│   ├── about/              # qué es TaleTrack (TFG, código y API abiertos) — pública
│   ├── credits/            # créditos de datos (TMDB, Open Library) — pública
│   └── privacy/            # política de privacidad (RGPD, cookies técnicas) — pública, enlazada desde el registro
├── _components/home/       # piezas del home (carrusel, stats, tarjetas)
components/
├── auth/                   # AuthShell, flujo de Google, pantallas de los enlaces de borrado, checklist de contraseña
├── activity/               # ActivityRow (una entrada del feed)
├── layout/                 # topnav, footer (Acerca de, Créditos, Privacidad, API, GitHub), toggles de tema/idioma, ConfirmDialog
├── media/                  # Cover, StarRating, ReviewModal, TrackingProgressModal
└── ui/                     # primitives shadcn (button, dialog, card...)
lib/
├── api/
│   ├── client.ts           # ApiClient — fetch con refresh automático de JWT en 401
│   ├── server.ts           # fetchers para Server Components (usa la cookie tt-token)
│   └── services/           # un fichero por dominio (auth, tracking, review, friend, user, sessions)
├── auth-context.tsx        # estado de auth vía useSyncExternalStore (no Context+useState)
├── auth-storage.ts         # nombres de las claves de localStorage y de las cookies de sesión
├── jwt.ts                  # decodifica el payload del JWT y comprueba si ha caducado
├── password.ts             # regla de contraseñas (8+ caracteres, mayúscula y número)
├── i18n.tsx / i18n-shared.ts / i18n-server.ts   # ver sección de i18n abajo
└── types/index.ts          # tipos de las respuestas del backend
```

## Auth

El access token se guarda en `localStorage` (`token`) y el refresh token en `tt-refresh`, y los dos
se copian en cookies (`tt-token` y `tt-refresh`, `SameSite=Lax`) para que el middleware (`proxy.ts`)
y los Server Components puedan leerlos. Los nombres están en `lib/auth-storage.ts`; el estado lo
expone `lib/auth-context.tsx`. `ApiClient`
(`lib/api/client.ts`) reintenta una vez con refresh automático si una petición devuelve 401.

Tres formas de entrar: email+contraseña, Google (`@react-oauth/google`), o código OTP por email
(`RequestCode` → `VerifyCode`). `proxy.ts` protege `/library /reviews /activity /friends
/profile /media /u /connections` y redirige a `/login` sin sesión (y al revés: si ya tienes sesión, `/login` y
`/register` te mandan a `/`).

## i18n

Sitio bilingüe es/en, sin librería externa — diccionarios planos en `messages/{en,es}.json`.

- **Client Components**: `useT()` / `useI18n()` de `lib/i18n.tsx` (Context + hook).
- **Server Components**: `getServerT()` / `getServerLocale()` de `lib/i18n-server.ts` — **no** las
  importes desde un componente cliente, tira de `next/headers` y rompe el bundle. Si necesitas algo
  compartido entre cliente y servidor (el tipo `Locale`, `isLocale`...), va en `lib/i18n-shared.ts`.
- El locale se guarda en la cookie `tt-locale`; sin cookie, se adivina por `Accept-Language`.

## Diseño

Tema "cottagecore" (verde salvia + crema cálido) en claro, gris/slate neutro con el mismo acento en
oscuro — variables OKLCH en `app/globals.css`. Tipografía: Geist (sans) + Cormorant Garamond
(`font-heading`, para títulos). `font-size: 112.5%` en `html` escala toda la app.

## Variables de entorno

| Variable | Uso |
|---|---|
| `INTERNAL_API_URL` | base que usan los Server Components para llamar directo al backend (`http://backend:8080/api` en Docker) |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | login con Google |

## Notas

- No hay test runner configurado para el frontend — la verificación es `bun run lint` +
  `bun x tsc --noEmit` + probar la app en el navegador.
- `'use client'` solo donde hace falta de verdad (hooks, estado, listeners de browser). La mayoría de
  piezas del home lo necesitan porque `useT()`/`useI18n()` son hooks de React; las páginas puramente
  estáticas (`/about`, `/credits`, `/privacy`) usan `getServerT()` y son Server Components.
