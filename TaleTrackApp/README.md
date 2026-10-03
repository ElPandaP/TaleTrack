# TaleTrack — Backend

ASP.NET Core 10 (.NET 10), Minimal APIs, patrón **REPR** (Request → Endpoint → Response).
PostgreSQL 15 vía EF Core. Autenticación JWT + refresh tokens.

> La API es pública por diseño: cualquier cliente (frontend, extensión de Netflix, plugin de
> KOReader, o uno de terceros) se autentica igual, con el JWT del usuario. No hay clave de API.

> Mapa rápido para reorientarse. Para el detalle vivo, mira siempre `Program.cs` (registro de
> endpoints y pipeline) y la carpeta `Features/`.

---

## 1. Cómo arranca (`Program.cs`)

Todo el arranque está en el top-level `Program.cs` con funciones locales:

```
loadEnvironment()      -> carga ../.env con DotNetEnv (relativo al cwd)
configureDatabase()    -> DbContext Npgsql; se SALTA si Environment == "Testing"
configureAuth()        -> JWT bearer + policy UserPolicy + EmailService HttpClient + BackgroundRunner
configureApi()         -> Swagger, servicios scoped (REPR), OpenLibrary/TMDB HttpClient, ValidationFilter
--- build ---
configurePipeline():
    applyMigrations()  -> db.Database.Migrate() automático en cada arranque (EnsureCreated si es SQLite)
    Swagger + SwaggerUI (siempre activos, incluso en prod, en /swagger)
    UseRateLimiter -> UseAuthentication -> UseAuthorization
    (rate limit global por IP: 200/min, o RateLimiting:PermitLimit; en Testing solo se activa si se fija ese valor)
    grupo "/api" y cada *Endpoint.Map(apiGroup)
```

- **Migraciones**: se aplican solas al arrancar. Para crear una nueva:
  `dotnet ef migrations add <Nombre>` (nunca escribir el archivo a mano).
- **Config JWT**: `appsettings.json > JwtSettings` (Issuer/Audience `TaleTrackApp`, access token 5 min,
  refresh token 60 días). El secreto viene de env `JwtSettings__Secret` (doble guion bajo = jerarquía
  en .NET config).

---

## 2. Organización de carpetas

```
TaleTrackApp/
├── Program.cs                 # arranque + registro de TODOS los endpoints
├── appsettings.json           # JwtSettings, Logging, OpenLibrary:SimilarityThreshold
├── Security/                  # piezas que se enganchan al pipeline de los endpoints
│   ├── Policies.cs            # constante: UserPolicy (JWT válido)
│   ├── ClaimsPrincipalExtensions.cs  # TryGetUserId: el userId del JWT
│   └── ValidationFilter.cs    # IEndpointFilter: valida DataAnnotations de cada argumento -> 400
├── OpenApi/                   # configuración de Swagger y helpers .Responds...() de los endpoints
├── Services/                  # servicios usados por varios features (no pertenecen a uno)
│   ├── JwtService.cs          # GenerateToken(userId, email, username)
│   └── BackgroundRunner.cs    # ejecuta trabajo tras responder (scope DI propio, errores logueados)
├── Data/
│   ├── AppDbContext.cs        # DbSets: Users, Medias, Reviews, TrackingEvents, Friendships, RefreshTokens, AuthActionTokens, UserAvatars
│   └── Migrations/            # generadas por EF, nunca a mano
├── Model/                     # entidades EF: User, Media, Review, TrackingEvent, Friendship, RefreshToken, AuthActionToken, UserAvatar
├── Features/
│   ├── User/                  # UserService + AvatarService: perfil, avatar, búsqueda (no un único <Feature>Service.cs)
│   ├── Media/  TrackingEvent/  Review/  Friend/  Activity/
│   ├── Auth/                  # registro, logins y sesiones: SessionService + AuthActionTokenService + GoogleAuthService (+ GoogleIdTokenValidator, GoogleSignupTokenService, TokenHasher, EmailService)
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

    private static async Task<IResult> HandleAsync(XRequest req, XService svc, ClaimsPrincipal user)
    {
        if (!user.TryGetUserId(out var userId))
            return Results.Unauthorized();
        ...
        return Results.Ok(new { success = true, ... });
    }
}
```

