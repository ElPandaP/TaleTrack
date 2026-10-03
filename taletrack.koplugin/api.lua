-- HTTP client for the TaleTrack backend. Every call is a JSON POST to SERVER_URL, the only
-- place that says which server the plugin talks to.

local ltn12 = require("ltn12")
local rapidjson = require("rapidjson")
local logger = require("logger")

-- socketutil lets us set short timeouts so a dead network fails in seconds
-- instead of hanging on the default (very long) socket timeout.
local socketutil_ok, socketutil = pcall(require, "socketutil")

local SERVER_URL = "https://taletrack.app"

local BLOCK_TIMEOUT = 3   -- seconds with no data before giving up
local TOTAL_TIMEOUT = 12  -- seconds for the whole request

--- What every backend call answers with: the parsed JSON body, or `{ success = false }` when
--- the body is empty or isn't a JSON object. Only the fields the plugin reads are listed.
---@class Api.Response
---@field success boolean True when the backend accepted the request.
---@field message? string Human-readable text from the backend, or the transport error when the request never got an answer.
---@field token? string Access token (JWT), on a successful sign-in or refresh.
---@field refreshToken? string Refresh token, on a successful sign-in or refresh.

--- HTTP communication with the TaleTrack backend: email-code sign-in, token refresh and
--- sign-out, and reporting reading progress. Each call returns the HTTP status (nil when the
--- request could not be made at all) and an `Api.Response`. Requests use short timeouts so a
--- dead network fails in seconds.
---@class Api
local Api = {}

--- Picks the HTTP library for the url: `ssl.https` for https urls when the device has it,
--- `socket.http` otherwise. Both are LuaSocket-style modules with a `request` function.
---@param url string
local function getHttpLib(url)
    if url:sub(1, 5) == "https" then
        local ok, https = pcall(require, "ssl.https")
        if ok then return https end
    end
    return require("socket.http")
end

--- Sends `body` as JSON to `path` on the server, with the bearer token when one is given.
---@param path string Path on the server, starting with `/api/`.
---@param body table Request body, encoded as JSON.
---@param token? string Access token for endpoints that need a signed-in user.
---@return integer? status HTTP status, or nil on a transport failure.
---@return Api.Response response Always a table; on a transport failure its `message` holds the error text.
local function post(path, body, token)
    local url = SERVER_URL .. path
    local body_json = rapidjson.encode(body)
    local response_chunks = {}

    local headers = {
        ["Content-Type"] = "application/json",
        ["Content-Length"] = tostring(#body_json),
        ["Accept"] = "application/json",
        ["User-Agent"] = (socketutil_ok and socketutil.USER_AGENT) or "KOReader",
    }
    if token then
        headers["Authorization"] = "Bearer " .. token
    end

    -- These timeouts are global to KOReader's sockets, so they are restored even if the request raises.
    if socketutil_ok then socketutil:set_timeout(BLOCK_TIMEOUT, TOTAL_TIMEOUT) end

    local lib = getHttpLib(url)
    local called, ok, status = pcall(lib.request, {
        url = url,
        method = "POST",
        headers = headers,
        source = ltn12.source.string(body_json),
        sink = ltn12.sink.table(response_chunks),
    })

    if socketutil_ok then socketutil:reset_timeout() end

    if not called then
        logger.warn("TaleTrack: request raised:", ok)
        return nil, { success = false, message = tostring(ok) }
    end
    if not ok then
        logger.warn("TaleTrack: request failed:", status)
        return nil, { success = false, message = tostring(status) }
    end

    local response_str = table.concat(response_chunks)
    local parse_ok, response = pcall(rapidjson.decode, response_str)
    if not parse_ok or type(response) ~= "table" then
        if response_str ~= "" then
            logger.warn("TaleTrack: response is not a JSON object, HTTP", status, response_str:sub(1, 200))
        end
        response = { success = false }
    end

    return status, response
end

--- Asks the backend to email a one-time sign-in code to `email`. The answer is the same
--- whether or not the account exists.
---@param email string Address to send the code to.
---@param locale? string Language of the email ("en" or "es").
---@return integer? status HTTP status, or nil on a transport failure.
---@return Api.Response response
function Api.requestCode(email, locale)
    local body = { Email = email }
    if locale and locale ~= "" then body.Locale = locale end
    return post("/api/auth/request-code", body)
end

--- Redeems the emailed code for a session. On success the response carries `token` and
--- `refreshToken`.
---@param email string Address the code was sent to.
---@param code string The 6-digit code.
---@return integer? status HTTP status (401 for a wrong or expired code), or nil on a transport failure.
---@return Api.Response response
function Api.verifyCode(email, code)
    return post("/api/auth/verify-code", { Email = email, Code = code })
end

--- Trades a refresh token for a new access and refresh token pair. The backend rotates the
--- refresh token, so the one returned replaces the one sent.
---@param refresh_token string The current refresh token.
---@return integer? status HTTP status (401 when the refresh token is no longer valid), or nil on a transport failure.
---@return Api.Response response
function Api.refresh(refresh_token)
    return post("/api/auth/refresh", { RefreshToken = refresh_token })
end

--- Revokes this device's refresh token on the server.
---@param refresh_token string The refresh token of the session to end.
---@return integer? status HTTP status, or nil on a transport failure.
---@return Api.Response response
function Api.logout(refresh_token)
    return post("/api/auth/logout", { RefreshToken = refresh_token })
end

--- Reports the reading progress of a book. The backend finds the book (by ISBN, then title and
--- author, then title), creates it if unknown, and stores this progress as the latest one.
--- Author and ISBN are only sent when present.
---@param token string Access token of the signed-in user.
---@param item Queue.Item The book and its progress.
---@return integer? status HTTP status, or nil on a transport failure.
---@return Api.Response response
function Api.trackBook(token, item)
    local body = {
        Title    = item.title,
        Pages    = item.pages,
        Progress = item.progress,
    }
    if item.author and item.author ~= "" then body.Author = item.author end
    if item.isbn   and item.isbn   ~= "" then body.Isbn   = item.isbn   end
    return post("/api/tracking/books", body, token)
end

return Api
