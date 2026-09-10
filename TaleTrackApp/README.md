# TaleTrack — Backend

ASP.NET Core 10 (.NET 10), Minimal APIs, patrón **REPR** (Request → Endpoint → Response).
PostgreSQL 15 vía EF Core. Autenticación JWT + refresh tokens + API key interna.

> Mapa rápido para reorientarse. Para el detalle vivo, mira siempre `Program.cs` (registro de
> endpoints y pipeline) y la carpeta `Features/`.

---

## 1. Cómo arranca (`Program.cs`)

Todo el arranque está en el top-level `Program.cs` con funciones locales:

```
loadEnvironment()      -> carga ../.env con DotNetEnv (relativo al cwd)
configureDatabase()    -> DbContext Npgsql; se SALTA si Environment == "Testing"
configureAuth()        -> JWT bearer + 3 policies + handler de API key + EmailService HttpClient
configureApi()         -> Swagger, servicios scoped (REPR), OpenLibrary/TMDB HttpClient, ValidationFilter
configureCors()        -> policy "FrontendCors" desde CORS_ALLOWED_ORIGINS (coma-separado)
--- build ---
configurePipeline():
    applyMigrations()  -> db.Database.Migrate() automático en cada arranque (EnsureCreated si es SQLite)
    Swagger + SwaggerUI (siempre activos, incluso en prod, en /swagger)
    UseHttpsRedirection
    UseCors -> UseAuthentication -> UseAuthorization
    grupo "/api" y cada *Endpoint.Map(apiGroup)
```

- **Migraciones**: se aplican solas al arrancar. Para crear una nueva:
  `dotnet ef migrations add <Nombre>` (nunca escribir el archivo a mano).
- **Config JWT**: `appsettings.json > JwtSettings` (Issuer/Audience `TaleTrackApp`, access token 60 min,
  refresh token 60 días). El secreto viene de env `JwtSettings__Secret` (doble guion bajo = jerarquía
  en .NET config).

---

## 2. Organización de carpetas

```
TaleTrackApp/
├── Program.cs                 # arranque + registro de TODOS los endpoints
├── appsettings.json           # JwtSettings, Logging, OpenLibrary:SimilarityThreshold
├── Auth/
│   ├── Policies.cs            # constantes: UserPolicy / InternalOnly / UserAndInternal
│   ├── JwtService.cs          # GenerateToken(userId, email, username)
│   ├── RefreshTokenService.cs # emite/rota/revoca refresh tokens; lista sesiones activas
│   ├── InternalApiKeyHandler.cs  # valida header X-Internal-Api-Key contra env INTERNAL_API_KEY
│   ├── ValidationFilter.cs    # IEndpointFilter: valida DataAnnotations de cada argumento -> 400
│   └── EmailService.cs        # manda códigos OTP vía API de Resend (https://api.resend.com/emails)
├── Data/
│   ├── AppDbContext.cs        # DbSets: Users, Medias, Reviews, TrackingEvents, Friendships, RefreshTokens
│   └── Migrations/            # generadas por EF, nunca a mano
├── Model/                     # entidades EF: User, Media, Review, TrackingEvent, Friendship, RefreshToken
├── Features/
│   ├── User/  Media/  TrackingEvent/  Review/  Friend/  Activity/  Auth/
│   ├── Stats/                 # StatsService + GetStats/  → GET /api/stats (resumen anual)
│   └── Library/               # LibraryService + GetLibrary/  → GET /api/library (1 fila por media)
└── Features/<Feature>/<Accion>/
        ├── <Accion>Endpoint.cs   # static class con Map(RouteGroupBuilder) + HandleAsync(...)
        ├── <Accion>Request.cs    # DTO de entrada con DataAnnotations (cuando el endpoint recibe body/query)
        └── (Response opcional)
    Features/<Feature>/<Feature>Service.cs   # lógica compartida del feature (scoped)
```

**Patrón de un endpoint** (todos iguales):

