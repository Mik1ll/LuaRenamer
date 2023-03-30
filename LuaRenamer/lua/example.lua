remove_illegal_chars = false
replace_illegal_chars = true

local maxnamelen = 35
local animelanguage = Language.English
local episodelanguage = Language.English
local spacechar = " "

local group = file.anidb and file.anidb.releasegroup and "[" .. (file.anidb.releasegroup.shortname or file.anidb.releasegroup.name) .. "]" or ""

local animename = anime:getname(animelanguage) or anime.preferredname

local episodename = ""
local engepname = episode:getname(Language.English) or ""
local episodenumber = ""
if anime.type ~= AnimeType.Movie or not engepname:find("^Complete Movie") then
  episodenumber = episode_numbers(#tostring(math.max(anime.episodecounts[episode.type], 10))) .. (file.anidb and file.anidb.version > 1 and "v" .. file.anidb.version or "")
  if #episodes == 1 and not engepname:find("^Episode") and not engepname:find("^OVA") then
    episodename = episode:getname(episodelanguage) or ""
  end
end

local res = file.media.video.res or ""
local codec = file.media.video.codec or ""
local bitdepth = file.media.video.bitdepth and file.media.video.bitdepth ~= 8 and file.media.video.bitdepth .. "bit" or ""
local source = file.anidb and file.anidb.source or ""
local fileinfo = "(" .. tostring(table.concat({res, codec, bitdepth, source}, " ")):clean_spaces(spacechar) .. ")"

local dublangs = from(file.anidb and file.anidb.media.dublanguages or from(file.media.audio):select("language"))
local audiotag = dublangs:contains(Language.English) and (dublangs:contains(Language.Japanese) and "[DUAL-AUDIO]" or "[DUB]") or ""

local sublangs = from(file.anidb and file.anidb.media.sublanguages or file.media.sublanguages)
local subtag = audiotag == "" and not sublangs:contains(Language.English) and "[RAW]" or ""

local centag = anime.restricted and file.anidb and (file.anidb.censored and "[CEN]" or "[UNCEN]") or ""
local hashtag = "[" .. file.hashes.crc .. "]"

local nametable = {group, animename:truncate(maxnamelen), episodenumber, episodename:truncate(maxnamelen), fileinfo, audiotag, subtag, centag, hashtag}
filename = table.concat(nametable, " "):clean_spaces(spacechar)
subfolder = {animename}
