-- The signed-in session: the access + refresh token pair, kept in KOReader's settings
-- directory, and the one place that knows how to renew it.

local DataStorage = require("datastorage")
local LuaSettings = require("luasettings")

local Session = {}
Session.__index = Session

-- opts = { api }
--   api : the api.lua module
function Session.new(opts)
    local self = setmetatable({}, Session)
    self.api = opts.api
    self.settings = LuaSettings:open(DataStorage:getSettingsDir() .. "/TaleTrack.lua")
    self.token = self.settings:readSetting("token")
    self.refresh_token = self.settings:readSetting("refresh_token")
    return self
end

function Session:isSignedIn()
    return self.token ~= nil
end

function Session:save(access, refresh)
    self.token = access
    self.refresh_token = refresh
    if access then
        self.settings:saveSetting("token", access)
    else
        self.settings:delSetting("token")
    end
    if refresh then
        self.settings:saveSetting("refresh_token", refresh)
    else
        self.settings:delSetting("refresh_token")
    end
    self.settings:flush()
end

function Session:clear()
    self:save(nil, nil)
end

-- Runs request(access_token), which must return an HTTP status and a response like the api.lua
-- calls do. On a 401 the token pair is renewed once and the request is retried with the new one.
-- The status returned says what happened to the session:
--   401 : there is no session, or the server rejected the renewal (the session is dead)
--   nil : the renewal could not be completed (no network, 5xx, rate limited): the session is kept
--   any other value is the one of the request itself
function Session:call(request)
    if not self.token then return 401 end

    local status, response = request(self.token)
    if status ~= 401 or not self.refresh_token then return status, response end

    local rstatus, rbody = self.api.refresh(self.refresh_token)
    if rstatus == 200 and rbody and rbody.token then
        self:save(rbody.token, rbody.refreshToken or self.refresh_token)
        return request(rbody.token)
    elseif rstatus == 401 then
        return 401, rbody
    end
    return nil, rbody
end

-- Best effort: revoke the session server-side, then drop the local tokens either way.
function Session:signOut()
    if self.refresh_token then
        pcall(self.api.logout, self.refresh_token)
    end
    self:clear()
end

return Session
