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
├── (app)/                  # grupo de rutas protegidas (ver proxy.ts) — comparten layout con footer
│   ├── library/            # biblioteca: filtros, búsqueda, editar/borrar progreso
│   ├── media/[id]/         # ficha de una película/serie/libro
│   ├── reviews/            # tus reseñas
│   ├── activity/           # feed de actividad (tuya + amigos)
│   ├── friends/            # amigos, solicitudes, buscar usuarios
│   ├── profile/            # tu perfil, privacidad, sesiones activas, borrar cuenta
│   ├── u/[id]/              # perfil público de otro usuario
│   └── about/               # créditos (TMDB, Open Library) — Server Component, sin 'use client'
├── _components/home/       # piezas del home (carrusel, stats, tarjetas)
components/
├── layout/                 # topnav, footer, toggles de tema/idioma
├── media/                  # Cover, StarRating, ReviewModal, TrackingProgressModal
└── ui/                     # primitives shadcn (button, dialog, card...)
lib/
├── api/
│   ├── client.ts           # ApiClient — fetch con refresh automático de JWT en 401
│   ├── server.ts           # fetchers para Server Components (usa la cookie tt-token)
│   └── services/           # un fichero por dominio (auth, tracking, review, friend, library, user, sessions)
├── auth-context.tsx        # estado de auth vía useSyncExternalStore (no Context+useState)
├── i18n.tsx / i18n-shared.ts / i18n-server.ts   # ver sección de i18n abajo
└── types/index.ts          # tipos de las respuestas del backend
```

## Auth

`lib/auth-context.tsx` guarda el JWT en `localStorage` (`tt-token`) y en una cookie del mismo nombre
(`SameSite=Lax`) para que el middleware (`proxy.ts`) y los Server Components puedan leerlo. `ApiClient`
(`lib/api/client.ts`) reintenta una vez con refresh automático si una petición devuelve 401.

Tres formas de entrar: email+contraseña, Google (`@react-oauth/google`), o código OTP por email
(`RequestCode` → `VerifyCode`). `proxy.ts` protege `/dashboard /library /reviews /activity /friends
/profile /media /u` y redirige a `/login` sin sesión (y al revés: si ya tienes sesión, `/login` y
`/register` te mandan a `/`).

## i18n

Sitio bilingüe es/en, sin librería externa — diccionarios planos en `messages/{en,es}.json`.

- **Client Components**: `useT()` / `useI18n()` de `lib/i18n.tsx` (Context + hook).
- **Server Components**: `getServerDict()` / `getServerLocale()` de `lib/i18n-server.ts` — **no** las
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
| `NEXT_PUBLIC_API_URL` | base de la API vista por el navegador (normalmente `/api`, vía el rewrite) |
| `INTERNAL_API_URL` | base que usan los Server Components para llamar directo al backend (`http://backend:8080/api` en Docker) |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | login con Google |

## Notas

- No hay test runner configurado para el frontend — la verificación es `bun run lint` +
  `bun x tsc --noEmit` + probar la app en el navegador.
- `'use client'` solo donde hace falta de verdad (hooks, estado, listeners de browser). La mayoría de
  piezas del home lo necesitan porque `useT()`/`useI18n()` son hooks de React; las páginas puramente
  estáticas (como `/about`) usan `getServerDict()` y son Server Components.
