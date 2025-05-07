---@meta

---Returns formatted episode numbers with padding
---@param pad integer
---@return string
function episode_numbers(pad) end

---Log with Debug log level
---@param message string
---@return nil
function logdebug(message) end

---Log with Information log level
---@param message string
---@return nil
function log(message) end

---Log with Warning log level
---@param message string
---@return nil
function logwarn(message) end

---Log with Error log level
---@param message string
---@return nil
function logerror(message) end

---@type File
file = nil
---@type Anime
anime = nil
---@type Episode
episode = nil
---@type Anime[]
animes = nil
---@type Episode[]
episodes = nil
---@type ImportFolder[]
importfolders = nil
---@type Group[]
groups = nil
---@type Group?
group = nil

-- Output variables

---@type string?
filename = nil
---@type (string|ImportFolder)?
destination = nil
---@type (string|string[])?
subfolder = nil

use_existing_anime_location = false
replace_illegal_chars = false
remove_illegal_chars = false
skip_rename = false
skip_move = false
---@type { [string]: string }
illegal_chars_override = {}

