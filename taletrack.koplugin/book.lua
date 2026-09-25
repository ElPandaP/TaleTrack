-- What the plugin needs to know about the book open in the reader: how far along the user is,
-- whether they have reached the end, and which book it is. Everything that touches the ui or
-- the document object lives here; the author and ISBN come from bookmeta.lua.

-- The last pages of a book are often acknowledgements, notes or ads nobody reads,
-- so reaching them counts as finishing: the final 5% of pages, capped at 25 pages.
local END_MARGIN_PERCENT = 5
local END_MARGIN_MAX_PAGES = 25

local Book = {}
Book.__index = Book

-- opts = { ui, meta, unknown_title }
--   ui            : the ReaderUI (or FileManager, which has no document) the plugin runs in
--   meta          : the bookmeta.lua module
--   unknown_title : title to use when the book has none and its file name can't be read
function Book.new(opts)
    local self = setmetatable({}, Book)
    self.ui = opts.ui
    self.meta = opts.meta
    self.unknown_title = opts.unknown_title
    return self
end

-- The page the reader is on. ReaderUI knows it for both fixed-layout documents (PDF, DjVu) and
-- reflowable ones (EPUB); the document alone only does for the latter.
function Book:currentPage()
    local doc = self.ui.document
    if not doc then return nil end
    if self.ui.getCurrentPage then return self.ui:getCurrentPage() end
    return doc.getCurrentPage and doc:getCurrentPage() or nil
end

-- Current reading progress as an integer percent (0-100), or nil.
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

-- True once the reader is within the end margin of the book.
function Book:nearEnd()
    local doc = self.ui.document
    local cur = self:currentPage()
    local total = doc and doc.getPageCount and doc:getPageCount()
    if not (cur and total and total > 0) then return false end

    local margin = math.min(math.ceil(total * END_MARGIN_PERCENT / 100), END_MARGIN_MAX_PAGES)
    return total - cur <= margin
end

-- The file name without its directory or extension, or nil.
local function fileTitle(path)
    if type(path) ~= "string" then return nil end
    local name = path:match("([^/\\]+)$")
    name = name and name:gsub("%.[^.]+$", "")
    if name and name ~= "" then return name end
    return nil
end

-- { key, title, pages?, author?, isbn? } for the open book, or nil when there is none.
-- `pages` is only given for fixed-layout documents: an EPUB has as many "pages" as the font and
-- margins make it, so its count says nothing about the book.
function Book:info()
    local doc = self.ui.document
    if not doc then return nil end

    local props = (doc.getProps and doc:getProps()) or {}
    local title = (props.title and props.title ~= "") and props.title
        or fileTitle(doc.file)
        or self.unknown_title

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
