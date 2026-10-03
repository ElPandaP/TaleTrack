-- What the plugin needs to know about the book open in the reader: how far along the user is,
-- whether they have reached the end, and which book it is. Everything that touches the ui or
-- the document object lives here; the author and ISBN come from bookmeta.lua.

-- The last pages of a book are often acknowledgements, notes or ads nobody reads,
-- so reaching them counts as finishing: the final 5% of pages, capped at 25 pages.
local END_MARGIN_PERCENT = 5
local END_MARGIN_MAX_PAGES = 25

--- Identity and metadata of the open book, as sent to the backend.
---@class Book.Info
---@field key string Deduplication key for the queue: the document's partial MD5 checksum, or the lowercased title.
---@field title string Title from the metadata, else the file name, else the "unknown" title.
---@field pages? integer Page count, only for fixed-layout documents (PDF, DjVu).
---@field author? string Authors joined with ", ".
---@field isbn? string Bare ISBN-10 or ISBN-13.

--- Options for `Book.new`.
---@class Book.Options
---@field ui table The ReaderUI (or FileManager, which has no document) the plugin runs in.
---@field meta BookMeta The bookmeta.lua module.
---@field unknown_title string Title to use when the book has none and its file name can't be read.

--- The book open in the reader: reading progress, whether the end margin has been reached, and
--- the book's identity. One per plugin instance; in the file browser there is no document and
--- every query answers nil (or false).
---@class Book
---@field ui table The ReaderUI or FileManager this instance reads from.
---@field meta BookMeta Extracts author and ISBN from the document properties.
---@field unknown_title string Fallback title.
local Book = {}
---@private
Book.__index = Book

--- Creates the book view for one plugin instance.
---@param opts Book.Options
---@return Book
function Book.new(opts)
    local self = setmetatable({}, Book)
    self.ui = opts.ui
    self.meta = opts.meta
    self.unknown_title = opts.unknown_title
    return self
end

--- The page the reader is on. ReaderUI knows it for both fixed-layout documents (PDF, DjVu) and
--- reflowable ones (EPUB); the document alone only does for the latter.
---@return integer? page Current page number, or nil when there is no document.
function Book:currentPage()
    local doc = self.ui.document
    if not doc then return nil end
    if self.ui.getCurrentPage then return self.ui:getCurrentPage() end
    return doc.getCurrentPage and doc:getCurrentPage() or nil
end

--- Current reading progress as an integer percent, as the reader itself reports it, falling
--- back to current page over page count.
---@return integer? percent Progress from 0 to 100, or nil when it can't be known.
function Book:progress()
    local doc = self.ui.document
    if not doc then return nil end

    local percent
    if doc.info and doc.info.has_pages then
        percent = self.ui.paging and self.ui.paging:getLastPercent()
    else
        percent = self.ui.rolling and self.ui.rolling:getLastPercent()
    end
    if not percent then
        local cur = self:currentPage()
        local total = doc.getPageCount and doc:getPageCount()
        if cur and total and total > 0 then percent = cur / total end
    end
    if not percent then return nil end

    local p = math.floor(percent * 100 + 0.5)
    if p < 0 then p = 0 elseif p > 100 then p = 100 end
    return p
end

--- Whether the reader is within the end margin of the book (the last 5% of pages, at most 25),
--- where the book counts as finished.
---@return boolean
function Book:nearEnd()
    local doc = self.ui.document
    local cur = self:currentPage()
    local total = doc and doc.getPageCount and doc:getPageCount()
    if not (cur and total and total > 0) then return false end

    local margin = math.min(math.ceil(total * END_MARGIN_PERCENT / 100), END_MARGIN_MAX_PAGES)
    return total - cur <= margin
end

--- The file name without its directory or extension, or nil.
---@param path any The document's file path.
---@return string?
local function fileTitle(path)
    if type(path) ~= "string" then return nil end
    local name = path:match("([^/\\]+)$")
    name = name and name:gsub("%.[^.]+$", "")
    if name and name ~= "" then return name end
    return nil
end

local MAX_TITLE = 255 -- backend limit for Title

--- Cuts a string to at most `max` bytes without splitting a UTF-8 character.
---@param s string
---@param max integer
---@return string
local function clip(s, max)
    if #s <= max then return s end
    local cut = max
    -- A continuation byte (10xxxxxx) right after the cut means a character would be split.
    while cut > 0 and s:byte(cut + 1) >= 0x80 and s:byte(cut + 1) < 0xC0 do
        cut = cut - 1
    end
    return s:sub(1, cut)
end

--- Identity and metadata of the open book.
--- `pages` is only given for fixed-layout documents: an EPUB has as many "pages" as the font and
--- margins make it, so its count says nothing about the book.
---@return Book.Info? info Nil when no document is open.
function Book:info()
    local doc = self.ui.document
    if not doc then return nil end

    local props = (doc.getProps and doc:getProps()) or {}
    local title = (props.title and props.title ~= "") and props.title
        or fileTitle(doc.file)
        or self.unknown_title
    title = clip(title, MAX_TITLE)

    local pages
    if doc.info and doc.info.has_pages and doc.getPageCount then
        pages = math.max(doc:getPageCount() or 1, 1)
    end

    local key = (self.ui.doc_settings and self.ui.doc_settings:readSetting("partial_md5_checksum"))
        or title:lower()

    local author, isbn = self.meta.extract(props)

    return { key = key, title = title, pages = pages, author = author, isbn = isbn }
end

return Book
