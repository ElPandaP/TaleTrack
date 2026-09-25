# TaleTrack — Plugin de KOReader

Plugin en Lua para [KOReader](https://github.com/koreader/koreader) que registra en tu cuenta de
TaleTrack lo que lees: el progreso de lectura mientras avanzas y el libro como terminado al
acabarlo.

## Instalación

Copia esta carpeta dentro de `koreader/plugins/` en tu dispositivo, con el nombre
`taletrack.koplugin` — KOReader detecta los plugins por el sufijo `.koplugin` del nombre de carpeta.

## Estructura

```
_meta.lua          # nombre/descripción/versión del plugin, la lee KOReader
main.lua            # punto de entrada: eventos del lector, menú y flujo de login
book.lua            # el libro abierto: progreso, margen final, título/clave (usa bookmeta.lua)
bookmeta.lua        # autor e ISBN sacados de los metadatos del documento
session.lua         # tokens del usuario (access + refresh) y su renovación
queue.lua           # cola offline de progreso (una entrada por libro, persistida)
sync.lua            # envía la cola al backend cuando hay red
api.lua             # comunicación HTTP con el backend (login OTP, refresh, trackear libro)
login_dialog.lua    # diálogo de login en dos pasos: email → código de 6 dígitos
i18n.lua            # textos ES/EN según el idioma configurado en KOReader
logo.png            # logo del proyecto (no lo usa KOReader)
```

`main.lua` solo cablea: recibe los eventos de KOReader y decide cuándo hacer qué; leer el libro,
guardar la sesión, mantener la cola y hablar con el backend son cosas de los demás módulos.
KOReader crea una instancia del plugin por cada explorador de archivos y cada lector que abre, y
la sesión y la cola deben existir una sola vez por proceso, así que `main.lua` las crea la primera
vez y todas las instancias comparten esas mismas (los módulos se cargan con `dofile` desde la ruta
del plugin, no con `require`, para que nombres como `api` o `queue` no choquen con los de KOReader).

## Cómo trackea

**Progreso mientras lees.** `main.lua` escucha `onPageUpdate` y, como mucho una vez cada
2 minutos mientras pasas páginas, pide a `book.lua` el porcentaje de lectura
(`ReaderPaging`/`ReaderRolling:getLastPercent()`) y lo encola. También encola al llegar al
final del libro (`onEndOfBook` → 100), al cerrar el documento y al suspender. Solo se encola un
progreso mayor que el último encolado: el backend nunca lo baja, así que releer páginas anteriores
no genera envíos.

**Libro terminado.** No hay marcado manual: un libro cuenta como terminado (progreso 100) al
entrar en el margen final, que es el 5% de las páginas con un máximo de 25, porque las últimas
páginas suelen ser agradecimientos, notas o publicidad que nadie lee. Es el equivalente a lo que
hace la extensión de Netflix al llegar a los créditos. Entrar en el margen se envía al momento,
sin esperar a los 2 minutos. El progreso nunca baja en el backend, así que volver atrás después no
lo desmarca. La página actual se pide a `ReaderUI:getCurrentPage()`, que la conoce tanto en los
documentos reflowables (EPUB) como en los de maquetación fija (PDF, DjVu).

**Cola offline (`queue.lua` y `sync.lua`).** Cada entrada es un libro con su último progreso;
encolar de nuevo el mismo libro **sustituye** la entrada (nos quedamos con el porcentaje más
alto), así que 8 actualizaciones seguidas se resuelven con un solo `POST`. La cola se persiste en
`taletrack_queue.lua` (máximo 100 entradas y 4 semanas) y `sync.lua` la vacía cuando hay red: al
abrir el lector, al reconectar (`onNetworkConnected`), al reanudar y tras cada envío. El backend es
idempotente y el progreso solo sube (`Math.Max`), así que reenviar es inofensivo. Si no hay
conexión, `flush` no hace nada y la cola espera. Un fallo transitorio (sin red, 5xx, 408, 429)
deja el elemento en la cola y detiene el vaciado; otros 4xx lo descartan porque reintentar no
cambiaría el resultado. Al cerrar sesión la cola se vacía, para que el progreso pendiente no acabe
en otra cuenta.

**Qué libro es (`book.lua`).** El título sale de los metadatos del documento; si no tiene, del
nombre del fichero sin extensión, y solo si tampoco se puede leer, `"Desconocido"`. La clave de
deduplicación de la cola es el `partial_md5_checksum` del documento, o el título si no está. El
número de páginas solo se envía en documentos de maquetación fija: en un EPUB depende de la fuente
y de los márgenes, así que no dice nada del libro.

**Autor e ISBN (`bookmeta.lua`).** Se leen de `doc:getProps()` (`authors` e `identifiers`, que
KOReader extrae del OPF del EPUB) y se envían si existen, para que el backend deduplique por ISBN
o por título + autor en vez de solo por título. Los autores se unen con `, `. De los
identificadores solo se acepta uno que sea un ISBN-10/13 con checksum válido (`urn:isbn:`, guiones
y espacios se normalizan), porque el backend limita `Isbn` a 13 caracteres y muchos EPUB traen
solo un UUID. Si el libro no trae ISBN válido o autor, ese campo simplemente no se envía.

## Auth

Login por **código OTP** (`login_dialog.lua`, dos pasos): pides el email
(`TaleTrack:requestCode` → `Api.requestCode` → `POST /api/auth/request-code`), introduces el código de
6 dígitos que llega por email (`TaleTrack:verifyCode` → `Api.verifyCode` → `POST /api/auth/verify-code`).
Se guardan **el access token y el refresh token** en `LuaSettings`
(`DataStorage:getSettingsDir()/TaleTrack.lua`), que gestiona `session.lua`.

El access token dura ~1 h; no se calcula la caducidad en local (el reloj de los e-readers no es
fiable). Cuando un `POST` de progreso devuelve **401**, `session.lua` canjea el refresh token por
un par nuevo (`POST /api/auth/refresh`, que rota el refresh token — se guarda el nuevo) y
reintenta. Solo si el servidor rechaza el refresh token se borran los tokens y se pide iniciar
sesión de nuevo (un único aviso, no uno por cada elemento de la cola); un fallo de red o del
servidor al renovar conserva la sesión y la cola.

**Red y pantalla.** KOReader hace las llamadas de red en el mismo hilo que dibuja la pantalla, así
que una petición lenta la congela. Por eso pedir el código, verificarlo y cerrar sesión muestran un
mensaje ("Enviando código…") mientras esperan, y el login espera a que haya conexión
(`NetworkMgr:runWhenOnline`, que pide activar el wifi si hace falta). Si el correo o el código
fallan, el diálogo vuelve a abrirse con el correo rellenado, y el motivo se explica según la
respuesta: sin conexión, demasiados intentos (429), error del servidor (5xx) o rechazo. El envío
de progreso, en cambio, es silencioso y con tiempos cortos (3 s sin datos, 12 s en total).

## Servidor

`api.lua` tiene `SERVER_URL` **hardcodeado** a `https://taletrack.app` (la VM de producción) — para
apuntar a un backend local hay que editar esa constante a mano y volver a copiar el plugin al
dispositivo. Detecta http/https automáticamente y usa `socket.http` o `ssl.https` según toque
(`ssl.https` no está disponible en todos los dispositivos). Los tiempos de espera se acortan con
`socketutil` para que una red caída falle en segundos en vez de colgarse, y se restauran siempre,
incluso si la petición falla, porque son globales de KOReader. Las peticiones llevan el
`User-Agent` de KOReader. Las llamadas devuelven siempre una tabla como respuesta: si el cuerpo no
es JSON (una página de error de un proxy, por ejemplo) no se enseña al usuario.
