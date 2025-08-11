---@meta

---@class (exact) AniDb
---@field id integer # AniDB file ID
---@field censored boolean # Whether the release is censored
---@field source string # Source media of the release e.g. DVD, BD, Web, etc.
---@field version integer # Version number of the release
---@field releasedate DateTime # Release date of the file
---@field description string # Description or notes about the release
---@field releasegroup ReleaseGroup|nil # Information about the release group
---@field media AniDbMedia # Media information from AniDB
local AniDb = {}

---@class (exact) AniDbMedia
---@field sublanguages Language[] # List of subtitle languages available in the release
---@field dublanguages Language[] # List of audio languages available in the release
local AniDbMedia = {}

---@class (exact) Anime
---@field airdate DateTime|nil # First air date of the anime
---@field enddate DateTime|nil # Last air date of the anime
---@field rating number # Average rating of the anime
---@field restricted boolean # Whether the anime is age-restricted
---@field type AnimeType # Type of the anime (Movie, TVSeries, etc.)
---@field preferredname string # The preferred title for the anime
---@field defaultname string # The default title for the anime
---@field id integer # AniDB anime ID
---@field titles Title[] # All available titles for the anime
---@field episodecounts table<EpisodeType, integer> # Count of episodes by type
---@field relations Relation[] # Related anime entries, not populated for nested Anime entries
---@field studios string[] # List of studios that produced the anime
local Anime = {}

---Get the anime title in the specified language
---@param lang Language The language to get the title in
---@param include_unofficial boolean|nil Whether to include unofficial titles
---@return string|nil
function Anime:getname(lang, include_unofficial) end

---@class (exact) Audio
---@field compressionmode string # Audio compression mode
---@field channels number # Number of audio channels, may have decimal part '.1'
---@field samplingrate integer # Audio sampling rate in Hz
---@field codec string # Audio codec name
---@field language string # Audio track language
---@field title string|nil # Audio track title or name
local Audio = {}

---@class (exact) DateTime
---@field year integer # Year (four digits)
---@field month integer # Month (1-12)
---@field day integer # Day of the month (1-31)
---@field yday integer # Day of the year (1-366)
---@field wday integer # Day of the week (1-7, 1 is Sunday)
---@field hour integer # Hour (0-23)
---@field min integer # Minute (0-59)
---@field sec integer # Second (0-59)
---@field isdst boolean # Is Daylight Saving Time in effect
local DateTime = {}

---@class (exact) Episode
---@field duration integer # Duration of the episode in seconds
---@field number integer # Episode number
---@field type EpisodeType # Type of the episode
---@field airdate DateTime|nil # Air date of the episode
---@field animeid integer # ID of the anime this episode belongs to
---@field id integer # AniDB episode ID
---@field titles Title[] # All available titles for the episode
---@field prefix string # Episode number type prefix (e.g., '', 'C', 'S', 'T', 'P', 'O')
local Episode = {}

---Get the episode title in the specified language
---@param lang Language The language to get the title in
---@return string|nil
function Episode:getname(lang) end

---@class (exact) File
---@field name string # The name of the file without extension
---@field extension string # The file extension including the dot
---@field path string # The full path to the file
---@field size integer # The file size in bytes
---@field importfolder ImportFolder # The import folder containing this file
---@field earliestname string|nil # The earliest known name of the file
---@field media Media|nil # Media information (via MediaInfo) for the file
---@field anidb AniDb|nil # AniDB information for the file
---@field hashes Hashes # File hashes
local File = {}

---@class (exact) Group
---@field name string # The name of the group
---@field mainanime Anime # The main anime in the group
---@field animes Anime[] # All animes in the group
local Group = {}

---@class (exact) Hashes
---@field crc string|nil # CRC32 hash of the file
---@field md5 string|nil # MD5 hash of the file
---@field ed2k string # ED2K hash of the file
---@field sha1 string|nil # SHA1 hash of the file
local Hashes = {}

---@class (exact) ImportFolder
---@field id integer # The Shoko import folder ID
---@field name string # Name of the import folder
---@field location string # File system path to the import folder
---@field type ImportFolderType # Type of the import folder
local ImportFolder = {}

---@class (exact) Media
---@field chaptered boolean # Whether the media file contains chapters
---@field duration integer # Duration of the media in seconds
---@field bitrate integer # Overall bitrate of the media file
---@field sublanguages string[] # List of subtitle languages
---@field audio Audio[] # List of audio tracks
---@field video Video|nil # Video stream information
local Media = {}

---@class (exact) Relation
---@field anime Anime # The related anime
---@field type RelationType # Type of relation between the anime
local Relation = {}

---@class (exact) ReleaseGroup
---@field name string # Full name of the release group
---@field shortname string # Abbreviated name or acronym of the release group
local ReleaseGroup = {}

---@class (exact) Title
---@field name string # The title text
---@field language Language # Language of the title
---@field languagecode string # ISO language code
---@field type TitleType # Type of the title
local Title = {}

---@class (exact) Tmdb
---@field movies TmdbMovie[] # List of TMDB movies related to the file
---@field shows TmdbShow[] # List of TMDB shows related to the file
---@field episodes TmdbEpisode[] # List of TMDB episodes related to the file
local Tmdb = {}

---@class (exact) TmdbEpisode
---@field id integer # TMDB episode ID
---@field showid integer # TMDB show ID
---@field titles Title[] # All available titles for the episode
---@field defaultname string # Default episode title
---@field preferredname string # Preferred episode title
---@field type EpisodeType # Type of episode
---@field number integer # Episode number within the season
---@field seasonnumber integer # Season number
---@field airdate DateTime|nil # Air date of the episode
local TmdbEpisode = {}

---Get the episode title in the specified language
---@param lang Language The language to get the title in
---@return string|nil
function TmdbEpisode:getname(lang) end

---@class (exact) TmdbMovie
---@field id integer # TMDB movie ID
---@field titles Title[] # All available titles for the movie
---@field defaultname string # Default movie title
---@field preferredname string # Preferred movie title
---@field rating number # Movie rating
---@field restricted boolean # Whether the movie is age-restricted
---@field studios string[] # List of production studios
---@field airdate DateTime|nil # Air date of the movie
local TmdbMovie = {}

---Get the movie title in the specified language
---@param lang Language The language to get the title in
---@return string|nil
function TmdbMovie:getname(lang) end

---@class (exact) TmdbShow
---@field id integer # TMDB show ID
---@field titles Title[] # All available titles for the show
---@field defaultname string # Default show title
---@field preferredname string # Preferred show title
---@field rating number # Show rating
---@field restricted boolean # Whether the show is age-restricted
---@field studios string[] # List of production studios
---@field episodecount integer # Total number of episodes
---@field airdate DateTime|nil # Air date of the show
---@field enddate DateTime|nil # End date of the show
local TmdbShow = {}

---Get the show title in the specified language
---@param lang Language The language to get the title in
---@return string|nil
function TmdbShow:getname(lang) end

---@class (exact) Video
---@field height integer # Video height in pixels
---@field width integer # Video width in pixels
---@field codec string # Video codec name
---@field res string # Resolution string e.g. '1080p', '720p', etc.
---@field bitrate integer # Video bitrate in bits per second
---@field bitdepth integer # Color depth in bits per channel
---@field framerate number # Frame rate in frames per second
local Video = {}
