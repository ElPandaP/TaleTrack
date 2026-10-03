-- Two-step OTP login dialog.
-- Step 1: email input. Step 2: 6-digit code verification.
-- `t` is the translator from i18n.lua, passed in by main.lua.

local InfoMessage = require("ui/widget/infomessage")
local InputDialog = require("ui/widget/inputdialog")
local UIManager = require("ui/uimanager")

--- The two input dialogs of the email-code sign-in. They only collect and check that the input
--- isn't empty; what happens next (calling the backend, showing errors) is up to the callbacks.
---@class LoginDialog
local LoginDialog = {}

--- Removes leading and trailing whitespace.
---@param text string
---@return string
local function trim(text)
    return (text:match("^%s*(.-)%s*$"))
end

--- Shows the first step: asks for the email address the code will be sent to.
---@param t I18n.Translator
---@param onEmailSubmit fun(email: string) Called with the trimmed address once the user submits it.
---@param initial_email? string Pre-fills the field, so a failed attempt can be corrected without retyping.
function LoginDialog.showEmailStep(t, onEmailSubmit, initial_email)
    local dialog
    dialog = InputDialog:new{
        title = t("login_title"),
        input = initial_email or "",
        input_hint = t("email_hint"),
        input_type = "text",
        buttons = {{
            {
                text = t("cancel"),
                callback = function()
                    UIManager:close(dialog)
                end,
            },
            {
                text = t("send_code"),
                is_enter_default = true,
                callback = function()
                    local email = trim(dialog:getInputText())
                    if email == "" then
                        UIManager:show(InfoMessage:new{
                            text = t("enter_email"),
                            timeout = 3,
                        })
                        return -- the dialog stays open
                    end
                    UIManager:close(dialog)
                    onEmailSubmit(email)
                end,
            },
        }},
    }
    UIManager:show(dialog)
end

--- Shows the second step: asks for the code that was emailed to `email`.
---@param t I18n.Translator
---@param email string Address the code was sent to, shown in the dialog.
---@param onCodeSubmit fun(code: string) Called with the trimmed code once the user submits it.
---@param onBack? fun() Called when the user goes back to re-enter their email.
function LoginDialog.showCodeStep(t, email, onCodeSubmit, onBack)
    local dialog
    dialog = InputDialog:new{
        title = t("enter_code_title"),
        description = t("code_sent_to", email),
        input_hint = t("code_hint"),
        input_type = "number",
        buttons = {{
            {
                text = t("back"),
                callback = function()
                    UIManager:close(dialog)
                    if onBack then onBack() end
                end,
            },
            {
                text = t("verify"),
                is_enter_default = true,
                callback = function()
                    local code = trim(dialog:getInputText())
                    if code == "" then
                        UIManager:show(InfoMessage:new{
                            text = t("enter_code"),
                            timeout = 3,
                        })
                        return -- the dialog stays open
                    end
                    UIManager:close(dialog)
                    onCodeSubmit(code)
                end,
            },
        }},
    }
    UIManager:show(dialog)
end

return LoginDialog
