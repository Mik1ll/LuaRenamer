---Trims string, removes extra spaces, and replaces with given character or space.
---@param self string
---@param char string? Character to replace spaces with (Default: " ")
---@return string
function string:cleanspaces(char) return (self:match("^%s*(.-)%s*$"):gsub("%s+", char or " ")) end

---Truncates string to given length and removes trailing space. Counts unicode characters by default.
---@param self string
---@param length integer Max length of the string (including ellipses)
---@param ellipses string? Append if string has been truncated (Default: "...")
---@param countbytes boolean? Count length of string in bytes instead of unicode characters (Default: false)
---@return string
function string:truncate(length, ellipses, countbytes)
    ellipses = ellipses or "..."
    if (countbytes and #self or utf8.len(self)) <= length then return self end
    local lastindex = (countbytes and
        utf8.offset(self, 0, length - #ellipses + 1) or
        utf8.offset(self, length - utf8.len(ellipses) + 1, 1)) - 1
    return self:sub(1, lastindex):gsub("%s+$", "") .. ellipses
end
