-- Author and ISBN extraction from the document properties KOReader exposes
-- (doc:getProps()). For EPUBs these come from the OPF metadata: `authors` is one
-- name per line and `identifiers` is one identifier per line, e.g.
-- "urn:isbn:978-84-376-0494-7", "uuid:6f9619ff-...".

local BookMeta = {}

local MAX_AUTHOR = 255 -- backend limit for Author

local function isbn10_valid(d)
    local sum = 0
    for i = 1, 10 do
        local c = d:sub(i, i)
        sum = sum + (c == "X" and 10 or tonumber(c)) * (11 - i)
    end
    return sum % 11 == 0
end

local function isbn13_valid(d)
    local sum = 0
    for i = 1, 13 do
        sum = sum + tonumber(d:sub(i, i)) * (i % 2 == 1 and 1 or 3)
    end
    return sum % 10 == 0
end

-- Bare ISBN-10/13 (digits only, checksum verified) or nil if it isn't one.
local function normalize_isbn(identifier)
    local s = identifier:upper()
    s = s:gsub("^%s*URN:", "")
    s = s:gsub("^%s*ISBN[:%s]*", "")
    s = s:gsub("[%s%-]", "")
    if s:match("^97[89]%d%d%d%d%d%d%d%d%d%d$") and isbn13_valid(s) then return s end
    if s:match("^%d%d%d%d%d%d%d%d%d[%dX]$") and isbn10_valid(s) then return s end
    return nil
end

function BookMeta.isbn(identifiers)
    if type(identifiers) ~= "string" then return nil end
    for line in identifiers:gmatch("[^\r\n,;]+") do
        local isbn = normalize_isbn(line)
        if isbn then return isbn end
    end
    return nil
end

-- All authors joined with ", "; only the first one if the list is too long for the backend.
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

-- props = the table from doc:getProps() (or any table with authors/identifiers).
-- Returns author, isbn (each nil when the document doesn't carry it).
function BookMeta.extract(props)
    if type(props) ~= "table" then return nil, nil end
    return BookMeta.author(props.authors), BookMeta.isbn(props.identifiers)
end

return BookMeta
