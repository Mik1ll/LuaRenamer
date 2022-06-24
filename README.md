# LuaRenamer

Lua file renaming and moving plugin for Shoko. Uses Lua 5.4.

## Installation

1. Download the [latest release](https://github.com/Mik1ll/LuaRenamer/releases/latest)
2. Unzip the files into a folder under the install location/Shoko Server/plugins or in the application data folder C:\ProgramData\ShokoServer\plugins (Windows),
   ~~/home/.shoko/Shoko.CLI/plugins (Linux CLI)~~ Linux is not supported yet
3. (Optional) You may COPY (not move) the lua sub-directory to a convenient location for script editing
4. It is strongly recommened to install VS Code and [the Lua extension](https://marketplace.visualstudio.com/items?itemName=sumneko.lua) to edit your script. The
   extension uses [EmmyLua for annotations](https://github.com/sumneko/lua-language-server/wiki/EmmyLua-Annotations)
5. Follow instructions in the next section to add your script

## Usage

1. Open the lua folder under the directory you extracted, in VS Code
2. Look at the sample script and the bottom of defs.lua to get an idea of available variables
3. Create a new file or modify the sample script to fit your needs
4. Open Shoko Desktop
5. Navigate to Utilities/File Renaming
6. Use the Default script or create a new one and set the type of the script to LuaRenamer in the drop-down menu
7. Copy your script and paste it in the text box
8. Test your script by adding files and clicking preview in the utility under the text box until you are satisfied
9. Check run on import, and save the script (next to the script type drop-down)
10. You may manually rename and move the files (if checked) of your collection in the utility

### Important Notes for File Moving

If 'use_existing_anime_location' is set to true, the last added file's location from the same anime will be used if it exists. The only destination folders
settable by the renamer are import folders with Drop Type of Destination or Both.  
The final destination MUST match the name or absolute path of a drop folder in order to move the file.  
Destination defaults to the first destination folder it can find.  
Subfolder defaults to your preferred language anime title.

## Script Environment

The lua environment is sandboxed, removing operations from standard libraries such as io, and os. See BaseEnv in [NLuaSingleton](./LuaRenamer/NLuaSingleton.cs)
.  
Additionally, a modified version of [lualinq from xanathar](https://github.com/xanathar/lualinq), licensed under the BSD 3 clause, has
been [included](./LuaRenamer/lua/lualinq.lua) for convenience. [Original Documentation](./LuaRenamer/lua/LuaLinq.pdf)

See [defs.lua](./LuaRenamer/lua/defs.lua) for all exposed data definitions/structure available from shoko.

## Sample Script

```lua
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
```

### Snippets

WIP
