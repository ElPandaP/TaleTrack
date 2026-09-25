-- Offline queue for book-progress syncs.
-- Holds at most one entry per book (the latest / highest progress). Persisted to its own file so
-- writing it is cheap and never touches auth state. Sending it is sync.lua's job.

local DataStorage = require("datastorage")
local LuaSettings = require("luasettings")

local MAX_ITEMS = 100
local MAX_AGE = 4 * 7 * 24 * 60 * 60 -- 4 weeks, in seconds

local Queue = {}
Queue.__index = Queue

-- Epoch time, or nil if the device clock looks bogus (e-reader RTCs drift badly).
local function sane_now()
    local t = os.time()
    if type(t) == "number" and t > 1600000000 then return t end
    return nil
end

function Queue.new()
    local self = setmetatable({}, Queue)
    self.store = LuaSettings:open(DataStorage:getSettingsDir() .. "/taletrack_queue.lua")
    self.items = self.store:readSetting("items") or {}
    self.dirty = false
    return self
end

-- item = { key, title, progress (1-100), pages?, author?, isbn? }
function Queue:enqueue(item)
    if not item.key or not item.title then return end
    if not item.progress or item.progress < 1 then return end

    local kept, prev, prev_item = {}, 0, nil
    for _, it in ipairs(self.items) do
        if it.key == item.key then
            prev = it.progress or 0
            prev_item = it
        else
            kept[#kept + 1] = it
        end
    end

    item.progress = math.max(item.progress, prev)
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

function Queue:isEmpty()
    return #self.items == 0
end

-- The entries, oldest first. Read only: change the queue through the methods here.
function Queue:list()
    return self.items
end

-- Replaces the entries with `kept`, which the caller built by dropping some of list().
function Queue:replace(kept)
    if #kept ~= #self.items then
        self.items = kept
        self.dirty = true
    end
end

function Queue:clear()
    self.items = {}
    self.dirty = true
    self:persist()
end

function Queue:persist()
    if not self.dirty then return end
    self.store:saveSetting("items", self.items)
    self.store:flush()
    self.dirty = false
end

return Queue
