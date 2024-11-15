---@meta

---@class (exact) File
---@field name string
---@field extension string
---@field earliestname string?
---@field path string
---@field size integer
---@field hashes Hashes
---@field anidb AniDb?
---@field media Media
---@field importfolder ImportFolder
local File = {}

---@class (exact) Hashes
---@field crc string?
---@field md5 string?
---@field ed2k string
---@field sha1 string?
local Hashes = {}

---@class (exact) AniDb
---@field censored boolean
---@field source string
---@field version integer
---@field releasedate DateTime
---@field releasegroup ReleaseGroup?
---@field id integer
---@field description string
---@field media AniDbMedia
local AniDb = {}

---@class (exact) ReleaseGroup
---@field name string
---@field shortname string
local ReleaseGroup = {}

---@class (exact) AniDbMedia
---@field sublanguages Language[]
---@field dublanguages Language[]
local AniDbMedia = {}

---@class (exact) Media
---@field chaptered boolean
---@field video Video?
---@field duration integer
---@field bitrate integer
---@field sublanguages string[]
---@field audio Audio[]
local Media = {}

---@class (exact) Video
---@field height integer
---@field width integer
---@field codec string
---@field res string
---@field bitrate integer
---@field bitdepth integer
---@field framerate number
local Video = {}

---@class (exact) Audio
---@field compressionmode string
---@field channels number
---@field samplingrate integer
---@field codec string
---@field language string
---@field title string?
local Audio = {}

---@class (exact) Anime
---@field airdate DateTime?
---@field enddate DateTime?
---@field rating number
---@field restricted boolean
---@field type AnimeType
---@field preferredname string
---@field defaultname string
---@field id integer
---@field titles Title[]
---@field episodecounts table<EpisodeType, integer>
---@field relations Relation[] Note: relations are not populated for related anime
local Anime = {}

---@param lang Language
---@param include_unofficial? boolean
---@return string?
function Anime:getname(lang, include_unofficial)
end

---@class (exact) Title
---@field name string
---@field language Language
---@field languagecode string
---@field type TitleType
local Title = {}

---@class (exact) DateTime
---@field year integer
---@field month integer
---@field day integer
---@field yday integer
---@field wday integer
---@field hour integer
---@field min integer
---@field sec integer
---@field isdst boolean
local DateTime = {}

---@class (exact) Episode
---@field duration integer
---@field number integer
---@field prefix string
---@field type EpisodeType
---@field airdate DateTime
---@field animeid integer
---@field id integer
---@field titles Title[]
local Episode = {}

---@param lang Language
---@return string?
function Episode:getname(lang)
end

---@class (exact) ImportFolder
---@field id integer
---@field name string
---@field location string
---@field type ImportFolderType
local ImportFolder = {}

---@class (exact) Group
---@field name string
---@field mainanime Anime
---@field animes Anime[]
local Group = {}

---@class (exact) Relation
---@field type RelationType
---@field anime Anime
local Relation = {}
