function rm_empty_str(table) return from(table):where(function (a) return a ~= "" end):toArray() end

local group = file.anidb and file.anidb.releasegroup and "[" .. (file.anidb.releasegroup.shortname or file.anidb.releasegroup.name) .. "]" or ""

local animename = anime:getname(Language.English) or anime:getname(Language.Romaji) or anime.preferredname
animename = string.gsub(string.sub(animename, 0, 35), "%s+$", "") .. (#animename > 35 and "..." or "")

local episodename = episode:getname(Language.English) or ""
episodename = string.gsub(string.sub(episodename, 0, 35), "%s+$", "") .. (#episodename > 35 and "..." or "")

local episodenumber = ""
if anime.type ~= AnimeType.Movie or not string.find(episodename, "Complete Movie") then
  episodenumber = episode_numbers(2) .. (file.anidb and file.anidb.version > 1 and "v" .. file.anidb.version or "")
  if #episodes > 1 or string.find(episodename, "Episode") then
    episodename = ""
  end
else
  episodename = ""
end

local res = file.media.video.res or ""
local codec = file.media.video.codec or ""
local bitdepth = file.media.video.bitdepth and file.media.video.bitdepth ~= 8 and file.media.video.bitdepth .. "bit" or ""
local source = file.anidb and file.anidb.source or ""

local fileinfo = "(" .. table.concat(rm_empty_str{res, codec, bitdepth, source}, " ") .. ")"

local dublangs = from(file.anidb and file.anidb.media.dublanguages or from(file.media.audio):select("language"))
local sublangs = from(file.anidb and file.anidb.media.sublanguages or file.media.sublanguages)
local audiotag = dublangs:contains(Language.English) and (dublangs:contains(Language.Japanese) and "[DUAL-AUDIO]" or "[DUB]") or ""
local subtag = audiotag == "" and not sublangs:contains(Language.English) and "[RAW]" or ""

local centag = anime.restricted and file.anidb and (file.anidb.censored and "[CEN]" or "[UNCEN]") or ""
local hashtag = "[" .. file.hashes.crc .. "]"

local nametable = rm_empty_str{group, animename, episodenumber, episodename, fileinfo, audiotag, subtag, centag, hashtag}
filename = string.sub(table.concat(nametable, " "), 0, 120)
