-- two-step OTP login dialog
-- step 1: email input → step 2: 6-digit code verification
-- `t` is the translator from i18n.lua, passed in by main.lua.

local InfoMessage = require("ui/widget/infomessage")
local InputDialog = require("ui/widget/inputdialog")
local UIManager = require("ui/uimanager")

local LoginDialog = {}

local function trim(text)
    return (text:match("^%s*(.-)%s*$"))
end

-- initial_email pre-fills the field, so a failed attempt can be corrected without retyping.
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

-- onBack is called when the user wants to re-enter their email
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