El `userId` (un `Guid`) se saca siempre con `user.TryGetUserId(out var userId)`. Los endpoints no
capturan excepciones genéricas: un error no previsto llega al manejador global, que lo registra y
responde un 500 con ProblemDetails.

---

## 3. Autenticación y autorización

| Policy | Requiere | Se usa para |
|---|---|---|
| `UserPolicy` | JWT válido (`RequireAuthenticatedUser`) | todos los endpoints de datos del usuario |

Es la única policy. La API es pública por diseño (un tercero puede construir su propio
cliente y autenticarse igual que el usuario); el JWT es el único control de acceso.
`POST /api/register` y `GET /api/users/{id}/avatar` son anónimos.

**Claims del JWT**: `sub` / `NameIdentifier` (userId), `email`, `unique_name` (username), `jti`.
Expira a los 5 min; el frontend/extensión lo renuevan con el refresh token sin que el usuario haga nada.

**3 formas de login**, todas devuelven `{ success, message, token, refreshToken, expiresIn }`:
1. Email + password → `POST /api/login`
2. Google OAuth → `POST /api/auth/google` (valida el idToken con `Google.Apis.Auth`; crea o vincula usuario por `GoogleId`/email)
3. Email OTP → `POST /api/auth/request-code` (manda código de 6 dígitos, expira 10 min) + `POST /api/auth/verify-code`

**Sesiones / refresh tokens** (`SessionService`, un `RefreshToken` por dispositivo/cliente; todos los logins arrancan la sesión con `SessionService.StartAsync`):
- `POST /api/auth/refresh` — cambia un refresh token por un par nuevo (rota; hay una ventana de gracia de
  60s para tolerar reintentos duplicados por red).
- `POST /api/auth/logout` — revoca el refresh token indicado.
- `GET /api/auth/sessions` — lista los dispositivos/sesiones activas del usuario ("Conexiones").
- `DELETE /api/auth/sessions/{id}` — revoca una sesión concreta (solo si es tuya).
- `POST /api/auth/extension-grant` — con el JWT del navegador, emite un par de tokens propio para la
  extensión de Netflix (aparece como sesión `"Netflix extension"`).

**Registro**: `POST /api/register` → **anónimo** (registro abierto). No hace auto-login; el
frontend llama a `/login` después.

**Hash de contraseñas**: PBKDF2-HMAC-SHA512 con sal por hash y 210 000 iteraciones, vía
`PasswordHasher<User>` de ASP.NET Core Identity (`UserService`). La migración
`ClearLegacyPasswordHashes` anuló los hashes SHA-256 sin sal anteriores; esos usuarios
entran con código por email y se ponen contraseña nueva desde el perfil.

⚠️ **Gotchas de seguridad** (relevante para el TFG):
- Swagger UI queda expuesto siempre, también en producción.

---

## 4. Endpoints (todos bajo `/api`)

### Documentación OpenAPI (`/swagger`)
La configuración vive en `OpenApi/` (info, esquema Bearer, tags con su descripción y un filtro que añade
el candado, el `401` de los endpoints protegidos y el `429` global). Al crear un endpoint:
- En `Map`: `.WithTags(...)` (uno de los de `OpenApiConfiguration.Tags`), `.WithSummary(...)` y, si hay
  comportamiento no obvio, `.WithDescription(...)`.
- Cada respuesta con `.Responds<T>("qué significa")` / `.RespondsBadRequest(...)` / `.RespondsNotFound(...)`
  / `.Responds(código, "...")` (sin cuerpo). Los cuerpos de error/confirmación se describen con
  `ApiError` y `ApiResult`; las respuestas con datos, con una clase `*Response` propia (no objetos anónimos,
  que Swagger no puede describir).
- Cada propiedad de un DTO lleva `/// <summary>`; los `record` documentan con `<param>`.
- `OpenApiDocumentationTests` falla si falta cualquiera de estas piezas.

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
| POST | `/register` | anónimo | crea usuario (email, username, password ≥6) |

### User
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| GET | `/users/me` | JWT | perfil completo (avatar, privacidad) del usuario autenticado |
| PUT | `/users/{id}` | JWT | edita username y `privacy{...}`; el email es fijo tras el registro; solo uno mismo (403 si no) |
| PUT | `/users/me/avatar` | JWT | sube la foto de perfil (multipart `file`, máx. 5 MB); la recorta a 256×256 WebP y devuelve `avatarUrl` |
| DELETE | `/users/me/avatar` | JWT | borra la foto de perfil |
| GET | `/users/{id}` | JWT | perfil público: `{ id, username, avatarUrl, createdAt, relationship, counts{book,movie,series,total} }` |
| GET | `/users/{id}/avatar` | anónimo | sirve la foto de perfil (WebP 256×256) desde la BD |
| GET | `/users/search?username=` | JWT | busca usuario por username exacto (quita `@` inicial) |

