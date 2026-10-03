-- The signed-in session: the access + refresh token pair, kept in KOReader's settings
-- directory, and the one place that knows how to renew it.

local DataStorage = require("datastorage")
local LuaSettings = require("luasettings")

--- A backend call made with an access token, returning what the `Api` calls return.
---@alias Session.Request fun(access_token: string): integer?, Api.Response

--- Options for `Session.new`.
---@class Session.Options
---@field api Api The api.lua module, used to renew and revoke the tokens.

--- The signed-in user's session: the access and refresh tokens, saved in `TaleTrack.lua` in
--- KOReader's settings directory. The access token's expiry is not tracked locally (e-reader
--- clocks are unreliable); instead, a request rejected with 401 renews the pair once and is
--- retried.
---@class Session
---@field private api Api
---@field private settings table The LuaSettings file the tokens are saved in.
---@field token? string Access token, nil when signed out.
---@field refresh_token? string Refresh token, nil when signed out.
local Session = {}
---@private
Session.__index = Session

--- Opens the session, loading any tokens saved on disk.
---@param opts Session.Options
---@return Session
function Session.new(opts)
    local self = setmetatable({}, Session)
    self.api = opts.api
    self.settings = LuaSettings:open(DataStorage:getSettingsDir() .. "/TaleTrack.lua")
    self.token = self.settings:readSetting("token")
    self.refresh_token = self.settings:readSetting("refresh_token")
    return self
end

--- Whether there is an access token stored.
---@return boolean
function Session:isSignedIn()
    return self.token ~= nil
end

--- Stores the token pair, in memory and on disk. A nil value removes that token.
---@param access? string Access token.
---@param refresh? string Refresh token.
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

--- Forgets both tokens locally, without telling the server.
function Session:clear()
    self:save(nil, nil)
end

--- Runs `request` with the access token. On a 401 the token pair is renewed once and the
--- request is retried with the new one. The status returned says what happened to the session:
---
--- - 401: there is no session, or the server rejected the renewal (the session is dead).
--- - nil: the renewal could not be completed (no network, 5xx, rate limited); the session is kept.
--- - Any other value is the one of the request itself.
---@param request Session.Request
---@return integer? status
---@return Api.Response? response Nil when there was no session to make the request with.
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

--- Signs out. Best effort: revokes the session server-side, then drops the local tokens
--- whether or not the server could be reached.
function Session:signOut()
    if self.refresh_token then
        pcall(self.api.logout, self.refresh_token)
    end
    self:clear()
end

return Session
