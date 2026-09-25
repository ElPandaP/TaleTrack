local WidgetContainer = require("ui/widget/container/widgetcontainer")
local UIManager = require("ui/uimanager")
local InfoMessage = require("ui/widget/infomessage")
local NetworkMgr = require("ui/network/manager")

-- How often, at most, progress is pushed while actively reading (seconds).
local SYNC_INTERVAL = 120

-- A message from the backend is shown to the user only if it is short enough to read.
local MAX_BACKEND_MESSAGE = 120

-- KOReader builds a new instance of this plugin for every file browser and every reader it opens,
-- but the session and the queue must exist once per process, or two instances would each keep
-- their own copy of the tokens and of the queue file. So the collaborators are created on first
-- use and shared. (They are loaded with dofile from the plugin's own path, not with require, so
-- names like "api" or "queue" can't clash with KOReader's own modules.)
local function loadCore(path)
    local key = "taletrack/core"
    if package.loaded[key] then return package.loaded[key] end

    local function load(name) return dofile(path .. "/" .. name .. ".lua") end

    local api = load("api")
    local session = load("session").new{ api = api }
    local queue = load("queue").new()
    local core = {
        api = api,
        session = session,
        queue = queue,
        sync = load("sync").new{ api = api, session = session, queue = queue },
        Book = load("book"),
        book_meta = load("bookmeta"),
        login_dialog = load("login_dialog"),
        i18n = load("i18n"),
    }
    package.loaded[key] = core
    return core
end

local TaleTrack = WidgetContainer:extend{
    name = "TaleTrack",
    is_doc_only = false, -- also loaded in the file browser, so signing in and draining the queue work outside a book
}

function TaleTrack:init()
    self.core = loadCore(self.path)

    local i18n = self.core.i18n.setup()
    self.lang = i18n.lang
    self.t = i18n.t

    self.book = self.core.Book.new{
        ui = self.ui,
        meta = self.core.book_meta,
        unknown_title = self.t("unknown_book"),
    }

    -- One stored reference each, so UIManager:unschedule can cancel them.
    self.sync_task = function()
        self.sync_scheduled = false
        self:syncCurrentProgress()
    end
    self.flush_task = function() self:flushQueue() end

    self.ui.menu:registerToMainMenu(self)

    -- Drain anything left from a previous offline session.
    if self:isSignedIn() then UIManager:scheduleIn(3, self.flush_task) end
end

-- The tasks are this instance's; once its widget is gone they must not run against it.
function TaleTrack:onCloseWidget()
    UIManager:unschedule(self.sync_task)
    UIManager:unschedule(self.flush_task)
end

function TaleTrack:isSignedIn()
    return self.core.session:isSignedIn()
end

--------------------------------------------------------------------------------
-- Progress queueing
--------------------------------------------------------------------------------

-- Enqueue the given progress for the current book (silent). `progress` defaults
-- to whatever the reader is showing right now, or 100 within the end margin.
function TaleTrack:enqueueCurrent(progress)
    if not self:isSignedIn() then return end
    progress = progress or (self.book:nearEnd() and 100 or self.book:progress())
    if not progress or progress < 1 then return end
    -- The backend only ever raises progress, so anything not above the last one queued is noise.
    if self.last_queued_progress and progress <= self.last_queued_progress then return end

    local book = self.book:info()
    if not book then return end

    self.core.queue:enqueue{
        key = book.key,
        title = book.title,
        pages = book.pages,
        author = book.author,
        isbn = book.isbn,
        progress = progress,
    }
    self.last_queued_progress = progress
end

function TaleTrack:flushQueue()
    if not self:isSignedIn() then return end
    self.core.sync:flush(function(kind)
        if kind == "auth" then
            self.core.session:clear()
            UIManager:show(InfoMessage:new{ text = self.t("session_expired"), timeout = 4 })
        end
    end)
end

function TaleTrack:syncCurrentProgress()
    self:enqueueCurrent()
    UIManager:scheduleIn(0, self.flush_task)
end

--------------------------------------------------------------------------------
-- Reader events (broadcast to all widgets)
--------------------------------------------------------------------------------

function TaleTrack:onReaderReady()
    self.last_queued_progress = nil
    self.sync_scheduled = false
    if self:isSignedIn() then UIManager:scheduleIn(3, self.flush_task) end
end

function TaleTrack:onPageUpdate()
    if not self:isSignedIn() then return end
    -- Entering the end margin is reported right away instead of waiting for the interval.
    if self.last_queued_progress ~= 100 and self.book:nearEnd() then
        self:syncCurrentProgress()
        return
    end
    if self.sync_scheduled then return end
    self.sync_scheduled = true
    UIManager:scheduleIn(SYNC_INTERVAL, self.sync_task)
end

function TaleTrack:onEndOfBook()
    if not self:isSignedIn() then return end
    self:enqueueCurrent(100)
    UIManager:scheduleIn(0, self.flush_task)