### Media
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| GET | `/media/{id}` | JWT | ficha de un media: datos + tu progreso/reseña + todas las reseñas + nota media |

### Tracking (`Features/TrackingEvent/`)
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| POST | `/tracking/movies` | JWT | registra progreso de una película. `FindOrCreateAsync` deduplica el Media |
| POST | `/tracking/series` | JWT | registra progreso de un episodio (`Season`+`Episode` obligatorios) |
| POST | `/tracking/books` | JWT | registra progreso de un libro; si falta autor/portada dispara enriquecimiento OpenLibrary en background |
| PUT | `/tracking/{mediaId}` | JWT | corrige el progreso de un media que el usuario ya sigue, por porcentaje o por temporada y episodio (editar desde la biblioteca); 404 si no lo sigue |
| DELETE | `/tracking/{mediaId}` | JWT | borra todo el seguimiento de ese media (lo saca de tu biblioteca) |

Hay un único seguimiento por (usuario, media) y cada envío, automático o manual, sustituye el
progreso guardado (y, en series, la temporada y el episodio), así que puede bajar.

Las tres rutas `POST /tracking/{movies,series,books}` deduplican el `Media` por título,
comparándolo con `TitleEN` y `TitleES` (así un título traducido por TMDB también encaja), y para
movies/series lanzan enriquecimiento TMDB en background si falta portada, sinopsis o el título en el
otro idioma y el request trae `Language` ("es"/"en").

No hay endpoint para **leer** el seguimiento en crudo: para eso está `/library`, que devuelve una
fila por media.

### Library / Stats (`Features/Library/`, `Features/Stats/` — alimentan el home `/`)
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| GET | `/stats` | JWT | resumen anual del usuario: `?year=` (def. actual) → `{ year, total, byType{book,movie,series}, byMonth[12], reviewCount }` |
| GET | `/library` | JWT | biblioteca del usuario, **una fila por media** + `myRating`/`myReviewId`. Filtros `?type=Book\|Movie\|Series&status=in_progress\|finished&sort=recent\|rating&year=&limit=` |
| GET | `/reviews` | JWT | reseñas escritas por el usuario, con `media` anidado |
| GET | `/reviews/pending` | JWT | media terminada (progreso 100) que el usuario aún no ha reseñado |

### Friends & Activity (`Features/Friend/`, `Features/Activity/` — todo JWT)
| Método | Ruta | Qué hace |
|---|---|---|
| GET | `/friends` | `{ friends, incoming, outgoing }` (amigos aceptados + solicitudes) |
| POST | `/friends/requests` `{userId}` | envía solicitud a ese usuario (responde con `code` estable para mapear el error en el frontend) |
| PUT | `/friends/requests/{id}` | acepta una solicitud entrante |
| DELETE | `/friends/requests/{id}` | rechaza una solicitud entrante (borra la fila) |
| DELETE | `/friends/{userId}` | elimina amistad o cancela solicitud |
| GET | `/activity?scope=all\|mine\|friends&limit=` | feed derivado de TrackingEvent + Review: `started`/`finished`/`reviewed`. Filtra por la privacidad **por tipo de media** de cada usuario (`Share{Book,Movie,Series}{Progress,Reviews}`) |
| GET | `/activity?userId=N&limit=` | actividad de un solo usuario (para su perfil público); vacío salvo que seas tú o su amigo |

### Review
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| POST | `/reviews` | JWT | crea reseña (mediaId, rating 1–10, comment opcional) |
| PUT | `/reviews/{id}` | JWT | edita; solo el dueño (403 si no) |
| DELETE | `/reviews/{id}` | JWT | borra; solo el dueño (403 si no) |

**Validación de entrada**: `ValidationFilter` (IEndpointFilter) recorre los argumentos y valida sus
DataAnnotations; si falla devuelve `400 { message: "err1; err2" }`. El texto de esos mensajes está en
español cuando el error lo puede haber causado el usuario (contraseña corta, email inválido...); se deja
en inglés cuando es un fallo de estado/lógica que la UI nunca llega a mostrar tal cual (sesión no
encontrada, parámetro interno ausente...).

