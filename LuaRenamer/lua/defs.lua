--#region class definitions

---@class File
---@field public name string
---@field public path string
---@field public size number
---@field public hashes Hashes
---@field public anidb? AniDb
---@field public media Media
local File = {}

---@class Hashes
---@field public crc string
---@field public md5 string
---@field public ed2k string
---@field public sha1 string
local Hashes = {}

---@class AniDb
---@field public censored boolean
---@field public source string
---@field public version number
---@field public releasedate DateTime
---@field public releasegroup ReleaseGroup
---@field public id number
---@field public media AniDbMedia
local AniDb = {}

---@class ReleaseGroup
---@field public name string
---@field public shortname string
local ReleaseGroup = {}

---@class AniDbMedia
---@field public videocodec string
---@field public sublanguages integer[]
---@field public dublanguages integer[]
local AniDbMedia = {}

---@class Media
---@field public chaptered boolean
---@field public video Video
---@field public duration number
---@field public bitrate number
---@field public sublanguages integer[]
---@field public audio Audio[]
local Media = {}

---@class Video
---@field public height number
---@field public width number
---@field public codec string
---@field public res string
---@field public bitrate number
---@field public bitdepth number
---@field public framerate number
local Video = {}

---@class Audio
---@field public compressionmode string
---@field public bitrate number
---@field public channels number
---@field public bitdepth number
---@field public samplingrate number
---@field public bitratemode string
---@field public simplecodec string
---@field public codec string
---@field public language integer
---@field public title string
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
---@field getname getname
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
---@field getname getname
local Episode = {}

---@class ImportFolder
---@field name string
---@field location string
---@field type integer
local ImportFolder = {}

---@class Group
---@field name string
---@field mainSeriesid? integer
---@field seriesids integer[]
local Group = {}

---Returns the anime/episode name priority Main > Official (> Synonym > Short)? if include_unofficial
---@alias getname fun(self, lang:Language, include_unofficial?:boolean):string?

--#endregion class definitions

--#region enumerations

---@class DropFolderType
DropFolderType = {
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

---@class EpisodeType
EpisodeType = {
  Episode = 1,
  Credits = 2,
  Special = 3,
  Trailer = 4,
  Parody = 5,
  Other = 6,
}

--#endregion enumerations

---Returns formatted episode numbers with padding
---@param pad integer
---@return string
function episode_numbers(pad)
end

filename = ""
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
