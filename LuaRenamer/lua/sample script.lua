replace_illegal_chars = true

local maxnamelen = 35
local animelanguage = Language.English
local episodelanguage = Language.English


local group = file.anidb and file.anidb.releasegroup and "[" .. (file.anidb.releasegroup.shortname or file.anidb.releasegroup.name) .. "]" or ""

local animename = anime:getname(animelanguage) or anime.preferredname
if utf8.len(animename) > maxnamelen then
  animename = animename:sub(1, utf8.offset(animename, maxnamelen+1, 1)-1):gsub("%s+$", "") .. "..."
end

local episodename = ""
local engepname = episode:getname(Language.English) or ""
local episodenumber = ""
if anime.type ~= AnimeType.Movie or not engepname:find("^Complete Movie") then
  local maxepnum = #tostring(fromDictionary(anime.episodecounts):select(function(kvp) return kvp.value; end):max())
  episodenumber = episode_numbers(maxepnum) .. (file.anidb and file.anidb.version > 1 and "v" .. file.anidb.version or "")
  if #episodes == 1 and not engepname:find("^Episode") then
    episodename = episode:getname(episodelanguage) or ""
    if utf8.len(episodename) > maxnamelen then
        episodename = episodename:sub(1, utf8.offset(episodename, maxnamelen+1, 1)-1):gsub("%s+$", "") .. "..."
    end
  end
end

local res = file.media.video.res or ""
local codec = file.media.video.codec or ""
local bitdepth = file.media.video.bitdepth and file.media.video.bitdepth ~= 8 and file.media.video.bitdepth .. "bit" or ""
local source = file.anidb and file.anidb.source or ""

function rm_empty_str(table) return from(table):where(function (a) return a ~= "" end):toArray() end
local fileinfo = "(" .. table.concat(rm_empty_str{res, codec, bitdepth, source}, " ") .. ")"

local dublangs = from(file.anidb and file.anidb.media.dublanguages or from(file.media.audio):select("language"))
local sublangs = from(file.anidb and file.anidb.media.sublanguages or file.media.sublanguages)
local audiotag = dublangs:contains(Language.English) and (dublangs:contains(Language.Japanese) and "[DUAL-AUDIO]" or "[DUB]") or ""
local subtag = audiotag == "" and not sublangs:contains(Language.English) and "[RAW]" or ""

local centag = anime.restricted and file.anidb and (file.anidb.censored and "[CEN]" or "[UNCEN]") or ""
local hashtag = "[" .. file.hashes.crc .. "]"

local nametable = rm_empty_str{group, animename, episodenumber, episodename, fileinfo, audiotag, subtag, centag, hashtag}
filename = table.concat(nametable, " ")