---

## 5. Modelo de datos (`Model/`, DbSets en `AppDbContext`)

| Entidad | Campos clave | Notas |
|---|---|---|
| **User** | Email, Username, PasswordHash?, GoogleId?, EmailCode?/Expiry?/FailedAttempts, AvatarUrl?, CreatedAt, 6 flags `Share{Book,Movie,Series}{Progress,Reviews}` | password/google opcionales → un user puede ser solo-Google. `Email` y `Username` son `citext`: sus índices únicos ignoran mayúsculas |
| **Media** | TitleEN?, TitleES?, Type, Length, Description?, PosterUrl?, Author?, Isbn?, SeasonEpisodeCounts?, FirstTrackedAt, UpdatedAt? | Tabla única para los tres tipos. `Type` es el enum `MediaType` (Movie, Series, Book), guardado como texto. Al menos uno de `TitleEN`/`TitleES`; el otro lo rellena TMDB. Check constraints: `Author` e `Isbn` solo en Book, `SeasonEpisodeCounts` solo en Series, `Type` limitado a los tres valores. Índices no únicos en `Isbn` (parcial), `TitleEN` y `TitleES` |
| **TrackingEvent** | UserId→, MediaId→, Progress? (0–100), Season?, Episode?, EpisodeTitle?, EventDate | `EventDate` = `DateTime.UtcNow` al crear/actualizar |
| **Review** | UserId→, MediaId→, Rating (1–10), Comment?, CreatedAt, UpdatedAt? | |
| **Friendship** | RequesterId→, AddresseeId→, Status, CreatedAt, RespondedAt? | `Status` es el enum `FriendshipStatus` (Pending, Accepted), guardado como texto. Una sola fila por pareja de usuarios en cualquier sentido: índice único sobre las columnas generadas `UserLowId`/`UserHighId` (`LEAST`/`GREATEST` de Requester y Addressee), que no existen en la entidad. Ambos FK cascade-delete |
| **RefreshToken** | UserId→, TokenHash, Device, CreatedAt, LastUsedAt, ExpiresAt, RevokedAt? | una fila por sesión/dispositivo; la rotación cambia el hash en la misma fila (el id no cambia), con ventana de gracia de 60s |

- **Cascade delete** configurado en `OnModelCreating`: borrar un `User` borra sus `Review`,
  `TrackingEvent`, `Friendship` y `RefreshToken`.
- **Dedup de Media** (`MediaService.FindOrCreateAsync`): prioridad ISBN → título+Author → título; el
  título se compara con `TitleEN` y `TitleES` (siempre dentro del mismo `Type`).
- **Enriquecimiento de libros** (`OpenLibraryService`): busca por ISBN o por título/autor (similitud de
  tokens, umbral 0.75 configurable) y rellena Author / PosterUrl / Isbn si faltan.
  Portadas: `covers.openlibrary.org`.
- **Enriquecimiento de películas/series** (`TmdbService`, requiere env `TMDB_API_KEY`): busca en TMDB por
  título y toma el primer resultado (los títulos llegan tal cual de Netflix), y rellena `PosterUrl`,
  `Length` (runtime, solo películas), `SeasonEpisodeCounts` (series) y el título en el otro idioma
  (`TitleEN`/`TitleES`, vía `append_to_response=translations`).
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

Hashing de contraseñas vía `PasswordHasher<User>` de `Microsoft.AspNetCore.Identity` (PBKDF2-HMAC-SHA512,
sal por hash, 210 000 iteraciones). Sin MediatR, sin FluentValidation, sin AutoMapper. DI a mano en
`Program.cs`. TMDB y Open Library se consumen con `HttpClient` a pelo, sin SDK.

---

## 7. Variables de entorno

Se leen de `../.env` (repo root).

| Variable | Uso |
|---|---|
| `POSTGRES_HOST/PORT/USER/PASSWORD/DB` | connection string (se construye a mano en `configureDatabase`) |
| `JWT_SECRET` / `JwtSettings__Secret` | firma del JWT |
| `GOOGLE_CLIENT_ID` | audience al validar el idToken de Google |
| `RESEND_API_KEY` | Bearer para la API de Resend (emails OTP) |
| `TMDB_API_KEY` | enriquecimiento de películas/series (portada, duración, título en el otro idioma). Sin ella, ese enriquecimiento simplemente no hace nada |