```csharp
public static class XEndpoint {
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/x", HandleAsync)
             .AddEndpointFilter<ValidationFilter>()
             .RequireAuthorization(Policies.UserPolicy);

    private static async Task<IResult> HandleAsync(XRequest req, XService svc, ClaimsPrincipal user, ILogger<XRequest> log)
    { ... return Results.Ok(new { success = true, ... }); }
}
```

El `userId` se saca siempre de `user.FindFirst(ClaimTypes.NameIdentifier)?.Value` y se parsea a `int`.

---

## 3. Autenticación y autorización

| Policy | Requiere | Se usa para |
|---|---|---|
| `UserPolicy` | JWT válido (`RequireAuthenticatedUser`) | endpoints de datos del usuario |
| `InternalOnly` | header `X-Internal-Api-Key` == env `INTERNAL_API_KEY` | register, lectura interna de media |
| `UserAndInternal` | ambos | definido pero **no usado** actualmente |

Varios endpoints encadenan `.RequireAuthorization(UserPolicy).RequireAuthorization(InternalOnly)` →
exigen **JWT + API key** a la vez (reseñas, editar/borrar usuario).

**Claims del JWT**: `sub` / `NameIdentifier` (userId), `email`, `unique_name` (username), `jti`.
Expira a los 60 min; el frontend/extensión lo renuevan con el refresh token sin que el usuario haga nada.

**3 formas de login**, todas devuelven `{ success, message, token, refreshToken, expiresIn }`:
1. Email + password → `POST /api/login`
2. Google OAuth → `POST /api/auth/google` (valida el idToken con `Google.Apis.Auth`; crea o vincula usuario por `GoogleId`/email)
3. Email OTP → `POST /api/auth/request-code` (manda código de 6 dígitos, expira 10 min) + `POST /api/auth/verify-code`

**Sesiones / refresh tokens** (`RefreshTokenService`, un `RefreshToken` por dispositivo/cliente):
- `POST /api/auth/refresh` — cambia un refresh token por un par nuevo (rota; hay una ventana de gracia de
  60s para tolerar reintentos duplicados por red).
- `POST /api/auth/logout` — revoca el refresh token indicado.
- `GET /api/auth/sessions` — lista los dispositivos/sesiones activas del usuario ("Conexiones").
- `DELETE /api/auth/sessions/{id}` — revoca una sesión concreta (solo si es tuya).
- `POST /api/auth/extension-grant` — con el JWT del navegador, emite un par de tokens propio para la
  extensión de Netflix (aparece como sesión `"Netflix extension"`).

**Registro**: `POST /api/register` → **solo con API key interna** (el frontend la inyecta desde
`NEXT_PUBLIC_INTERNAL_API_KEY`). No hace auto-login; el frontend llama a `/login` después.

⚠️ **Gotchas de seguridad** (relevante para el TFG):
- El hash de password es **SHA-256 sin sal** (`UserService.HashPassword`). No es bcrypt/argon2.
- Swagger UI queda expuesto siempre, también en producción.

---

## 4. Endpoints (todos bajo `/api`)

### Auth / sesiones
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| POST | `/login` | anónimo | login email+password |
| POST | `/auth/google` | anónimo | login/registro con Google idToken |
| POST | `/auth/request-code` | anónimo | envía OTP al email (respuesta genérica, no filtra si existe) |
| POST | `/auth/verify-code` | anónimo | valida OTP → JWT |
| POST | `/auth/refresh` | anónimo (usa el refresh token del body) | rota el par de tokens |
| POST | `/auth/logout` | anónimo | revoca un refresh token |
| POST | `/auth/extension-grant` | JWT | emite tokens propios para la extensión |
| GET | `/auth/sessions` | JWT | lista tus sesiones activas |
| DELETE | `/auth/sessions/{id}` | JWT | revoca una sesión tuya |
| POST | `/register` | **InternalOnly** | crea usuario (email, username, password ≥6) |

