# TaleTrack — Web app

Next.js 16 (App Router), React 19, Tailwind CSS 4 + shadcn/ui. It talks to the backend in `../TaleTrackApp`.

## Running it

```bash
bun install
bun run dev      # http://localhost:3000 (or :8090 inside docker-compose.dev.yml)
bun run lint
bun run build
```

In Docker (`docker compose -f ../docker-compose.dev.yml up -d`) the web app runs on port **8090**
with hot reload, and Next.js acts as a proxy: `/api/*` and `/swagger/*` are rewritten
(`next.config.ts`) to `http://backend:8080/...`, so the browser always talks to the same origin and
never needs to know where the backend lives.

## Structure

```
app/
├── page.tsx                # "/": signed-in home (carousels, stats) or public landing page without a session
├── login/  register/       # auth forms (email + password, Google, one-time code)
├── extension-auth/         # OAuth-like bridge that hands tokens to the Netflix extension
├── (logged)/               # pages with the column + footer layout (proxy.ts protects them by path prefix)
│   ├── library/            # library: filters, search, edit/delete progress
│   ├── media/[id]/         # a film, series or book page
│   ├── reviews/            # your reviews
│   ├── activity/           # activity feed (yours + friends)
│   ├── friends/            # friends, requests, user search
│   ├── profile/            # your profile, privacy, account deletion
│   ├── connections/        # active sessions and the KOReader and Netflix install guides
│   ├── u/[id]/             # another user's public profile
│   ├── about/              # what TaleTrack is (TFG, open code and API), public
│   ├── credits/            # data credits (TMDB, Open Library), public
│   └── privacy/            # privacy policy (GDPR, technical cookies), public, linked from sign-up
├── _components/home/       # home page pieces (carousel, stats, cards)
components/
├── auth/                   # AuthShell, Google flow, email-link screens, password checklist
├── activity/               # ActivityRow (one feed entry)
├── layout/                 # top nav, footer (About, Credits, Privacy, API, GitHub), theme/language toggles, ConfirmDialog
├── media/                  # Cover, StarRating, ReviewModal, TrackingProgressModal
└── ui/                     # shadcn primitives (button, dialog, card...)
lib/
├── api/
│   ├── client.ts           # ApiClient: fetch with automatic JWT refresh on 401
│   ├── server.ts           # fetchers for Server Components (use the tt-token cookie)
│   └── services/           # one file per domain (auth, tracking, review, friend, user, sessions)
├── auth-context.tsx        # auth state through useSyncExternalStore (not Context + useState)
├── auth-storage.ts         # names of the localStorage keys and session cookies
├── jwt.ts                  # decodes the JWT payload and checks whether it has expired
├── password.ts             # password rule (8+ characters, an uppercase letter and a digit)
├── i18n.tsx / i18n-shared.ts / i18n-server.ts   # see the i18n section below
└── types/index.ts          # types of the backend responses
```

## Auth

The access token is stored in `localStorage` (`token`) and the refresh token in `tt-refresh`, and
both are copied into cookies (`tt-token` and `tt-refresh`, `SameSite=Lax`) so the middleware
(`proxy.ts`) and Server Components can read them. The names live in `lib/auth-storage.ts`; the state
is exposed by `lib/auth-context.tsx`. `ApiClient` (`lib/api/client.ts`) retries once with an
automatic refresh when a request returns 401.

Three ways to sign in: email + password, Google (`@react-oauth/google`), or a one-time code by email
(`RequestCode` → `VerifyCode`). `proxy.ts` protects `/library /reviews /activity /friends /profile
/media /u /connections` and redirects to `/login` without a session (and the other way round: with
a session, `/login` and `/register` send you to `/`). It also refreshes an expired access token from
the refresh cookie before rendering.

## i18n

Bilingual site (Spanish/English) without an external library: flat dictionaries in
`messages/{en,es}.json`.

- **Client Components**: `useT()` / `useI18n()` from `lib/i18n.tsx` (Context + hook).
- **Server Components**: `getServerT()` / `getServerLocale()` from `lib/i18n-server.ts`. **Do not**
  import them from a client component: they use `next/headers` and break the bundle. Anything shared
  between client and server (the `Locale` type, `isLocale`...) goes in `lib/i18n-shared.ts`.
- The locale is stored in the `tt-locale` cookie; without it, it is guessed from `Accept-Language`.
- Backend errors arrive as a stable `code` that the web app translates, so the backend never sends
  user-facing text.

## Design

"Cottagecore" theme (sage green + warm cream) in light mode, neutral grey/slate with the same accent
in dark mode, as OKLCH variables in `app/globals.css`. Fonts: Geist (sans) + Cormorant Garamond
(`font-heading`, for headings). `font-size: 112.5%` on `html` scales the whole app.

## Environment variables

| Variable | Use |
|---|---|
| `INTERNAL_API_URL` | base URL Server Components use to call the backend directly (`http://backend:8080/api` in Docker) |
| `NEXT_PUBLIC_GOOGLE_CLIENT_ID` | Google sign-in (Docker Compose fills it from `GOOGLE_CLIENT_ID`) |
| `NEXT_PUBLIC_EXTENSION_ID` | id of the Netflix extension that `/extension-auth` sends tokens to; optional, defaults to the id fixed by the `key` in the extension's `manifest.json` |

## Notes

- There is no test runner for the web app: it is checked with `bun run lint`, `bun x tsc --noEmit`
  and by using the app in the browser.
- `'use client'` only where it is really needed (hooks, state, browser listeners). Most home page
  pieces need it because `useT()`/`useI18n()` are React hooks; purely static pages (`/about`,
  `/credits`, `/privacy`) use `getServerT()` and are Server Components.
