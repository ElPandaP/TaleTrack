# TaleTrack — Extensión de Netflix

Extensión de Chrome (Manifest V3) que detecta lo que estás viendo en Netflix y lo trackea
automáticamente en tu cuenta de TaleTrack — sin tener que anotarlo a mano.

## Cómo funciona

Netflix no expone una API pública, así que la extensión combina varias fuentes con fallbacks, todas
en `src/netflix-extract.ts`:

- El DOM de la página (título, año, géneros, sinopsis...) con varios selectores por campo, porque
  Netflix cambia sus clases con frecuencia.
- El **player de Netflix** (`window.netflix.appContext...`), leído desde un script inyectado en el
  `MAIN` world (`src/injected.ts`) — un content script normal no puede tocar el JS de la página,
  solo su DOM.
- La caché **Falcor** de Netflix (`window.netflix.falcorCache`), para season/episode/tipo cuando el
  DOM no los tiene.
- `<html lang>` para saber si la interfaz está en español o inglés — se lo mandamos al backend para
  que pueda enriquecer con TMDB (portada, duración, título en el otro idioma) sin depender de que el
  título coincida palabra por palabra si cambias el idioma de Netflix a mitad de temporada.

`src/content.ts` es la parte que decide **cuándo** llamar a esa extracción: sondea cada 15s mientras
hay un `<video>` reproduciéndose, y además reacciona al instante al evento `loadedmetadata` (la barra
de título de Netflix solo está visible los primeros segundos, así que si la perdemos reintenta unas
cuantas veces en los siguientes ~7s antes de rendirse). Una vez resuelto el título para un vídeo, se
queda cacheado y no se vuelve a leer del DOM mientras sigas en el mismo episodio/película.

El progreso se manda a `POST /api/tracking/movies` o `/api/tracking/series` vía el service worker
(`src/background.ts`), que throttlea los envíos (solo cada 5% de avance, o al cruzar el 95%, o al
cerrar/cambiar de pestaña) para no saturar al backend.

## Estructura

```
src/
├── content.ts           # bucle de auto-tracking + listener de mensajes del popup
├── netflix-extract.ts    # toda la lógica de "qué hay en esta página de Netflix"
├── injected.ts           # se ejecuta en el MAIN world de la página (acceso a window.netflix)
├── background.ts         # service worker: throttle de envíos, refresco de token
├── auth.ts               # ciclo de vida del JWT/refresh token (chrome.storage.local)
├── popup.ts               # UI del popup: estado de sesión + panel de depuración manual
├── config.ts              # BACKEND_URL / FRONTEND_URL, inyectadas en build time
└── types.ts               # tipos compartidos entre los distintos contextos
```

## Auth

Al pulsar "Iniciar sesión" en el popup, se abre `FRONTEND_URL/extension-auth` con
`chrome.identity.launchWebAuthFlow` — esa página del frontend, ya con tu sesión web, te pide
confirmar y le devuelve a la extensión un par de tokens propio (vía `POST /api/auth/extension-grant`,
que aparece como la sesión **"Netflix extension"** en tu perfil). Los tokens se guardan en
`chrome.storage.local` y se refrescan solos.

## Compilar

```bash
bun install
bun build.ts
```

Por defecto apunta al servidor de producción (`https://taletrack.app`, hardcodeado en `build.ts` y
`src/config.ts` como fallback). Para desarrollo local:

```bash
TT_BACKEND_URL=http://localhost:8080 TT_FRONTEND_URL=http://localhost:8090 bun build.ts
```

Esto genera `dist/` con los scripts y `manifest.json`/`popup.html` copiados tal cual. Para cargarla en
Chrome: `chrome://extensions` → activar "Modo de desarrollador" → "Cargar extensión sin empaquetar" →
seleccionar la carpeta `dist/`. Tras recompilar hay que darle a recargar en `chrome://extensions`.

`manifest.json` tiene `host_permissions` fijas para `netflix.com`, la IP de producción, y
`localhost:8080`/`:8090` (backend/frontend en local) — si cambias de puerto en local hay que añadirlo ahí.

## Popup manual (depuración)

Además del auto-tracking, el popup tiene un botón "Extraer datos" que llama a `extractNetflixData()`
a demanda y muestra el resultado (con un `<details>Ver JSON</details>` para el objeto completo) — útil
para comprobar qué está leyendo la extensión en una página concreta sin esperar al siguiente tick.
