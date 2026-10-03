-- Offline queue for book-progress syncs.
-- Holds at most one entry per book (its latest progress). Persisted to its own file so
-- writing it is cheap and never touches auth state. Sending it is sync.lua's job.

local DataStorage = require("datastorage")
local LuaSettings = require("luasettings")

local MAX_ITEMS = 100
local MAX_AGE = 4 * 7 * 24 * 60 * 60 -- 4 weeks, in seconds

--- One pending progress update: a book and the latest progress read on it.
---@class Queue.Item
---@field key string Identifies the book in the queue (see `Book.Info.key`).
---@field title string Book title.
---@field progress integer Percent read, from 1 to 100.
---@field pages? integer Page count, only for fixed-layout documents.
---@field author? string Authors joined with ", ".
---@field isbn? string Bare ISBN-10 or ISBN-13.
---@field ts? integer When it was queued (epoch seconds), or nil if the device clock was not trustworthy.

--- Offline queue of progress updates, kept in `taletrack_queue.lua` in KOReader's settings
--- directory. It holds at most one entry per book, so queueing a book again replaces its entry
--- with the latest progress. Entries older than 4 weeks are discarded, and beyond 100 entries
--- the oldest ones go first. Changes are written to disk on `persist`.
---@class Queue
---@field private store table The LuaSettings file the entries are saved in.
---@field private items Queue.Item[] The entries, oldest first.
---@field private dirty boolean Whether there are changes not yet written to disk.
local Queue = {}
---@private
Queue.__index = Queue

--- Epoch time, or nil if the device clock looks bogus (e-reader RTCs drift badly).
---@return integer?
local function sane_now()
    local t = os.time()
    if type(t) == "number" and t > 1600000000 then return t end
    return nil
end

--- Opens the queue, loading the entries saved on disk.
---@return Queue
function Queue.new()
    local self = setmetatable({}, Queue)
    self.store = LuaSettings:open(DataStorage:getSettingsDir() .. "/taletrack_queue.lua")
    self.items = self.store:readSetting("items") or {}
    self.dirty = false
    return self
end

--- Adds the latest progress of a book, replacing any entry the book already had and keeping
--- the author and ISBN that entry carried when the new one lacks them. Items without key, title
--- or a progress of at least 1 are ignored.
---@param item Queue.Item The update; it is stored as given, stamped with the current time.
function Queue:enqueue(item)
    if not item.key or not item.title then return end
    if not item.progress or item.progress < 1 then return end

    local kept, prev_item = {}, nil
    for _, it in ipairs(self.items) do
        if it.key == item.key then
            prev_item = it
        else
            kept[#kept + 1] = it
        end
    end

    if prev_item then
        -- Don't lose metadata a previous enqueue had and this one lacks.
        item.author = item.author or prev_item.author
        item.isbn = item.isbn or prev_item.isbn
    end
    item.ts = sane_now()
    kept[#kept + 1] = item
    self.items = kept
    self:prune()
    self.dirty = true
end

--- Drops entries older than 4 weeks (only when the clock can be trusted) and the oldest ones
--- beyond 100.
---@private
function Queue:prune()
    local now = sane_now()
    if now then
        local cutoff = now - MAX_AGE
        local kept = {}
        for _, it in ipairs(self.items) do
            if not it.ts or it.ts >= cutoff then kept[#kept + 1] = it end
        end
        if #kept ~= #self.items then self.dirty = true end
        self.items = kept
    end
    while #self.items > MAX_ITEMS do
        table.remove(self.items, 1)
        self.dirty = true
    end
end

--- Whether there is nothing waiting to be sent.
---@return boolean
function Queue:isEmpty()
    return #self.items == 0
end

--- The entries, oldest first. Read only: change the queue through the methods here.
---@return Queue.Item[]
function Queue:list()
    return self.items
end

--- Replaces the entries with `kept`, which the caller built by dropping some of list().
---@param kept Queue.Item[] The entries that remain, in the same order.
function Queue:replace(kept)
    if #kept ~= #self.items then
        self.items = kept
        self.dirty = true
    end
end

--- Empties the queue and writes it to disk right away.
function Queue:clear()
    self.items = {}
    self.dirty = true
    self:persist()
end

--- Writes the entries to disk if they changed since the last write.
function Queue:persist()
    if not self.dirty then return end
    self.store:saveSetting("items", self.items)
    self.store:flush()
    self.dirty = false
end

return Queue