end

function TaleTrack:onCloseDocument()
    UIManager:unschedule(self.sync_task)
    self.sync_scheduled = false
    self:enqueueCurrent()
    self:flushQueue()
    self.core.queue:persist()
    self.last_queued_progress = nil
end

function TaleTrack:onSuspend()
    UIManager:unschedule(self.sync_task)
    self.sync_scheduled = false
    self:enqueueCurrent()
    self.core.queue:persist()
end

function TaleTrack:onResume()
    if self:isSignedIn() then UIManager:scheduleIn(1, self.flush_task) end
end

function TaleTrack:onNetworkConnected()
    if self:isSignedIn() then UIManager:scheduleIn(0.5, self.flush_task) end
end

function TaleTrack:onFlushSettings()
    self.core.queue:persist()
end

--------------------------------------------------------------------------------
-- Menu + login
--------------------------------------------------------------------------------

function TaleTrack:addToMainMenu(menu_items)
    menu_items.TaleTrack = {
        text = "TaleTrack",
        sorting_hint = "tools",
        sub_item_table = {
            {
                text_func = function()
                    return self:isSignedIn() and self.t("sign_out") or self.t("sign_in")
                end,
                callback = function()
                    if self:isSignedIn() then
                        self:logout()
                    else
                        self:showLogin()
                    end
                end,
            },
        },
    }
end

-- Runs `action` once the device is online, asking to turn wifi on first if it isn't.
function TaleTrack:whenOnline(action)
    if NetworkMgr.runWhenOnline then
        NetworkMgr:runWhenOnline(action)
    else
        action()
    end
end

-- KOReader does network calls on the thread that draws the screen, so `work` (a blocking call
-- returning a status and a response) would freeze it with no explanation. Show `text` and
-- repaint before it starts, and take the message down when it is done.
function TaleTrack:whileShowing(text, work)
    local info = InfoMessage:new{ text = text }
    UIManager:show(info)
    UIManager:forceRePaint()
    local ok, status, response = pcall(work)
    UIManager:close(info)
    if not ok then error(status, 0) end
    return status, response
end

function TaleTrack:showFailure(text)
    UIManager:show(InfoMessage:new{ text = text, timeout = 4 })
end

-- What to tell the user about a failed backend call. `rejected` is the text for a request the
-- backend understood and refused (with no reason of its own worth showing).
function TaleTrack:failureText(status, response, rejected)
    if status == nil then return self.t("connection_error") end
    if status == 429 then return self.t("too_many_requests") end
    if status >= 500 then return self.t("server_error") end

    local message = response and response.message
    if type(message) == "string" and message ~= "" and #message <= MAX_BACKEND_MESSAGE then
        return message
    end
    return rejected
end

-- Two-step OTP login: email -> request a code -> verify it.
function TaleTrack:showLogin(email)
    self.core.login_dialog.showEmailStep(self.t, function(submitted)
        self:requestCode(submitted)
    end, email)
end

function TaleTrack:showCodeStep(email)
    self.core.login_dialog.showCodeStep(self.t, email, function(code)
        self:verifyCode(email, code)
    end, function()
        self:showLogin(email)
    end)
end

function TaleTrack:requestCode(email)
    self:whenOnline(function()
        local status, response = self:whileShowing(self.t("sending_code"), function()
            return self.core.api.requestCode(email, self.lang)
        end)

        if status == 200 and response.success then
            self:showCodeStep(email)
            return
        end

        -- Back to the email dialog, with the address filled in, so it can be corrected.
        local reason = status == 400 and self.t("invalid_email")
            or self:failureText(status, response, self.t("connection_error"))
        self:showLogin(email)
        self:showFailure(self.t("send_code_error", reason))
    end)
end

function TaleTrack:verifyCode(email, code)
    self:whenOnline(function()
        local status, response = self:whileShowing(self.t("verifying_code"), function()
            return self.core.api.verifyCode(email, code)
        end)

        if status == 200 and response.success and response.token then
            self.core.session:save(response.token, response.refreshToken)
            UIManager:show(InfoMessage:new{
                text = self.t("signed_in"),
                timeout = 2,
            })
            UIManager:scheduleIn(1, self.flush_task)
            return
        end

        -- Back to the code dialog, so a mistyped code can be corrected.
        local reason = self:failureText(status, response, self.t("code_invalid"))
        self:showCodeStep(email)
        self:showFailure(self.t("verify_code_error", reason))
    end)
end

function TaleTrack:logout()
    local session = self.core.session
    if NetworkMgr:isConnected() then
        self:whileShowing(self.t("signing_out"), function() session:signOut() end)
    else
        session:clear() -- nothing to reach the server with; the session just expires there
    end
    -- Pending progress belongs to this account; don't let it leak into the next one.
    self.core.queue:clear()
    UIManager:show(InfoMessage:new{
        text = self.t("signed_out"),
        timeout = 2,
    })
end

return TaleTrack
