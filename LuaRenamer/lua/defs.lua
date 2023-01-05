--#region class definitions

---@class File
---@field name string
---@field path string
---@field size integer
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
---@field version integer
---@field releasedate DateTime
---@field releasegroup? ReleaseGroup
---@field id integer
---@field description string
---@field media AniDbMedia
local AniDb = {}

---@class ReleaseGroup
---@field name string
---@field shortname string
local ReleaseGroup = {}

---@class AniDbMedia
---@field sublanguages Language[]
---@field dublanguages Language[]
local AniDbMedia = {}

---@class Media
---@field chaptered boolean
---@field video Video
---@field duration integer
---@field bitrate integer
---@field sublanguages string[]
---@field audio Audio[]
local Media = {}

---@class Video
---@field height integer
---@field width integer
---@field codec string
---@field res string
---@field bitrate integer
---@field bitdepth integer
---@field framerate number
local Video = {}

---@class Audio
---@field compressionmode string
---@field channels number
---@field samplingrate integer
---@field codec string
---@field language string
---@field title? string
local Audio = {}

---@class Anime
---@field airdate DateTime
---@field enddate DateTime
---@field rating number
---@field restricted boolean
---@field type AnimeType
---@field preferredname string
---@field id integer
---@field titles Title[]
---@field episodecounts table<EpisodeType, integer>
---@field relations RelatedAnime[]
local Anime = {}

---@class RelatedAnime
---@field anime Anime
---@field relationtype RelationType
local RelatedAnime = {}

---@class Title
---@field name string
---@field language Language
---@field languagecode string
---@field type TitleType
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
---@field type EpisodeType
---@field airdate DateTime
---@field animeid integer
---@field id integer
---@field titles Title[]
local Episode = {}

---@class ImportFolder
---@field name string
---@field location string
---@field type ImportFolderType
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

---@alias ImportFolderType "'Excluded'" | "'Source'" | "'Destination'" | "'Both'"
ImportFolderType = {
  Excluded = "Excluded",
  Source = "Source",
  Destination = "Destination",
  Both = "Both"
}

---@alias AnimeType "'Movie'" | "'OVA'" | "'TVSeries'" | "'TVSpecial'" | "'Web'" | "'Other'"
AnimeType = {
  Movie = "Movie",
  OVA = "OVA",
  TVSeries = "TVSeries",
  TVSpecial = "TVSpecial",
  Web = "Web",
  Other = "Other"
}

---@alias RelationType "'Other'" | "'SameSetting'" | "'AlternativeSetting'" | "'AlternativeVersion'" | "'SharedCharacters'" | "'Prequel'" | "'MainStory'" | "'FullStory'" | "'Sequel'" | "'SideStory'" | "'Summary'"
RelationType = {
  Other = "Other",
  SameSetting = "SameSetting",
  AlternativeSetting = "AlternativeSetting",
  AlternativeVersion = "AlternativeVersion",
  SharedCharacters = "SharedCharacters",
  Prequel = "Prequel",
  MainStory = "MainStory",
  FullStory = "FullStory",
  Sequel = "Sequel",
  SideStory = "SideStory",
  Summary = "Summary"
}

---@alias EpisodeType "'Episode'" | "'Credits'" | "'Special'" | "'Trailer'" | "'Parody'" | "'Other'"
EpisodeType = {
  Episode = "Episode",
  Credits = "Credits",
  Special = "Special",
  Trailer = "Trailer",
  Parody = "Parody",
  Other = "Other"
}

---@alias TitleType "'None'" | "'Main'" | "'Official'" | "'Short'" | "'Synonym'" | "'TitleCard'" | "'KanjiReading'"
TitleType = {
  None = "None",
  Main = "Main",
  Official = "Official",
  Short = "Short",
  Synonym = "Synonym",
  TitleCard = "TitleCard",
  KanjiReading = "KanjiReading"
}

---@alias Language "'Unknown'" | "'English'" | "'Romaji'" | "'Japanese'" | "'Afrikaans'" | "'Arabic'" | "'Bangladeshi'" | "'Bulgarian'" | "'FrenchCanadian'" | "'Czech'" | "'Danish'" | "'German'" | "'Greek'" | "'Spanish'" | "'Estonian'" | "'Finnish'" | "'French'" | "'Galician'" | "'Hebrew'" | "'Hungarian'" | "'Italian'" | "'Korean'" | "'Lithuania'" | "'Mongolian'" | "'Malaysian'" | "'Dutch'" | "'Norwegian'" | "'Polish'" | "'Portuguese'" | "'BrazilianPortuguese'" | "'Romanian'" | "'Russian'" | "'Slovak'" | "'Slovenian'" | "'Serbian'" | "'Swedish'" | "'Thai'" | "'Turkish'" | "'Ukrainian'" | "'Vietnamese'" | "'Chinese'" | "'ChineseSimplified'" | "'ChineseTraditional'" | "'Pinyin'" | "'Latin'" | "'Albanian'" | "'Basque'" | "'Bengali'" | "'Bosnian'"
Language = {
  Unknown = "Unknown",
  English = "English",
  Romaji = "Romaji",
  Japanese = "Japanese",
  Afrikaans = "Afrikaans",
  Arabic = "Arabic",
  Bangladeshi = "Bangladeshi",
  Bulgarian = "Bulgarian",
  FrenchCanadian = "FrenchCanadian",
  Czech = "Czech",
  Danish = "Danish",
  German = "German",
  Greek = "Greek",
  Spanish = "Spanish",
  Estonian = "Estonian",
  Finnish = "Finnish",
  French = "French",
  Galician = "Galician",
  Hebrew = "Hebrew",
  Hungarian = "Hungarian",
  Italian = "Italian",
  Korean = "Korean",
  Lithuania = "Lithuania",
  Mongolian = "Mongolian",
  Malaysian = "Malaysian",
  Dutch = "Dutch",
  Norwegian = "Norwegian",
  Polish = "Polish",
  Portuguese = "Portuguese",
  BrazilianPortuguese = "BrazilianPortuguese",
  Romanian = "Romanian",
  Russian = "Russian",
  Slovak = "Slovak",
  Slovenian = "Slovenian",
  Serbian = "Serbian",
  Swedish = "Swedish",
  Thai = "Thai",
  Turkish = "Turkish",
  Ukrainian = "Ukrainian",
  Vietnamese = "Vietnamese",
  Chinese = "Chinese",
  ChineseSimplified = "ChineseSimplified",
  ChineseTraditional = "ChineseTraditional",
  Pinyin = "Pinyin",
  Latin = "Latin",
  Albanian = "Albanian",
  Basque = "Basque",
  Bengali = "Bengali",
  Bosnian = "Bosnian"
}

--#endregion enumerations




---Returns formatted episode numbers with padding
---@param pad integer
---@return string
function episode_numbers(pad) end

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
---@type Group|nil
group = nil
use_existing_anime_location = false
replace_illegal_chars = false
remove_illegal_chars = false