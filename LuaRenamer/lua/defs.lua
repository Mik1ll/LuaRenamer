--#region class definitions

---@class File
---@field name string
---@field path string
---@field size number
---@field hashes Hashes
---@field anidb? AniDb
---@field media Media
---@field importfolder ImportFolder
local File = {}

---@class Hashes
---@field crc string
---@field md5 string
---@field ed2k string
---@field sha1 string
local Hashes = {}

---@class AniDb
---@field censored boolean
---@field source string
---@field version number
---@field releasedate DateTime
---@field releasegroup? ReleaseGroup
---@field id number
---@field media AniDbMedia
local AniDb = {}

---@class ReleaseGroup
---@field name string
---@field shortname string
local ReleaseGroup = {}

---@class AniDbMedia
---@field videocodec string
---@field sublanguages integer[]
---@field dublanguages integer[]
local AniDbMedia = {}

---@class Media
---@field chaptered boolean
---@field video Video
---@field duration number
---@field bitrate number
---@field sublanguages integer[]
---@field audio Audio[]
local Media = {}

---@class Video
---@field height number
---@field width number
---@field codec string
---@field res string
---@field bitrate number
---@field bitdepth number
---@field framerate number
local Video = {}

---@class Audio
---@field compressionmode string
---@field bitrate number
---@field channels number
---@field bitdepth number
---@field samplingrate number
---@field bitratemode string
---@field simplecodec string
---@field codec string
---@field language integer
---@field title string
local Audio = {}

---@class Anime
---@field airdate DateTime
---@field enddate DateTime
---@field rating number
---@field restricted boolean
---@field type integer
---@field preferredname string
---@field id integer
---@field titles Title[]
---@field episodecounts table<EpisodeType, integer>
local Anime = {}

---@class Title
---@field name string
---@field language integer
---@field languagecode string
---@field type integer
local Title = {}

---@class DateTime
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

---@class Episode
---@field duration integer
---@field number integer
---@field prefix string
---@field type integer
---@field airdate DateTime
---@field animeid integer
---@field id integer
---@field titles Title[]
local Episode = {}

---@class ImportFolder
---@field name string
---@field location string
---@field type integer
local ImportFolder = {}

---@class Group
---@field name string
---@field mainseriesid? integer
---@field seriesids integer[]
local Group = {}

---@param self Anime|Episode
---@param lang Language
---@param include_unofficial? boolean
---@return string? name
function Episode.getname(self, lang, include_unofficial) end
Anime.getname = Episode.getname

--#endregion class definitions

--#region enumerations

---@class ImportFolderType
ImportFolderType = {
  Excluded = 0,
  Source = 1,
  Destination = 2,
  Both = 3
}

---@class AnimeType
AnimeType = {
  Movie = 0,
  OVA = 1,
  TVSeries = 2,
  TVSpecial = 3,
  Web = 4,
  Other = 5
}

---@class EpisodeType
EpisodeType = {
  Episode = 1,
  Credits = 2,
  Special = 3,
  Trailer = 4,
  Parody = 5,
  Other = 6,
}

---@class TitleType
TitleType = {
  None = 0,
  Main = 1,
  Official = 2,
  Short = 3,
  Synonym = 4,
}

---@class Language
Language = {
  Unknown = 0,
  English = 1,
  Romaji = 2,
  Japanese = 3,
  Afrikaans = 4,
  Arabic = 5,
  Bangladeshi = 6,
  Bulgarian = 7,
  FrenchCanadian = 8,
  Czech = 9,
  Danish = 10,
  German = 11,
  Greek = 12,
  Spanish = 13,
  Estonian = 14,
  Finnish = 15,
  French = 16,
  Galician = 17,
  Hebrew = 18,
  Hungarian = 19,
  Italian = 20,
  Korean = 21,
  Lithuania = 22,
  Mongolian = 23,
  Malaysian = 24,
  Dutch = 25,
  Norwegian = 26,
  Polish = 27,
  Portuguese = 28,
  BrazilianPortuguese = 29,
  Romanian = 30,
  Russian = 31,
  Slovak = 32,
  Slovenian = 33,
  Serbian = 34,
  Swedish = 35,
  Thai = 36,
  Turkish = 37,
  Ukrainian = 38,
  Vietnamese = 39,
  Chinese = 40,
  ChineseSimplified = 41,
  ChineseTraditional = 42,
  Pinyin = 43,
  Latin = 44,
  Albanian = 45,
  Basque = 46,
  Bengali = 47,
  Bosnian = 48
}

--#endregion enumerations




---Returns formatted episode numbers with padding
---@param pad integer
---@return string
function episode_numbers(pad) end

---@type string|nil
filename = nil
---@type string|table|nil
destination = nil
---@type table|nil
subfolder = nil
---@type File
file = {}
---@type Anime
anime = {}
---@type Episode
episode = {}
---@type Anime[]
animes = {}
---@type Episode[]
episodes = {}
---@type ImportFolder[]
importfolders = {}
---@type Group[]
groups = {}
use_existing_anime_location = false
replace_illegal_chars = false
remove_illegal_chars = false