-- Author and ISBN extraction from the document properties KOReader exposes
-- (doc:getProps()). For EPUBs these come from the OPF metadata: `authors` is one
-- name per line and `identifiers` is one identifier per line, e.g.
-- "urn:isbn:978-84-376-0494-7", "uuid:6f9619ff-...".

--- Reads the author and the ISBN of a book from its document properties, so the backend can
--- match the book by ISBN or by title and author. Values that the backend would reject (an
--- identifier that isn't a valid ISBN, an author list over 255 characters) are left out.
---@class BookMeta
local BookMeta = {}

local MAX_AUTHOR = 255 -- backend limit for Author

--- Whether a 10-character ISBN has a valid check digit.
---@param d string Ten characters: nine digits and a digit or "X".
---@return boolean
local function isbn10_valid(d)
    local sum = 0
    for i = 1, 10 do
        local c = d:sub(i, i)
        sum = sum + (c == "X" and 10 or tonumber(c)) * (11 - i)
    end
    return sum % 11 == 0
end

--- Whether a 13-digit ISBN has a valid check digit.
---@param d string Thirteen digits.
---@return boolean
local function isbn13_valid(d)
    local sum = 0
    for i = 1, 13 do
        sum = sum + tonumber(d:sub(i, i)) * (i % 2 == 1 and 1 or 3)
    end
    return sum % 10 == 0
end

--- Bare ISBN-10/13 (digits only, checksum verified) or nil if it isn't one. A "urn:isbn:" or
--- "isbn:" prefix, hyphens and spaces are removed first.
---@param identifier string One identifier from the document metadata.
---@return string?
local function normalize_isbn(identifier)
    local s = identifier:upper()
    s = s:gsub("^%s*URN:", "")
    s = s:gsub("^%s*ISBN[:%s]*", "")
    s = s:gsub("[%s%-]", "")
    if s:match("^97[89]%d%d%d%d%d%d%d%d%d%d$") and isbn13_valid(s) then return s end
    if s:match("^%d%d%d%d%d%d%d%d%d[%dX]$") and isbn10_valid(s) then return s end
    return nil
end

--- The first valid ISBN among the document identifiers.
---@param identifiers any The `identifiers` property: identifiers separated by new lines, commas or semicolons.
---@return string? isbn Bare ISBN-10 or ISBN-13, or nil when there is none.
function BookMeta.isbn(identifiers)
    if type(identifiers) ~= "string" then return nil end
    for line in identifiers:gmatch("[^\r\n,;]+") do
        local isbn = normalize_isbn(line)
        if isbn then return isbn end
    end
    return nil
end

--- All authors joined with ", "; only the first one if the list is too long for the backend.
---@param authors any The `authors` property: one name per line.
---@return string? author Nil when there is no author or even the first one is too long.
function BookMeta.author(authors)
    if type(authors) ~= "string" then return nil end
    local list = {}
    for name in authors:gmatch("[^\r\n]+") do
        name = name:match("^%s*(.-)%s*$")
        if name ~= "" then list[#list + 1] = name end
    end
    if #list == 0 then return nil end

    local joined = table.concat(list, ", ")
    if #joined <= MAX_AUTHOR then return joined end
    return #list[1] <= MAX_AUTHOR and list[1] or nil
end

--- Author and ISBN of a book, each nil when the document doesn't carry it.
---@param props any The table from doc:getProps() (or any table with `authors` and `identifiers`).
---@return string? author
---@return string? isbn
function BookMeta.extract(props)
    if type(props) ~= "table" then return nil, nil end
    return BookMeta.author(props.authors), BookMeta.isbn(props.identifiers)
end

return BookMeta