---

## 8. Tests (`../TaleTrackApp.Tests`)

- **xUnit** + `Microsoft.AspNetCore.Mvc.Testing`: todos son de integración, levantan la API entera en
  memoria (`WebApplicationFactory<Program>`) y la llaman por HTTP como un cliente. No hay tests
  unitarios del backend: con los servicios externos simulados, la integración llega a toda la lógica.
- `CustomWebApplicationFactory`: fuerza `Environment=Testing` (salta Npgsql y el rate limit), inyecta
  **SQLite in-memory** (con una conexión KeepAlive compartida por toda la colección) y fija
  `JwtSettings__Secret`, `TMDB_API_KEY` y `GOOGLE_CLIENT_ID` de test.
- **Servicios externos simulados** (`ExternalServiceFakes.cs`), compartidos por todos los tests y
  accesibles desde la factoría. Ningún test sale a la red:
  - `FakeTmdb` / `FakeOpenLibrary`: un título solo existe si el test lo añade (`Add`); `AddDown` hace
    que respondan 500; `FakeOpenLibrary.SearchesFor` cuenta las búsquedas.
  - `FakeResend`: guarda los correos enviados (destinatario, asunto, enlace y código), así que los
    tests usan el enlace o el código del correo como lo haría el usuario. Un destinatario que
    contenga `resend-down` recibe un 500.
  - `FakeGoogleIdTokenValidator`: sustituye la validación de Google; acepta los tokens hechos con
    `FakeGoogleIdTokenValidator.Token(googleId, email)`.
  - Los clientes tipados se registran con el nombre corto de la clase (`nameof(TmdbService)`), no con
    el completo: con el completo la sustitución no se aplica y se llama a la API real.
- Las caducidades (código de 10 min, enlaces de 1 h / 7 días) se prueban moviendo la fecha guardada
  en la BD (`UpdateActionTokenAsync`, `EmailCodeExpiry`), sin esperar.
- Un fichero por área (`*FlowTests.cs`), más `AuthorizationTests` (recorre todos los endpoints y
  comprueba que solo los de la lista blanca son anónimos), `ValidationTests`, `RateLimitTests` (levanta
  otra instancia con `RateLimiting:PermitLimit=5`), `MediaConstraintsTests` (check constraints de
  `Media` contra la BD), `UnhandledExceptionTests` y `OpenApiDocumentationTests`.
- Helpers: `CreateUserAsync` crea un usuario y devuelve un cliente autenticado; `ApiHelpers` tiene las
  llamadas que repiten muchos tests (biblioteca, ficha, reseñas, amistades, sesiones); `Eventually`
  espera a lo que el backend hace en segundo plano (correos, enriquecimiento).
- Correr: `dotnet test ../TaleTrackApp.Tests`

---

## 9. Cómo levantarlo

```bash
# Stack de desarrollo completo (hot-reload; el backend aplica las migraciones al arrancar):
docker compose -f docker-compose.dev.yml up -d
#   backend  -> http://localhost:8080  (Swagger en /swagger)
#   frontend -> http://localhost:8090
#   logs     -> http://localhost:9999  (dozzle)
```

Para meter un usuario de prueba con datos: `../scripts/seed-demo.ps1` (registra `demo@taletrack.dev` /
`demo1234` + tracking events de ejemplo).

---

## 10. Consumidores del backend

| Cliente | Cómo llama |
|---|---|
| `taletrack-frontend` (Next.js) | navegador → `/api/*` (rewrite a `backend:8080`). SSR usa `lib/api/server.ts` con la cookie `tt-token` |
| `taletrack.koplugin` (KOReader, Lua) | login por OTP (`/api/auth/request-code` + `/verify-code`), luego `POST /api/tracking/books` con `Authorization: Bearer` al terminar un libro |
| `taletrack-netflix-extension` | detecta reproducción en Netflix, extrae título/temporada/episodio/progreso/idioma y postea a `/api/tracking/movies` o `/api/tracking/series` cada ~15s (con throttle por % de cambio) vía el service worker |

> El koplugin y la extensión apuntan por defecto al servidor de producción hardcodeado
> (`https://taletrack.app`); para desarrollo local se sobreescribe (ver el README de cada uno).