### User
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| GET | `/user/me` | JWT | perfil completo (avatar, privacidad) del usuario autenticado |
| PUT | `/user/{id}` | JWT + Internal | edita username/email/password/avatarUrl + `privacy{...}`; solo uno mismo (403 si no) |
| DELETE | `/user/{id}` | JWT + Internal | borra la cuenta; solo uno mismo. Cascade borra Reviews + TrackingEvents |
| GET | `/users/{id}` | JWT | perfil público: `{ id, username, avatarUrl, createdAt, relationship, counts{book,movie,series,total} }` |
| GET | `/users/search?username=` | JWT | busca usuario por username exacto (quita `@` inicial) |

### Media
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| POST | `/media` | JWT | crea Media suelto vía `MediaService.CreateAsync` (**sin dedup**) |
| GET | `/media` | **InternalOnly** | lista media con filtros `?type=&limit=&orderBy=` (no filtra por usuario) |
| GET | `/media/{id}` | JWT | ficha de un media: datos + tu progreso/reseña + todas las reseñas + nota media |

### Tracking (`Features/TrackingEvent/`)
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| POST | `/tracking/movies` | JWT | registra progreso de una película. `FindOrCreateAsync` deduplica el Media |
| POST | `/tracking/series` | JWT | registra progreso de un episodio (`Season`+`Episode` obligatorios) |
| POST | `/tracking/books` | JWT | registra progreso de un libro; si falta autor/portada dispara enriquecimiento OpenLibrary en background |
| PUT | `/tracking/{mediaId}` | JWT | sobrescribe el progreso del evento más reciente de ese media (editar desde la biblioteca) |
| DELETE | `/tracking/{mediaId}` | JWT | borra todo el seguimiento de ese media (lo saca de tu biblioteca) |

Las tres rutas `POST /tracking/{movies,series,books}` deduplican el `Media` por título (y por
`AltTitle` si TMDB ya rellenó una traducción — ver más abajo), y para movies/series lanzan
enriquecimiento TMDB en background si falta portada o `AltTitle` y el request trae `Language` ("es"/"en").

No hay endpoint para **leer** los eventos de tracking en crudo — para eso está `/library`, que ya
colapsa a una fila por media. (Existió un `GET /tracking` y un `GET /books`; se quitaron por no
tener ningún consumidor real.)

### Library / Stats (`Features/Library/`, `Features/Stats/` — alimentan el home `/`)
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| GET | `/stats` | JWT | resumen anual del usuario: `?year=` (def. actual) → `{ year, total, byType{book,movie,series}, byMonth[12], reviewCount }` |
| GET | `/library` | JWT | biblioteca del usuario, **una fila por media** (evento más reciente gana) + `myRating`/`myReviewId`. Filtros `?type=Book\|Movie\|Series&status=in_progress\|finished&sort=recent\|rating&year=&limit=` |
| GET | `/reviews` | JWT | reseñas escritas por el usuario, con `media` anidado |
| GET | `/reviews/pending` | JWT | media terminada (progreso 100) que el usuario aún no ha reseñado |

