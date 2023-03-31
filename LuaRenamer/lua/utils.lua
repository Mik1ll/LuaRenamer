---Trims string, removes extra spaces, and replaces with given character or space
---@param self string
---@param char string?
---@return string
function string:cleanspaces(char) return (self:match("^%s*(.-)%s*$"):gsub("%s+", char or " ")) end

---Returns truncated string, supporting unicode characters
---@param self string
---@param len integer
---@return string
function string:truncate(len) return utf8.len(self) > len and self:sub(1, utf8.offset(self, len + 1, 1) - 1):gsub("%s+$", "") .. "..." or self end