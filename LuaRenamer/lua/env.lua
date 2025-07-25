---@meta

---Returns formatted episode numbers with padding
---@param pad integer The amount of padding to use
---@return string
function episode_numbers(pad) end

---Log with Debug log level
---@param message string The message to log
---@return nil
function logdebug(message) end

---Log with Information log level
---@param message string The message to log
---@return nil
function log(message) end

---Log with Warning log level
---@param message string The message to log
---@return nil
function logwarn(message) end

---Log with Error log level
---@param message string The message to log
---@return nil
function logerror(message) end

---The current file being processed
---@type File
file = nil

---The primary anime for the current file
---@type Anime
anime = nil

---All anime related to the current file
---@type Anime[]
animes = nil

---The primary episode for the current file
---@type Episode
episode = nil

---All episodes related to the current file
---@type Episode[]
episodes = nil

---All available import folders
---@type ImportFolder[]
importfolders = nil

---The group containing the primary anime
---@type Group|nil
group = nil

---All groups containing anime related to the current file
---@type Group[]
groups = nil

---TMDB information for the current file
---@type Tmdb
tmdb = nil

---Output: The filename to rename to
---@type string|nil
filename = nil

---Output: Import folder name / full directory path / ImportFolder that specifies the destination
---@type string|ImportFolder|nil
destination = nil

---Output: The subfolder to move the file to, must be an array table if there is more than one directory component
---@type string|string[]|nil
subfolder = nil

---Output: Whether to use the existing location of files from the same anime to determine the output destination/subfolder.
---@type boolean
use_existing_anime_location = false

---Output: Whether to replace illegal characters with their mapped values
---@type boolean
replace_illegal_chars = false

---Output: Whether to remove illegal characters entirely
---@type boolean
remove_illegal_chars = false

---Output: Whether to skip renaming the file
---@type boolean
skip_rename = false

---Output: Whether to skip moving the file
---@type boolean
skip_move = false

---Output: Map of illegal characters to their replacements
---@type table<string, string>
illegal_chars_map = nil