### Friends & Activity (`Features/Friend/`, `Features/Activity/` — todo JWT)
| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/friends` | `{ friends, incoming, outgoing }` (amigos aceptados + solicitudes) |
| POST | `/friends/requests` `{userId}` | envía solicitud a ese usuario (responde con `code` estable para mapear el error en el frontend) |
| POST | `/friends/requests/{id}` `{accept}` | acepta / rechaza una entrante |
| DELETE | `/friends/{userId}` | elimina amistad o cancela solicitud |
| GET | `/activity?scope=all\|mine\|friends&limit=` | feed derivado de TrackingEvent + Review: `started`/`finished`/`reviewed`. Filtra por la privacidad **por tipo de media** de cada usuario (`Share{Book,Movie,Series}{Progress,Reviews}`) |
| GET | `/activity?userId=N&limit=` | actividad de un solo usuario (para su perfil público); vacío salvo que seas tú o su amigo |

### Review
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| POST | `/review` | JWT + Internal | crea reseña (mediaId, rating 1–10, comment opcional) |
| PUT | `/review/{id}` | JWT + Internal | edita; solo el dueño (403 si no) |
| DELETE | `/review/{id}` | JWT + Internal | borra; solo el dueño (403 si no) |

**Validación de entrada**: `ValidationFilter` (IEndpointFilter) recorre los argumentos y valida sus
DataAnnotations; si falla devuelve `400 { message: "err1; err2" }`. El texto de esos mensajes está en
español cuando el error lo puede haber causado el usuario (contraseña corta, email inválido...); se deja
en inglés cuando es un fallo de estado/lógica que la UI nunca llega a mostrar tal cual (sesión no
encontrada, parámetro interno ausente...).

---

## 5. Modelo de datos (`Model/`, DbSets en `AppDbContext`)

| Entidad | Campos clave | Notas |
|---|---|---|
| **User** | Email, Username, PasswordHash?, GoogleId?, EmailCode?/Expiry?, AvatarUrl?, IsActive, CreatedAt, 6 flags `Share{Book,Movie,Series}{Progress,Reviews}` | password/google opcionales → un user puede ser solo-Google |
| **Media** | Title, AltTitle?, Type, Length, Description?, PosterUrl?, Author?, Isbn?, FirstTrackedAt, UpdatedAt? | `Type` regex `^(Movie\|Series\|Book)$`. `AltTitle` = título en el otro idioma (es↔en), rellenado por TMDB |
| **TrackingEvent** | UserId→, MediaId→, Progress? (0–100), Season?, Episode?, EpisodeTitle?, EventDate | `EventDate` = `DateTime.UtcNow` al crear/actualizar |
| **Review** | UserId→, MediaId→, Rating (1–10), Comment?, CreatedAt, UpdatedAt? | |
| **Friendship** | RequesterId→, AddresseeId→, Status ("Pending"/"Accepted"), CreatedAt, RespondedAt? | índice único en (Requester, Addressee); ambos FK cascade-delete |
| **RefreshToken** | UserId→, TokenHash, Device, CreatedAt, LastUsedAt, ExpiresAt, RevokedAt?, ReplacedByTokenHash? | una fila por sesión/dispositivo; rotación con ventana de gracia de 60s |

- **Cascade delete** configurado en `OnModelCreating`: borrar un `User` borra sus `Review`,
  `TrackingEvent`, `Friendship` y `RefreshToken`.
- **Dedup de Media** (`MediaService.FindOrCreateAsync`): prioridad ISBN → Title+Author → Title →
  `AltTitle` (siempre dentro del mismo `Type`).
- **Enriquecimiento de libros** (`OpenLibraryService`): busca por ISBN o por título/autor (similitud de
  tokens, umbral 0.75 configurable) y rellena Author / PosterUrl / Isbn si faltan.
  Portadas: `covers.openlibrary.org`.
- **Enriquecimiento de películas/series** (`TmdbService`, requiere env `TMDB_API_KEY`): busca en TMDB por
  título (con el mismo matching por similitud), y si encuentra coincidencia rellena `PosterUrl`,
  `Length` (runtime) y `AltTitle` (traducción al otro idioma vía `append_to_response=translations`).
  Solo se activa si el request trae `Language` ("es"/"en" — lo manda la extensión de Netflix leyendo
  `<html lang>`); sin API key configurada, o sin `Language`, no hace nada y no rompe el flujo normal.

---

## 6. Paquetes NuGet (`TaleTrackApp.csproj`)

| Paquete | Para qué |
|---|---|
| `Microsoft.EntityFrameworkCore` | ORM |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | provider PostgreSQL |
| `Microsoft.EntityFrameworkCore.Design` | `dotnet ef` (migraciones) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | validación de JWT |
| `System.IdentityModel.Tokens.Jwt` | generación de JWT |
| `Google.Apis.Auth` | validar idToken de Google |
| `DotNetEnv` | cargar `.env` |
| `Swashbuckle.AspNetCore` | Swagger / OpenAPI |

Sin librería de hashing (usa `System.Security.Cryptography.SHA256`), sin MediatR, sin FluentValidation,
sin AutoMapper. DI a mano en `Program.cs`. TMDB y Open Library se consumen con `HttpClient` a pelo, sin SDK.

---

## 7. Variables de entorno

Se leen de `../.env` (repo root).

| Variable | Uso |
|---|---|
| `POSTGRES_HOST/PORT/USER/PASSWORD/DB` | connection string (se construye a mano en `configureDatabase`) |
| `JWT_SECRET` / `JwtSettings__Secret` | firma del JWT |
| `INTERNAL_API_KEY` | policy `InternalOnly` |
| `GOOGLE_CLIENT_ID` | audience al validar el idToken de Google |
| `RESEND_API_KEY` | Bearer para la API de Resend (emails OTP) |
| `TMDB_API_KEY` | enriquecimiento de películas/series (portada, duración, título en el otro idioma). Sin ella, ese enriquecimiento simplemente no hace nada |
| `CORS_ALLOWED_ORIGINS` | orígenes permitidos, coma-separado (default `http://localhost:8090`) |

