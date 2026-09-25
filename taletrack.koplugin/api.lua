-- handles all HTTP communication with the TaleTrack backend
-- change SERVER_URL when the server moves

local ltn12 = require("ltn12")
local rapidjson = require("rapidjson")
local logger = require("logger")

-- socketutil lets us set short timeouts so a dead network fails in seconds
-- instead of hanging on the default (very long) socket timeout.
local socketutil_ok, socketutil = pcall(require, "socketutil")

local SERVER_URL = "https://taletrack.app"

local BLOCK_TIMEOUT = 3   -- seconds with no data before giving up
local TOTAL_TIMEOUT = 12  -- seconds for the whole request

local Api = {}

-- pick http or https based on the url, ssl.https might not be available on all devices
local function getHttpLib(url)
    if url:sub(1, 5) == "https" then
        local ok, https = pcall(require, "ssl.https")
        if ok then return https end
    end
    return require("socket.http")
end

-- Returns: status (HTTP code number) or nil on transport failure,
--          response (always a table: the parsed JSON body, or { success = false } when the body
--          is empty or isn't a JSON object; on a transport failure it carries the error text
--          in `message`)
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

function Api.requestCode(email, locale)
    local body = { Email = email }
    if locale and locale ~= "" then body.Locale = locale end
    return post("/api/auth/request-code", body)
end

function Api.verifyCode(email, code)
    return post("/api/auth/verify-code", { Email = email, Code = code })
end

-- Trades a refresh token for a fresh access + refresh pair (the backend rotates it).
function Api.refresh(refresh_token)
    return post("/api/auth/refresh", { RefreshToken = refresh_token })
end

-- Revokes this device's refresh token on the server.
function Api.logout(refresh_token)
    return post("/api/auth/logout", { RefreshToken = refresh_token })
end

-- item = { title, progress (0-100), pages?, author?, isbn? }
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
