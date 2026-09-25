-- Sends the queued book-progress entries to the backend when there's a network connection.
-- The queue itself is queue.lua; the session (and renewing it) is session.lua.

local NetworkMgr = require("ui/network/manager")
local logger = require("logger")

local Sync = {}
Sync.__index = Sync

-- opts = { api, session, queue }
function Sync.new(opts)
    local self = setmetatable({}, Sync)
    self.api = opts.api
    self.session = opts.session
    self.queue = opts.queue
    return self
end

-- Sends one item. Returns "ok" | "auth" | "retry" | "drop".
function Sync:send(item)
    local status, response = self.session:call(function(access)
        return self.api.trackBook(access, item)
    end)

    if status == 200 and response and response.success then
        return "ok"
    elseif status == 401 then
        return "auth" -- no session, or the server rejected the renewal: the session is dead
    elseif status == nil or status >= 500 or status == 408 or status == 429 then
        return "retry" -- transport failure, server hiccup or rate limit: try again later
    else
        logger.warn("TaleTrack: dropping queued item, HTTP", status,
            response and response.message)
        return "drop" -- other 4xx: retrying won't change the outcome
    end
end

-- Drains the queue oldest-first. Stops at the first transient failure.
-- on_status("auth") is called at most once if the session is dead.
function Sync:flush(on_status)
    if self.queue:isEmpty() then return end
    if not NetworkMgr:isConnected() then return end

    local kept, stop, auth_failed = {}, false, false

    for _, item in ipairs(self.queue:list()) do
        if stop then
            kept[#kept + 1] = item
        else
            local result = self:send(item)
            if result == "retry" then
                kept[#kept + 1] = item
                stop = true
            elseif result == "auth" then
                kept[#kept + 1] = item
                auth_failed = true
                stop = true
            end -- "ok" and "drop" leave the queue
        end
    end

    self.queue:replace(kept)
    self.queue:persist()

    if auth_failed and on_status then on_status("auth") end
end

return Sync
