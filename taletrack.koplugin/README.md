# TaleTrack — KOReader plugin

Lua plugin for [KOReader](https://github.com/koreader/koreader) that records what you read in your
TaleTrack account: your reading progress as you go, and the book as finished when you reach the end.

## Install

Download `taletrack.koplugin.zip` from the
[latest release](https://github.com/ElPandaP/TaleTrack/releases/latest) and unzip it into the
`koreader/plugins/` folder of your device, so that you end up with `plugins/taletrack.koplugin/`.
KOReader finds plugins by the `.koplugin` suffix of the folder name. Copying this folder from the
repository works the same way. Restart KOReader and sign in from *Tools → TaleTrack*.

## Structure

```
_meta.lua          # plugin name/description/version, read by KOReader
main.lua           # entry point: reader events, menu and sign-in flow
book.lua           # the open book: progress, end margin, title/key (uses bookmeta.lua)
bookmeta.lua       # author and ISBN taken from the document metadata
session.lua        # the user's tokens (access + refresh) and their renewal
queue.lua          # offline progress queue (one entry per book, persisted)
sync.lua           # sends the queue to the backend when there is network
api.lua            # HTTP communication with the backend (one-time code sign-in, refresh, track book)
login_dialog.lua   # two-step sign-in dialog: email → 6-digit code
i18n.lua           # Spanish/English strings depending on KOReader's language
logo.png           # project logo (not used by KOReader)
```

`main.lua` only wires things together: it receives KOReader's events and decides when to do what;
reading the book, storing the session, keeping the queue and talking to the backend belong to the
other modules. KOReader creates one plugin instance for every file browser and reader it opens,
while the session and the queue must exist only once per process, so `main.lua` creates them the
first time and every instance shares them (modules are loaded with `dofile` from the plugin path,
not with `require`, so names like `api` or `queue` do not clash with KOReader's own).

## How it tracks

**Progress while reading.** `main.lua` listens to `onPageUpdate` and, at most once every 2 minutes
while you turn pages, asks `book.lua` for the reading percentage
(`ReaderPaging`/`ReaderRolling:getLastPercent()`) and queues it. It also queues on reaching the end
of the book (`onEndOfBook` → 100), when the document is closed and when the device suspends. Only a
progress different from the last queued one is queued. The backend always keeps the last progress
received, so going back to earlier pages also lowers the stored progress.

**Finished book.** There is no manual marking: a book counts as finished (progress 100) on entering
the end margin, which is 5 % of the pages with a maximum of 25, because the last pages are usually
acknowledgements, notes or ads that nobody reads. It is the equivalent of what the Netflix extension
does when the credits start. Entering the margin is sent right away, without waiting for the 2
minutes. Going back afterwards and reading outside the margin unmarks it, because the next update
carries the real progress. The current page comes from `ReaderUI:getCurrentPage()`, which knows it
for both reflowable documents (EPUB) and fixed-layout ones (PDF, DjVu).

**Offline queue (`queue.lua` and `sync.lua`).** Each entry is a book with its latest progress;
queuing the same book again **replaces** the entry (only the latest percentage matters), so 8
updates in a row end up as a single `POST`. The queue is persisted in `taletrack_queue.lua` (up to
100 entries and 4 weeks) and `sync.lua` flushes it when there is network: when the reader opens, on
reconnecting (`onNetworkConnected`), on resume, when the document is closed, after signing in and
after every update. The backend is idempotent (it keeps the last progress received), so sending
again is harmless. Without a connection, `flush` does nothing and the queue waits. A transient
failure (no network, 5xx, 408, 429) keeps the item in the queue and stops the flush; other 4xx
responses discard it because retrying would not change the result. Signing out empties the queue,
so pending progress never ends up in another account.

**Which book it is (`book.lua`).** The title comes from the document metadata; failing that, from the
file name without its extension, and only if that cannot be read either, `"Desconocido"`. The
queue's deduplication key is the document's `partial_md5_checksum`, or the title if there is none.
The page count is only sent for fixed-layout documents: in an EPUB it depends on the font and the
margins, so it says nothing about the book.

**Author and ISBN (`bookmeta.lua`).** They are read from `doc:getProps()` (`authors` and
`identifiers`, which KOReader extracts from the EPUB's OPF) and sent when present, so the backend
deduplicates by ISBN or by title + author instead of by title alone. Authors are joined with `, `.
Of the identifiers, only a valid ISBN-10/13 (checksum included) is accepted (`urn:isbn:`, dashes and
spaces are normalised), because the backend limits `Isbn` to 13 characters and many EPUBs only carry
a UUID. If the book has no valid ISBN or author, that field is simply not sent.

## Auth

Sign-in with a **one-time code** (`login_dialog.lua`, two steps): you enter your email
(`TaleTrack:requestCode` → `Api.requestCode` → `POST /api/auth/request-code`), then the 6-digit code
that arrives by email (`TaleTrack:verifyCode` → `Api.verifyCode` → `POST /api/auth/verify-code`).
**Both the access token and the refresh token** are stored in `LuaSettings`
(`DataStorage:getSettingsDir()/TaleTrack.lua`), managed by `session.lua`.

The access token lasts 5 minutes; its expiry is not computed locally (e-reader clocks are not
reliable). When a progress `POST` returns **401**, `session.lua` exchanges the refresh token for a
new pair (`POST /api/auth/refresh`, which rotates the refresh token, so the new one is stored) and
retries. Only if the server rejects the refresh token are the tokens deleted and the user asked to
sign in again (a single notice, not one per queued item); a network or server failure while renewing
keeps both the session and the queue.

**Network and screen.** KOReader makes network calls on the same thread that draws the screen, so a
slow request freezes it. That is why requesting the code, verifying it and signing out show a
message ("Sending code…") while they wait, and sign-in waits until there is a connection
(`NetworkMgr:runWhenOnline`, which offers to turn on Wi-Fi if needed). If the email or the code
fails, the dialog reopens with the email filled in, and the reason is explained from the response:
no connection, too many attempts (429), server error (5xx) or rejection. Sending progress, on the
other hand, is silent and uses short timeouts (3 s without data, 12 s in total).

## Server

`api.lua` has `SERVER_URL` **hardcoded** to `https://taletrack.app` (the production server). To point
it at a local backend, edit that constant by hand and copy the plugin to the device again. It
detects http/https automatically and uses `socket.http` or `ssl.https` accordingly (`ssl.https` is
not available on every device). Timeouts are shortened with `socketutil` so a dead network fails in
seconds instead of hanging, and they are always restored, even if the request fails, because they
are global to KOReader. Requests carry KOReader's `User-Agent`. Calls always return a table as the
response: if the body is not JSON (an error page from a proxy, for example) it is never shown to
the user.

## Testing

There are no automated tests. The plugin was tested by hand in KOReader's desktop emulator, which
keeps the KOReader console visible while using the plugin, so warnings and Lua stack traces show up
the moment they happen.
