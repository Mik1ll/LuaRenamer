---@meta

--#region class definitions

---@class (exact) File
---@field name string
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
---@field video Video
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
---@field id integer
---@field titles Title[]
---@field episodecounts table<EpisodeType, integer>
---@field relations Relation[]
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
---@param include_unofficial? boolean
---@return string?
function Episode:getname(lang, include_unofficial)
end

---@class (exact) ImportFolder
---@field name string
---@field location string
---@field type ImportFolderType
local ImportFolder = {}

---@class (exact) Group
---@field name string
---@field mainanime Anime?
---@field animes Anime[]
local Group = {}

---@class (exact) Relation
---@field type RelationType
---@field anime Anime
local Relation = {}

--#endregion class definitions

--#region enumerations

---@enum ImportFolderType
ImportFolderType = {
  Excluded = "Excluded",
  Source = "Source",
  Destination = "Destination",
  Both = "Both"
}

---@enum AnimeType
AnimeType = {
  Movie = "Movie",
  OVA = "OVA",
  TVSeries = "TVSeries",
  TVSpecial = "TVSpecial",
  Web = "Web",
  Other = "Other"
}

---@enum EpisodeType
EpisodeType = {
  Episode = "Episode",
  Credits = "Credits",
  Special = "Special",
  Trailer = "Trailer",
  Parody = "Parody",
  Other = "Other"
}

---@enum TitleType
TitleType = {
  None = "None",
  Main = "Main",
  Official = "Official",
  Short = "Short",
  Synonym = "Synonym",
  TitleCard = "TitleCard",
  KanjiReading = "KanjiReading"
}

---@enum Language
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
  Lithuanian = "Lithuanian",
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
  Bosnian = "Bosnian",
  Amharic = "Amharic",
  Armenian = "Armenian",
  Azerbaijani = "Azerbaijani",
  Belarusian = "Belarusian",
  Catalan = "Catalan",
  Chichewa = "Chichewa",
  Corsican = "Corsican",
  Croatian = "Croatian",
  Divehi = "Divehi",
  Esperanto = "Esperanto",
  Fijian = "Fijian",
  Georgian = "Georgian",
  Gujarati = "Gujarati",
  HaitianCreole = "HaitianCreole",
  Hausa = "Hausa",
  Icelandic = "Icelandic",
  Igbo = "Igbo",
  Indonesian = "Indonesian",
  Irish = "Irish",
  Javanese = "Javanese",
  Kannada = "Kannada",
  Kazakh = "Kazakh",
  Khmer = "Khmer",
  Kurdish = "Kurdish",
  Kyrgyz = "Kyrgyz",
  Lao = "Lao",
  Latvian = "Latvian",
  Luxembourgish = "Luxembourgish",
  Macedonian = "Macedonian",
  Malagasy = "Malagasy",
  Malayalam = "Malayalam",
  Maltese = "Maltese",
  Maori = "Maori",
  Marathi = "Marathi",
  MyanmarBurmese = "MyanmarBurmese",
  Nepali = "Nepali",
  Oriya = "Oriya",
  Pashto = "Pashto",
  Persian = "Persian",
  Punjabi = "Punjabi",
  Quechua = "Quechua",
  Samoan = "Samoan",
  ScotsGaelic = "ScotsGaelic",
  Sesotho = "Sesotho",
  Shona = "Shona",
  Sindhi = "Sindhi",
  Sinhala = "Sinhala",
  Somali = "Somali",
  Swahili = "Swahili",
  Tajik = "Tajik",
  Tamil = "Tamil",
  Tatar = "Tatar",
  Telugu = "Telugu",
  Turkmen = "Turkmen",
  Uighur = "Uighur",
  Uzbek = "Uzbek",
  Welsh = "Welsh",
  Xhosa = "Xhosa",
  Yiddish = "Yiddish",
  Yoruba = "Yoruba",
  Zulu = "Zulu",
}

---@enum RelationType
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

---@type string?
filename = nil
---@type (string|string[])?
destination = nil
---@type string[]?
subfolder = nil
---@type File
file = nil
---@type Anime
anime = nil
---@type Episode
episode = Episode
---@type Anime[]
animes = nil
---@type Episode[]
episodes = nil
---@type ImportFolder[]
importfolders = {}
---@type Group[]
groups = nil
---@type Group?
group = nil
use_existing_anime_location = false
replace_illegal_chars = false
remove_illegal_chars = false
skip_rename = false
skip_move = false