---

## 8. Tests (`../TaleTrackApp.Tests`)

- **xUnit** + `Microsoft.AspNetCore.Mvc.Testing` (integración end-to-end sobre `WebApplicationFactory<Program>`).
- `CustomWebApplicationFactory`: fuerza `Environment=Testing` (salta Npgsql), inyecta **SQLite in-memory**
  (con conexión KeepAlive compartida entre toda la colección de tests), stubbea las llamadas HTTP de
  OpenLibrary y TMDB (siempre 404 — nunca pegan a la red real), y fija `JwtSettings__Secret` /
  `INTERNAL_API_KEY` / `TMDB_API_KEY` de test.
- Un fichero de tests por área de negocio, todos con el sufijo `*FlowTests.cs`:
  `AuthFlowTests`, `TrackingSplitTests` (movies/series, upsert, progreso monótono), `BookTrackingFlowTests`,
  `TmdbTrackingFlowTests` (dedup entre usuarios, enriquecimiento), `ReviewFlowTests`, `FriendFlowTests`,
  `LibraryFlowTests`, `ActivityFlowTests` (privacidad, amigos), `UserProfileFlowTests`, `SessionsFlowTests`,
  `StatsFlowTests`.
- Correr: `dotnet test ../TaleTrackApp.Tests`

---

## 9. Cómo levantarlo

```bash
# Solo DB + migraciones (sin .NET local):
docker compose up -d postgres
docker compose --profile db run --rm migrator

# Stack de desarrollo completo (hot-reload):
docker compose -f docker-compose.dev.yml up -d
#   backend  -> http://localhost:8080  (Swagger en /swagger)
#   frontend -> http://localhost:8090
#   logs     -> http://localhost:9999  (dozzle)

# Local sin Docker:
cd TaleTrackApp && dotnet run
```

Para meter un usuario de prueba con datos: `../scripts/seed-demo.ps1` (registra `demo@taletrack.dev` /
`demo1234` + tracking events de ejemplo).

---

## 10. Consumidores del backend

| Cliente | Cómo llama |
|---|---|
| `taletrack-frontend` (Next.js) | navegador → `/api/*` (rewrite a `backend:8080`). SSR usa `lib/api/server.ts` con la cookie `tt-token` + `X-Internal-Api-Key` |
| `taletrack.koplugin` (KOReader, Lua, submódulo git) | login por OTP (`/api/auth/request-code` + `/verify-code`), luego `POST /api/tracking/books` con `Authorization: Bearer` al terminar un libro |
| `taletrack-netflix-extension` | detecta reproducción en Netflix, extrae título/temporada/episodio/progreso/idioma y postea a `/api/tracking/movies` o `/api/tracking/series` cada ~15s (con throttle por % de avance) vía el service worker |

> El koplugin y la extensión apuntan por defecto al servidor de producción hardcodeado
> (`http://143.47.54.63`); para desarrollo local se sobreescribe (ver el README de cada uno).
