local nametable = {
  add = function(self, str)
    table.insert(self, str)
  end
}

if file.anidb and file.anidb.releasegroup then
  nametable:add("[" .. file.anidb.releasegroup.shortname or file.anidb.releasegroup.name .. "]")
end
local animename = anime:getname(Language.English) or anime:getname(Language.Romaji) or anime.preferredname
nametable:add(animename)

local episodename = episode:getname(Language.English)
if not (anime.type == AnimeType.Movie and string.find(episodename, "Complete Movie")) then
  nametable:add(episode_numbers(2))
  if file.anidb and file.anidb.version > 1 then
    nametable[#nametable] = nametable[#nametable] .. "v" .. file.anidb.version
  end
  if #episodes == 1 and not string.find(episodename, "Episode") then
    nametable:add(string.sub(episodename, 0, 35))
  end
end

nametable:add("(" .. file.media.video.res)
nametable:add(file.media.video.codec)
if file.media.video.bitdepth and file.media.video.bitdepth ~= 8 then
  nametable:add(file.media.video.bitdepth .. "bit")
end
if file.anidb then
  nametable:add(file.anidb.source)
end
nametable[#nametable] = nametable[#nametable] .. ")"

local dublangs = from(file.anidb and file.anidb.media.dublanguages or from(file.media.audio):select(function(a)
  return a.language
end))
local sublangs = from(file.anidb and file.anidb.media.sublanguages or file.media.sublanguages)
if dublangs:any(function(a)
  return a == Language.English
end) then
  if dublangs:any(function(a)
    return a == Language.Japanese
  end) then
    nametable:add("[DUAL-AUDIO]")
  else
    nametable:add("[DUB]")
  end
elseif not sublangs:any(function(a)
  return a == Language.English
end) then
  nametable:add("[RAW]")
end
if anime.restricted and file.anidb then
  if file.anidb.censored then
    nametable:add("[CEN]")
  else
    nametable:add("[UNCEN]")
  end
end
nametable:add("[" .. file.hashes.crc .. "]")
filename = string.sub(table.concat(nametable, " "), 0, 120)

