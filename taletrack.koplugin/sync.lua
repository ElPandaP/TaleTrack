-- Sends the queued book-progress entries to the backend when there's a network connection.
-- The queue itself is queue.lua; the session (and renewing it) is session.lua.

local NetworkMgr = require("ui/network/manager")
local logger = require("logger")

--- Outcome of sending one queued item:
--- "ok" (accepted), "auth" (the session is dead), "retry" (transient failure, keep it),
--- "drop" (rejected for good, discard it).
---@alias Sync.Result "ok"|"auth"|"retry"|"drop"

--- Options for `Sync.new`.
---@class Sync.Options
---@field api Api
---@field session Session
---@field queue Queue

--- Drains the offline queue to the backend. Sending is safe to repeat because the backend
--- keeps the latest progress it receives. A transient failure (no network, 5xx, 408, 429)
--- keeps the item and stops the drain; any other rejection discards it, since retrying would
--- give the same answer.
---@class Sync
---@field private api Api
---@field private session Session
---@field private queue Queue
local Sync = {}
---@private
Sync.__index = Sync

--- Creates the sync over the shared api, session and queue.
---@param opts Sync.Options
---@return Sync
function Sync.new(opts)
    local self = setmetatable({}, Sync)
    self.api = opts.api
    self.session = opts.session
    self.queue = opts.queue
    return self
end

--- Sends one item, renewing the session if needed.
---@param item Queue.Item
---@return Sync.Result
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

--- Drains the queue oldest-first and saves what is left. Does nothing when the queue is empty
--- or the device is offline. Stops at the first transient failure.
---@param on_status? fun(kind: "auth") Called at most once, with "auth", if the session is dead.
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
