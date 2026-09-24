# TaleTrack — Extensión de Netflix

Extensión de Chrome (Manifest V3) que detecta lo que estás viendo en Netflix y lo trackea
automáticamente en tu cuenta de TaleTrack — sin tener que anotarlo a mano.

## Cómo funciona

Netflix no expone una API pública, así que la extensión combina varias fuentes con fallbacks, todas
en `src/netflix-extract.ts`:

- El DOM de la página (título, temporada y episodio, tipo) con varios selectores por campo, porque
  Netflix cambia sus clases con frecuencia.
- El **player de Netflix** (`window.netflix.appContext...`), leído desde un script inyectado en el
  `MAIN` world (`src/injected.ts`) — un content script normal no puede tocar el JS de la página,
  solo su DOM.
- La caché **Falcor** de Netflix (`window.netflix.falcorCache`), para season/episode/tipo cuando el
  DOM no los tiene.
- `<html lang>` para saber si la interfaz está en español o inglés — se lo mandamos al backend para
  que pueda enriquecer con TMDB (portada, sinopsis, duración, título en el otro idioma) sin depender de
  que el título coincida palabra por palabra si cambias el idioma de Netflix a mitad de temporada.

`src/content.ts` es la parte que decide **cuándo** llamar a esa extracción: sondea cada 15s mientras
hay un `<video>` reproduciéndose, y además reacciona al instante al evento `loadedmetadata` (la barra
de título de Netflix solo está visible los primeros segundos, así que si la perdemos reintenta hasta
10 veces, una por segundo, antes de rendirse hasta el siguiente sondeo). Una vez resuelto el título
para un vídeo, se queda cacheado y no se vuelve a leer del DOM mientras sigas en el mismo
episodio/película. Un título que viene de los metadatos del reproductor se usa tal cual; solo el
texto raspado del DOM se limpia de marcas de episodio, y solo en series, porque los dos puntos o el
guion de una película (`Mission: Impossible`) forman parte de su nombre.

El momento en que algo cuenta como visto lo decide `content.ts`: al entrar en el margen previo a los
créditos (el `creditsOffset` que da Netflix, que se vuelve a pedir en cada sondeo hasta que aparece)
el progreso pasa a 100 %, sin esperar a que el vídeo llegue a su final real.

El progreso se manda a `POST /api/tracking/movies` o `/api/tracking/series` vía el service worker
(`src/background.ts`), que throttlea los envíos para no saturar al backend: el primero de cada vídeo,
después solo cada 2 % de avance hacia delante o al cruzar el 95 %. Se salta el filtro (envío forzado)
al cerrar u ocultar la pestaña, al terminar el vídeo, al cambiar de vídeo y al salir del reproductor.
En los dos últimos casos el `<video>` ya no se puede leer, así que se envía la última lectura que
`content.ts` guardó. No hay cola offline: si un envío falla, el siguiente sondeo informa el estado
actual.

## Estructura

```
src/
├── content.ts           # bucle de auto-tracking: sondeo, título, créditos, envío del progreso
├── netflix-extract.ts    # toda la lógica de "qué hay en esta página de Netflix"
├── injected.ts           # se ejecuta en el MAIN world de la página (acceso a window.netflix)
├── background.ts         # service worker: throttle de envíos, refresco de token
├── auth.ts               # ciclo de vida del JWT/refresh token (chrome.storage.local)
├── popup.ts               # UI del popup: estado de sesión, iniciar y cerrar sesión
├── config.ts              # BACKEND_URL / FRONTEND_URL / DEBUG (build time) y debug()
└── types.ts               # tipos compartidos entre los distintos contextos
tests/
└── netflix-extract.test.ts  # limpieza de títulos y lectura de marcas de episodio (bun test)
```

## Auth

Al pulsar "Iniciar sesión" en el popup, se abre una pestaña normal (y el popup se cierra: no espera
el resultado, al volver a abrirlo muestra la sesión) en `FRONTEND_URL/extension-auth`
(`chrome.tabs.create`, sin `chrome.identity` — esa API queda detrás de un ajuste de "permitir
inicio de sesión de Google" en Brave que no tiene nada que ver con este flujo, así que se evita
por completo). Esa página, ya con tu sesión web, te pide confirmar, pide un par de tokens propio
al backend (`POST /api/auth/extension-grant`, que aparece como la sesión **"Netflix extension"**
en tu perfil) y se lo pasa a la extensión con `chrome.runtime.sendMessage(EXTENSION_ID, ...)`
— permitido porque el origen de `FRONTEND_URL` está declarado en `externally_connectable` en
`manifest.json` (generado en build time a partir de `TT_FRONTEND_URL`, nunca un wildcard).
`background.ts` vuelve a comprobar el origen del remitente y la forma del mensaje antes de guardar
nada. Los tokens se guardan en `chrome.storage.local` y se refrescan solos.

El campo `"key"` de `manifest.json` fija el id de la extensión (`kbhjoofgffidokbnlklekelkpdhcllih`)
para que no cambie entre recargas — el frontend necesita conocerlo de antemano
(`NEXT_PUBLIC_EXTENSION_ID`, con ese mismo valor por defecto) para poder enviarle el mensaje.

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

Con `TT_DEBUG=1` la extensión escribe trazas detalladas en la consola (`debug()` de `config.ts`); sin
él no escribe nada salvo errores. Esto genera `dist/` con los scripts, `popup.html` y el
`manifest.json`, al que el build le añade `externally_connectable` y `host_permissions` a partir de
las URLs elegidas. Para cargarla en
Chrome: `chrome://extensions` → activar "Modo de desarrollador" → "Cargar extensión sin empaquetar" →
seleccionar la carpeta `dist/`. Tras recompilar hay que darle a recargar en `chrome://extensions`.

Los `host_permissions` del build son siempre `netflix.com` y el origen de `TT_BACKEND_URL`, así que
una build de producción no pide acceso a `localhost` y una local acepta el puerto que le pases.
Todavía no hay iconos (`icons` en el manifiesto y los PNG en `public/`), y la Chrome Web Store los exige.

## Tests

```bash
bun test
```

Cubre la limpieza de títulos y la lectura de marcas de episodio (`tests/netflix-extract.test.ts`); lo
demás depende del DOM y del reproductor de Netflix y solo se puede probar a mano.
